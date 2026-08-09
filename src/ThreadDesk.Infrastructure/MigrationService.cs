using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadDesk.Core;

namespace ThreadDesk.Infrastructure;

public interface IMigrationService
{
    Task ApplyMigrationsAsync();
}

public class MigrationService : IMigrationService
{
    private readonly ISqliteService _sqlite;

    public MigrationService(ISqliteService sqlite)
    {
        _sqlite = sqlite;
    }

    public async Task ApplyMigrationsAsync()
    {
        // Ensure DB exists
        await _sqlite.InitializeAsync();

        // Ensure migrations table exists
        var createMigrationsTable = @"CREATE TABLE IF NOT EXISTS migrations_applied (
    id TEXT PRIMARY KEY,
    applied_at TEXT NOT NULL
);";
        await _sqlite.ExecuteAsync(createMigrationsTable);

        // Define migrations in order
        var migrations = new List<(string id, string sql)>
        {
            ("0001_initial", @"BEGIN TRANSACTION;
CREATE TABLE IF NOT EXISTS companies (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    metadata TEXT
);

CREATE TABLE IF NOT EXISTS sync_state (
    id TEXT PRIMARY KEY,
    company_id TEXT NOT NULL,
    entity_type TEXT NOT NULL,
    last_synced_cursor TEXT,
    last_synced_at TEXT
);

CREATE TABLE IF NOT EXISTS outbox (
    id TEXT PRIMARY KEY,
    company_id TEXT NOT NULL,
    payload TEXT NOT NULL,
    created_at TEXT NOT NULL,
    processed_at TEXT
);

COMMIT;"
            ),
            ("0002_ledgers", @"BEGIN TRANSACTION;
CREATE TABLE IF NOT EXISTS ledgers (
    id TEXT PRIMARY KEY,
    company_id TEXT NOT NULL,
    name TEXT NOT NULL,
    group_name TEXT,
    metadata TEXT,
    updated_at TEXT
);
COMMIT;"
            ),
            ("0003_sync_identity_and_status", @"BEGIN TRANSACTION;
ALTER TABLE ledgers ADD COLUMN external_id TEXT;
ALTER TABLE sync_state ADD COLUMN started_at TEXT;
ALTER TABLE sync_state ADD COLUMN completed_at TEXT;
ALTER TABLE sync_state ADD COLUMN status TEXT NOT NULL DEFAULT 'Pending';
ALTER TABLE sync_state ADD COLUMN records_discovered INTEGER NOT NULL DEFAULT 0;
ALTER TABLE sync_state ADD COLUMN records_inserted INTEGER NOT NULL DEFAULT 0;
ALTER TABLE sync_state ADD COLUMN records_updated INTEGER NOT NULL DEFAULT 0;
ALTER TABLE sync_state ADD COLUMN error_message TEXT;
CREATE UNIQUE INDEX IF NOT EXISTS ux_ledgers_company_external_id ON ledgers(company_id, external_id);
COMMIT;"
            )
        };

        foreach (var m in migrations)
        {
            // Check if applied
            var existing = await _sqlite.QueryAsync<string>("SELECT id FROM migrations_applied WHERE id = @Id LIMIT 1", new { Id = m.id });
            if (System.Linq.Enumerable.Any(existing)) continue;

            // Apply migration
            await _sqlite.ExecuteAsync(m.sql);

            // Record applied migration
            await _sqlite.ExecuteAsync("INSERT INTO migrations_applied (id, applied_at) VALUES (@Id, @AppliedAt)", new { Id = m.id, AppliedAt = DateTime.UtcNow.ToString("o") });
        }
    }
}
