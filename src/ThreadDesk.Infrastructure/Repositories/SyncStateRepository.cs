using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using ThreadDesk.Core.Models;
using ThreadDesk.Core.Repositories;
using ThreadDesk.Infrastructure;

namespace ThreadDesk.Infrastructure.Repositories;

public class SyncStateRepository : ISyncStateRepository
{
    private readonly ISqliteService _sqlite;

    public SyncStateRepository(ISqliteService sqlite)
    {
        _sqlite = sqlite;
    }

    public async Task UpsertAsync(SyncState state)
    {
        var sql = @"INSERT INTO sync_state (id, company_id, entity_type, last_synced_cursor, last_synced_at, started_at, completed_at, status, records_discovered, records_inserted, records_updated, error_message)
VALUES (@Id, @CompanyId, @EntityType, @LastSyncedCursor, @LastSyncedAt, @StartedAt, @CompletedAt, @Status, @RecordsDiscovered, @RecordsInserted, @RecordsUpdated, @ErrorMessage)
ON CONFLICT(id) DO UPDATE SET last_synced_cursor = excluded.last_synced_cursor, last_synced_at = excluded.last_synced_at, started_at = excluded.started_at, completed_at = excluded.completed_at, status = excluded.status, records_discovered = excluded.records_discovered, records_inserted = excluded.records_inserted, records_updated = excluded.records_updated, error_message = excluded.error_message;";
        await _sqlite.ExecuteAsync(sql, state);
    }

    public async Task<SyncState?> GetByIdAsync(string id)
    {
        var sql = "SELECT id, company_id as CompanyId, entity_type as EntityType, last_synced_cursor as LastSyncedCursor, last_synced_at as LastSyncedAt, started_at AS StartedAt, completed_at AS CompletedAt, status AS Status, records_discovered AS RecordsDiscovered, records_inserted AS RecordsInserted, records_updated AS RecordsUpdated, error_message AS ErrorMessage FROM sync_state WHERE id = @Id LIMIT 1";
        var rows = await _sqlite.QueryAsync<SyncState>(sql, new { Id = id });
        return System.Linq.Enumerable.FirstOrDefault(rows);
    }

    public async Task<IEnumerable<SyncState>> ListByCompanyAsync(string companyId)
    {
        var sql = "SELECT id, company_id as CompanyId, entity_type as EntityType, last_synced_cursor as LastSyncedCursor, last_synced_at as LastSyncedAt, started_at AS StartedAt, completed_at AS CompletedAt, status AS Status, records_discovered AS RecordsDiscovered, records_inserted AS RecordsInserted, records_updated AS RecordsUpdated, error_message AS ErrorMessage FROM sync_state WHERE company_id = @CompanyId";
        return await _sqlite.QueryAsync<SyncState>(sql, new { CompanyId = companyId });
    }
}
