using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace ThreadDesk.Infrastructure;

public interface ISqliteService : IDisposable
{
    /// <summary>
    /// Ensure directories and database file exist. Call once at startup.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Returns an open IDbConnection (caller should not dispose the underlying connection; use helpers instead).
    /// </summary>
    Task<IDbConnection> GetOpenConnectionAsync();

    Task<int> ExecuteAsync(string sql, object? parameters = null);
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null);
}

public class SqliteService : ISqliteService
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private bool _initialized;

    public SqliteService()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var dir = Path.Combine(programData, "ThreadDesk");
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "threaddesk.db");
        _connectionString = new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();
    }

    public Task InitializeAsync()
    {
        // Ensure the DB file exists by opening a connection which will create the file if missing.
        if (_initialized) return Task.CompletedTask;
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        // No schema applied here — migration runner will be a separate task.
        _initialized = true;
        return Task.CompletedTask;
    }

    public async Task<IDbConnection> GetOpenConnectionAsync()
    {
        var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    public async Task<int> ExecuteAsync(string sql, object? parameters = null)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        return await conn.ExecuteAsync(sql, parameters);
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        return await conn.QueryAsync<T>(sql, parameters);
    }

    public void Dispose()
    {
        // Nothing to dispose at the moment. Keep for interface compatibility.
    }
}
