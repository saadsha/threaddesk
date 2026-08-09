using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThreadDesk.Core;

namespace ThreadDesk.Tally;

public class OdbcTallyProvider : ITallyConnectionManager
{
    private readonly string _connectionString;
    private readonly ILogger<OdbcTallyProvider> _logger;
    private readonly AppConfig _config;

    public OdbcTallyProvider(IOptions<AppConfig> config, ILogger<OdbcTallyProvider> logger)
    {
        _config = config.Value;
        _logger = logger;
        _connectionString = _config.TallyOdbcConnectionString ?? string.Empty;
    }

    private OdbcConnection CreateConnection() => new(_connectionString);

    public async Task<bool> TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _logger.LogWarning("ODBC connection string not configured for Tally provider.");
            return false;
        }

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            return true;
        }
        catch (OdbcException ex) when (ex.Message.Contains("IM002", StringComparison.Ordinal))
        {
            _logger.LogWarning("Tally ODBC connection failed because its driver or DSN was not found (IM002).");
            return false;
        }
        catch
        {
            _logger.LogWarning("Tally ODBC connection test failed.");
            return false;
        }
    }

    public async Task<IEnumerable<TallyCompany>> GetCompaniesAsync()
    {
        var query = _config.TallyCompaniesQuery ?? "SELECT $GUID, $Name FROM Company";
        var companies = new List<TallyCompany>();

        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = query;
        cmd.CommandTimeout = _config.TallyOdbcCommandTimeoutSeconds;
        await using var reader = await cmd.ExecuteReaderAsync();
        var idOrdinal = GetRequiredOrdinal(reader, "TallyCompaniesQuery", "Id", "$GUID");
        var nameOrdinal = GetRequiredOrdinal(reader, "TallyCompaniesQuery", "Name", "$Name");
        while (await reader.ReadAsync())
        {
            var id = reader.IsDBNull(idOrdinal) ? string.Empty : reader.GetValue(idOrdinal).ToString() ?? string.Empty;
            var name = reader.IsDBNull(nameOrdinal) ? string.Empty : reader.GetValue(nameOrdinal).ToString() ?? string.Empty;
            companies.Add(new TallyCompany(name, id));
        }

        return companies;
    }

    public async Task<IEnumerable<TallyLedger>> GetLedgersAsync(TallyCompany company)
    {
        // Tally's ODBC server scopes exposed collections to the company currently selected in TallyPrime.
        // Therefore this query must not attempt SQL-style company filtering or inject a company name.
        var query = _config.TallyLedgersQuery ?? "SELECT $GUID, $Name, $Parent FROM Ledger";
        if (string.IsNullOrWhiteSpace(company.Id))
            throw new InvalidOperationException("A stable Tally company external ID is required to read ledgers.");

        var ledgers = new List<TallyLedger>();
        await using var conn = CreateConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = query;
        cmd.CommandTimeout = _config.TallyOdbcCommandTimeoutSeconds;
        await using var reader = await cmd.ExecuteReaderAsync();
        var idOrdinal = GetRequiredOrdinal(reader, "TallyLedgersQuery", "Id", "$GUID");
        var nameOrdinal = GetRequiredOrdinal(reader, "TallyLedgersQuery", "Name", "$Name");
        var groupOrdinal = TryGetOrdinal(reader, "GroupName", "$Parent");
        while (await reader.ReadAsync())
        {
            var id = reader.IsDBNull(idOrdinal) ? string.Empty : reader.GetValue(idOrdinal).ToString() ?? string.Empty;
            var name = reader.IsDBNull(nameOrdinal) ? string.Empty : reader.GetValue(nameOrdinal).ToString() ?? string.Empty;
            var group = groupOrdinal is null || reader.IsDBNull(groupOrdinal.Value) ? null : reader.GetValue(groupOrdinal.Value).ToString();
            ledgers.Add(new TallyLedger(id, name, group));
        }

        return ledgers;
    }

    public async Task<IEnumerable<TallyVoucher>> GetVouchersAsync(DateTime? since = null)
    {
        var query = _config.TallyVouchersQuery ?? "SELECT VoucherId, Date, Narration, Amount FROM Vouchers";
        var vouchers = new List<TallyVoucher>();
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = since.HasValue
                ? query.Replace("{since}", "'" + since.Value.ToString("yyyy-MM-dd") + "'", StringComparison.Ordinal)
                : query;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.IsDBNull(0) ? string.Empty : reader.GetValue(0).ToString() ?? string.Empty;
                var date = reader.IsDBNull(1) ? DateTime.MinValue : reader.GetDateTime(1);
                var narration = reader.IsDBNull(2) ? string.Empty : reader.GetValue(2).ToString() ?? string.Empty;
                var amount = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3));
                vouchers.Add(new TallyVoucher(id, date, narration, amount));
            }
        }
        catch
        {
            _logger.LogWarning("Voucher retrieval failed.");
        }
        return vouchers;
    }

    public async Task<bool> CreateVoucherAsync(TallyVoucher voucher)
    {
        var insertTemplate = _config.TallyVouchersQuery;
        if (string.IsNullOrWhiteSpace(insertTemplate) || !insertTemplate.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("No INSERT template configured for vouchers; CreateVoucherAsync not supported.");
            return false;
        }

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = insertTemplate.Replace("{id}", "'" + voucher.Id + "'", StringComparison.Ordinal)
                .Replace("{date}", "'" + voucher.Date.ToString("yyyy-MM-dd") + "'", StringComparison.Ordinal)
                .Replace("{narration}", "'" + voucher.Narration.Replace("'", "''", StringComparison.Ordinal) + "'", StringComparison.Ordinal)
                .Replace("{amount}", voucher.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        catch
        {
            _logger.LogError("CreateVoucherAsync failed.");
            return false;
        }
    }

    private static int GetRequiredOrdinal(IDataRecord record, string settingName, params string[] columnNames)
    {
        return TryGetOrdinal(record, columnNames)
            ?? throw new InvalidOperationException($"{settingName} must return one of: {string.Join(", ", columnNames)}.");
    }

    private static int? TryGetOrdinal(IDataRecord record, params string[] columnNames)
    {
        for (var index = 0; index < record.FieldCount; index++)
        {
            if (columnNames.Any(columnName => string.Equals(record.GetName(index), columnName, StringComparison.OrdinalIgnoreCase))) return index;
        }
        return null;
    }
}
