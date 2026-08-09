using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using ThreadDesk.Core.Models;
using ThreadDesk.Core.Repositories;
using ThreadDesk.Infrastructure;

namespace ThreadDesk.Infrastructure.Repositories;

public class CompanyRepository : ICompanyRepository
{
    private readonly ISqliteService _sqlite;

    public CompanyRepository(ISqliteService sqlite)
    {
        _sqlite = sqlite;
    }

    public async Task<UpsertResult> AddOrUpdateAsync(Company company)
    {
        var exists = await GetByIdAsync(company.Id) is not null;
        var sql = @"INSERT INTO companies (id, name, metadata) VALUES (@Id, @Name, @Metadata)
ON CONFLICT(id) DO UPDATE SET name = excluded.name, metadata = excluded.metadata;";
        await _sqlite.ExecuteAsync(sql, new { company.Id, company.Name, company.Metadata });
        return exists ? UpsertResult.Updated : UpsertResult.Inserted;
    }

    public async Task<Company?> GetByIdAsync(string id)
    {
        var sql = "SELECT id, name, metadata FROM companies WHERE id = @Id LIMIT 1";
        var rows = await _sqlite.QueryAsync<Company>(sql, new { Id = id });
        return System.Linq.Enumerable.FirstOrDefault(rows);
    }

    public async Task<IEnumerable<Company>> ListAsync()
    {
        var sql = "SELECT id, name, metadata FROM companies";
        return await _sqlite.QueryAsync<Company>(sql);
    }
}
