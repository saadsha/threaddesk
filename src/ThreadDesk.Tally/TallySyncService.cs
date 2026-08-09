using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThreadDesk.Core;
using ThreadDesk.Core.Models;
using ThreadDesk.Core.Repositories;

namespace ThreadDesk.Tally;

public sealed record TallySyncResult(
    bool Succeeded,
    int CompaniesDiscovered,
    int CompaniesInserted,
    int CompaniesUpdated,
    int LedgersDiscovered,
    int LedgersInserted,
    int LedgersUpdated,
    int FailedCompanyLedgerSyncs);

public class TallySyncService
{
    private const string ConnectorStateCompanyId = "tally-connector";
    private const string CompaniesStateId = "tally:companies";
    private readonly ITallyConnectionManager _tally;
    private readonly ICompanyRepository _companyRepo;
    private readonly ILedgerRepository _ledgerRepo;
    private readonly ISyncStateRepository _syncStateRepo;
    private readonly AppConfig _config;
    private readonly ILogger<TallySyncService> _logger;

    public TallySyncService(
        ITallyConnectionManager tally,
        ICompanyRepository companyRepo,
        ILedgerRepository ledgerRepo,
        ISyncStateRepository syncStateRepo,
        IOptions<AppConfig> config,
        ILogger<TallySyncService> logger)
    {
        _tally = tally;
        _companyRepo = companyRepo;
        _ledgerRepo = ledgerRepo;
        _syncStateRepo = syncStateRepo;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<TallySyncResult> SyncCompaniesAndLedgersAsync(CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var result = new SyncCounters();
        _logger.LogInformation("Tally sync started.");
        await SaveStateSafelyAsync(NewState(CompaniesStateId, ConnectorStateCompanyId, "Companies", "Running", startedAt));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await _tally.TestConnectionAsync())
                throw new InvalidOperationException("Tally connection test returned unsuccessful.");

            _logger.LogInformation("Tally connection successful.");
            var companies = (await _tally.GetCompaniesAsync())
                .Where(company => IsValid(company.Id, company.Name, "company"))
                .GroupBy(company => company.Id!, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();

            if (!string.IsNullOrWhiteSpace(_config.TallyCompanyExternalId))
            {
                companies = companies
                    .Where(company => string.Equals(company.Id, _config.TallyCompanyExternalId, StringComparison.Ordinal))
                    .ToList();
            }

            result.CompaniesDiscovered = companies.Count;
            _logger.LogInformation("Companies discovered: {Count}", result.CompaniesDiscovered);

            foreach (var tallyCompany in companies)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var company = new Company { Id = tallyCompany.Id!, Name = tallyCompany.Name };
                Count(await _companyRepo.AddOrUpdateAsync(company), result, isCompany: true);
                await SyncLedgersForCompanyAsync(tallyCompany, result, cancellationToken);
            }

            var succeeded = result.FailedCompanyLedgerSyncs == 0;
            var status = succeeded ? "Completed" : "CompletedWithErrors";
            await SaveStateSafelyAsync(NewState(CompaniesStateId, ConnectorStateCompanyId, "Companies", status, startedAt, result, succeeded ? null : "One or more company ledger synchronizations failed."));
            LogCompletion(result, status);
            return result.ToResult(succeeded);
        }
        catch (OperationCanceledException)
        {
            await SaveStateSafelyAsync(NewState(CompaniesStateId, ConnectorStateCompanyId, "Companies", "Cancelled", startedAt, result, "Synchronization was cancelled."));
            _logger.LogInformation("Tally sync cancelled.");
            throw;
        }
        catch (Exception exception)
        {
            await SaveStateSafelyAsync(NewState(CompaniesStateId, ConnectorStateCompanyId, "Companies", "Failed", startedAt, result, SafeError(exception)));
            _logger.LogWarning("Tally sync failed: {FailureType}.", exception.GetType().Name);
            return result.ToResult(false);
        }
    }

    private async Task SyncLedgersForCompanyAsync(TallyCompany tallyCompany, SyncCounters counters, CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var stateId = $"{tallyCompany.Id}:ledgers";
        await SaveStateSafelyAsync(NewState(stateId, tallyCompany.Id!, "Ledgers", "Running", startedAt));

        try
        {
            _logger.LogInformation("Ledger sync started for a Tally company.");
            var ledgers = (await _tally.GetLedgersAsync(tallyCompany))
                .Where(ledger => IsValid(ledger.Id, ledger.Name, "ledger"))
                .GroupBy(ledger => ledger.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
            counters.LedgersDiscovered += ledgers.Count;
            _logger.LogInformation("Ledgers discovered: {Count}", ledgers.Count);

            var companyCounters = new SyncCounters { LedgersDiscovered = ledgers.Count };
            foreach (var tallyLedger in ledgers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var ledger = new Ledger
                {
                    Id = $"{tallyCompany.Id}:{tallyLedger.Id}",
                    CompanyId = tallyCompany.Id!,
                    ExternalId = tallyLedger.Id,
                    Name = tallyLedger.Name,
                    GroupName = tallyLedger.Group,
                    UpdatedAt = DateTime.UtcNow.ToString("O")
                };
                var upsertResult = await _ledgerRepo.AddOrUpdateAsync(ledger);
                Count(upsertResult, counters, isCompany: false);
                Count(upsertResult, companyCounters, isCompany: false);
            }

            await SaveStateSafelyAsync(NewState(stateId, tallyCompany.Id!, "Ledgers", "Completed", startedAt, companyCounters));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            counters.FailedCompanyLedgerSyncs++;
            await SaveStateSafelyAsync(NewState(stateId, tallyCompany.Id!, "Ledgers", "Failed", startedAt, null, SafeError(exception)));
            _logger.LogWarning("Ledger sync failed for one Tally company: {FailureType}.", exception.GetType().Name);
        }
    }

    private bool IsValid(string? externalId, string? name, string entityType)
    {
        if (!string.IsNullOrWhiteSpace(externalId) && !string.IsNullOrWhiteSpace(name)) return true;
        _logger.LogWarning("Skipping malformed Tally {EntityType}; a stable external ID and name are required.", entityType);
        return false;
    }

    private async Task SaveStateSafelyAsync(SyncState state)
    {
        try
        {
            await _syncStateRepo.UpsertAsync(state);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Unable to record Tally sync state: {FailureType}.", exception.GetType().Name);
        }
    }

    private static SyncState NewState(string id, string companyId, string entityType, string status, DateTime startedAt, SyncCounters? counters = null, string? error = null) => new()
    {
        Id = id,
        CompanyId = companyId,
        EntityType = entityType,
        StartedAt = startedAt,
        CompletedAt = status is "Running" ? null : DateTime.UtcNow,
        LastSyncedAt = status.StartsWith("Completed", StringComparison.Ordinal) ? DateTime.UtcNow : null,
        Status = status,
        RecordsDiscovered = counters?.TotalDiscovered ?? 0,
        RecordsInserted = counters?.TotalInserted ?? 0,
        RecordsUpdated = counters?.TotalUpdated ?? 0,
        ErrorMessage = error
    };

    private static void Count(UpsertResult result, SyncCounters counters, bool isCompany)
    {
        if (isCompany)
        {
            if (result == UpsertResult.Inserted) counters.CompaniesInserted++; else counters.CompaniesUpdated++;
        }
        else
        {
            if (result == UpsertResult.Inserted) counters.LedgersInserted++; else counters.LedgersUpdated++;
        }
    }

    private void LogCompletion(SyncCounters counters, string status)
    {
        _logger.LogInformation("Companies inserted: {Count}; companies updated: {Updated}.", counters.CompaniesInserted, counters.CompaniesUpdated);
        _logger.LogInformation("Ledgers inserted: {Count}; ledgers updated: {Updated}.", counters.LedgersInserted, counters.LedgersUpdated);
        _logger.LogInformation("Tally sync {Status}.", status);
    }

    private static string SafeError(Exception exception) => exception switch
    {
        InvalidOperationException => "Tally configuration or data validation failed.",
        _ => "Tally synchronization failed. See local agent logs for the failure type."
    };

    private sealed class SyncCounters
    {
        public int CompaniesDiscovered { get; set; }
        public int CompaniesInserted { get; set; }
        public int CompaniesUpdated { get; set; }
        public int LedgersDiscovered { get; set; }
        public int LedgersInserted { get; set; }
        public int LedgersUpdated { get; set; }
        public int FailedCompanyLedgerSyncs { get; set; }
        public int TotalDiscovered => CompaniesDiscovered + LedgersDiscovered;
        public int TotalInserted => CompaniesInserted + LedgersInserted;
        public int TotalUpdated => CompaniesUpdated + LedgersUpdated;
        public TallySyncResult ToResult(bool succeeded) => new(succeeded, CompaniesDiscovered, CompaniesInserted, CompaniesUpdated, LedgersDiscovered, LedgersInserted, LedgersUpdated, FailedCompanyLedgerSyncs);
    }
}
