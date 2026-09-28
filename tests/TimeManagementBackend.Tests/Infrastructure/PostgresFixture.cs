using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using TimeManagementBackend.Data;

namespace TimeManagementBackend.Tests.Infrastructure;

/// <summary>
/// Starts one throwaway Postgres for the whole test run and migrates a single template database.
/// Each test then clones that template, which is a file copy inside Postgres rather than a replay
/// of every migration, so tests stay fully isolated without paying the migration cost each time.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string TemplateDb = "tm_template";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("postgres")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private string _maintenanceConnectionString = string.Empty;
    private int _databaseCounter;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _maintenanceConnectionString = _container.GetConnectionString();

        await ExecuteMaintenanceAsync($"""CREATE DATABASE "{TemplateDb}";""");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionStringFor(TemplateDb))
            .Options;

        await using (var db = new AppDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        // CREATE DATABASE ... TEMPLATE is refused while any session is connected to the
        // template, and EF's connection would otherwise linger in the Npgsql pool.
        NpgsqlConnection.ClearAllPools();
    }

    /// <summary>Clones the migrated template into a fresh database and returns its connection string.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"tm_test_{Interlocked.Increment(ref _databaseCounter)}";
        await ExecuteMaintenanceAsync($"""CREATE DATABASE "{name}" TEMPLATE "{TemplateDb}";""");
        return ConnectionStringFor(name);
    }

    private string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(_maintenanceConnectionString)
        {
            Database = database,
            // Without pooling a disposed DbContext closes its physical connection immediately,
            // so a long run cannot creep up on Postgres's max_connections.
            Pooling = false,
        }.ConnectionString;

    private async Task ExecuteMaintenanceAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_maintenanceConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

/// <summary>
/// Groups every database-backed test into one xUnit collection so they share a single container.
/// Sharing a collection also serialises them, which keeps the Serializable transactions in
/// WorkSessionService from contending with each other over an unrelated test's rows.
/// </summary>
[CollectionDefinition(DatabaseCollection.Name)]
public sealed class DatabaseCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
