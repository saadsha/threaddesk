using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ThreadDesk.Core;
using ThreadDesk.Core.Models;
using ThreadDesk.Core.Repositories;
using ThreadDesk.Tally;
using Xunit;

namespace ThreadDesk.UnitTests;

public class TallySyncServiceTests
{
    [Fact]
    public async Task Sync_inserts_companies_and_company_scoped_ledgers()
    {
        var (service, _, companies, ledgers, _) = CreateService();

        var result = await service.SyncCompaniesAndLedgersAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.CompaniesInserted);
        Assert.Equal(4, result.LedgersInserted);
        Assert.Equal(2, companies.Items.Count);
        Assert.Equal(4, ledgers.Items.Count);
        Assert.Equal(2, ledgers.Items.Values.Count(ledger => ledger.ExternalId == "L1"));
    }

    [Fact]
    public async Task Sync_updates_existing_records_without_duplicates()
    {
        var (service, _, companies, ledgers, _) = CreateService();
        await service.SyncCompaniesAndLedgersAsync();

        var result = await service.SyncCompaniesAndLedgersAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.CompaniesInserted);
        Assert.Equal(2, result.CompaniesUpdated);
        Assert.Equal(0, result.LedgersInserted);
        Assert.Equal(4, result.LedgersUpdated);
        Assert.Equal(2, companies.Items.Count);
        Assert.Equal(4, ledgers.Items.Count);
    }

    [Fact]
    public async Task Sync_returns_failure_when_tally_connection_is_unavailable()
    {
        var provider = new MockTallyProvider { ConnectionSucceeds = false };
        var (service, _, _, _, states) = CreateService(provider);

        var result = await service.SyncCompaniesAndLedgersAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("Failed", states.Items["tally:companies"].Status);
    }

    [Fact]
    public async Task Sync_records_ledger_retrieval_failure_and_preserves_companies()
    {
        var provider = new MockTallyProvider { ThrowOnLedgerRead = true };
        var (service, _, companies, ledgers, states) = CreateService(provider);

        var result = await service.SyncCompaniesAndLedgersAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(2, companies.Items.Count);
        Assert.Empty(ledgers.Items);
        Assert.Equal("Failed", states.Items["company-1:ledgers"].Status);
    }

    [Fact]
    public async Task Sync_returns_failure_when_sqlite_persistence_fails()
    {
        var provider = new MockTallyProvider();
        var states = new InMemorySyncStateRepository();
        var service = new TallySyncService(
            provider,
            new ThrowingCompanyRepository(),
            new InMemoryLedgerRepository(),
            states,
            Options.Create(new AppConfig()),
            NullLogger<TallySyncService>.Instance);

        var result = await service.SyncCompaniesAndLedgersAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("Failed", states.Items["tally:companies"].Status);
    }

    private static (TallySyncService Service, MockTallyProvider Provider, InMemoryCompanyRepository Companies, InMemoryLedgerRepository Ledgers, InMemorySyncStateRepository States) CreateService(MockTallyProvider? provider = null)
    {
        provider ??= new MockTallyProvider();
        var companies = new InMemoryCompanyRepository();
        var ledgers = new InMemoryLedgerRepository();
        var states = new InMemorySyncStateRepository();
        return (new TallySyncService(provider, companies, ledgers, states, Options.Create(new AppConfig()), NullLogger<TallySyncService>.Instance), provider, companies, ledgers, states);
    }

    private sealed class InMemoryCompanyRepository : ICompanyRepository
    {
        public Dictionary<string, Company> Items { get; } = new();
        public Task<UpsertResult> AddOrUpdateAsync(Company company)
        {
            var result = Items.ContainsKey(company.Id) ? UpsertResult.Updated : UpsertResult.Inserted;
            Items[company.Id] = company;
            return Task.FromResult(result);
        }
        public Task<Company?> GetByIdAsync(string id) => Task.FromResult(Items.GetValueOrDefault(id));
        public Task<IEnumerable<Company>> ListAsync() => Task.FromResult<IEnumerable<Company>>(Items.Values);
    }

    private sealed class ThrowingCompanyRepository : ICompanyRepository
    {
        public Task<UpsertResult> AddOrUpdateAsync(Company company) => throw new InvalidOperationException("Simulated SQLite failure.");
        public Task<Company?> GetByIdAsync(string id) => Task.FromResult<Company?>(null);
        public Task<IEnumerable<Company>> ListAsync() => Task.FromResult<IEnumerable<Company>>(Array.Empty<Company>());
    }

    private sealed class InMemoryLedgerRepository : ILedgerRepository
    {
        public Dictionary<string, Ledger> Items { get; } = new();
        public Task<UpsertResult> AddOrUpdateAsync(Ledger ledger)
        {
            var result = Items.ContainsKey(ledger.Id) ? UpsertResult.Updated : UpsertResult.Inserted;
            Items[ledger.Id] = ledger;
            return Task.FromResult(result);
        }
        public Task<Ledger?> GetByIdAsync(string id) => Task.FromResult(Items.GetValueOrDefault(id));
        public Task<IEnumerable<Ledger>> ListByCompanyAsync(string companyId) => Task.FromResult<IEnumerable<Ledger>>(Items.Values.Where(ledger => ledger.CompanyId == companyId));
    }

    private sealed class InMemorySyncStateRepository : ISyncStateRepository
    {
        public Dictionary<string, SyncState> Items { get; } = new();
        public Task UpsertAsync(SyncState state) { Items[state.Id] = state; return Task.CompletedTask; }
        public Task<SyncState?> GetByIdAsync(string id) => Task.FromResult(Items.GetValueOrDefault(id));
        public Task<IEnumerable<SyncState>> ListByCompanyAsync(string companyId) => Task.FromResult<IEnumerable<SyncState>>(Items.Values.Where(state => state.CompanyId == companyId));
    }
}
