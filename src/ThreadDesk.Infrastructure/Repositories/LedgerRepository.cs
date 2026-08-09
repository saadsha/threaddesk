using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using ThreadDesk.Core.Models;
using ThreadDesk.Core.Repositories;
using ThreadDesk.Infrastructure;

namespace ThreadDesk.Infrastructure.Repositories;

public class LedgerRepository : ILedgerRepository
{
    private readonly ISqliteService _sqlite;

    public LedgerRepository(ISqliteService sqlite)
    {
        _sqlite = sqlite;
    }

    public async Task<UpsertResult> AddOrUpdateAsync(Ledger ledger)
    {
        var exists = await GetByIdAsync(ledger.Id) is not null;
        var sql = @"INSERT INTO ledgers (id, company_id, external_id, name, group_name, metadata, updated_at)
VALUES (@Id, @CompanyId, @ExternalId, @Name, @GroupName, @Metadata, @UpdatedAt)
ON CONFLICT(company_id, external_id) DO UPDATE SET
    id = excluded.id,
    name = excluded.name,
    group_name = excluded.group_name,
    metadata = excluded.metadata,
    updated_at = excluded.updated_at;";
        await _sqlite.ExecuteAsync(sql, new { ledger.Id, ledger.CompanyId, ledger.ExternalId, ledger.Name, ledger.GroupName, ledger.Metadata, ledger.UpdatedAt });
        return exists ? UpsertResult.Updated : UpsertResult.Inserted;
    }

    public async Task<Ledger?> GetByIdAsync(string id)
    {
        var sql = "SELECT id, company_id AS CompanyId, external_id AS ExternalId, name, group_name AS GroupName, metadata, updated_at AS UpdatedAt FROM ledgers WHERE id = @Id LIMIT 1";
        var rows = await _sqlite.QueryAsync<Ledger>(sql, new { Id = id });
        return System.Linq.Enumerable.FirstOrDefault(rows);
    }

    public async Task<IEnumerable<Ledger>> ListByCompanyAsync(string companyId)
    {
        var sql = "SELECT id, company_id AS CompanyId, external_id AS ExternalId, name, group_name AS GroupName, metadata, updated_at AS UpdatedAt FROM ledgers WHERE company_id = @CompanyId";
        return await _sqlite.QueryAsync<Ledger>(sql, new { CompanyId = companyId });
    }
}
