using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OpenQuiz.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace OpenQuiz.Api.Tests.Infrastructure;

/// <summary>
/// Owns the SQL Server the whole suite talks to and applies the migrations once.
/// A throwaway container is the default so CI needs no setup, but pointing
/// OPENQUIZ_TEST_SQL at an existing server keeps the suite usable on machines
/// where Docker is not available.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string EnvironmentVariable = "OPENQUIZ_TEST_SQL";

    private const string DatabaseName = "OpenQuizTests";
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest";

    private MsSqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable(EnvironmentVariable);

        if (string.IsNullOrWhiteSpace(server))
        {
            _container = new MsSqlBuilder(SqlServerImage).Build();
            await _container.StartAsync();
            server = _container.GetConnectionString();
        }

        ConnectionString = new SqlConnectionStringBuilder(server)
        {
            InitialCatalog = DatabaseName,
            TrustServerCertificate = true,
        }.ConnectionString;

        // A reused server may still hold the previous run's rows, and tests that
        // assert on counts need a known starting point.
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    public OpenQuizDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OpenQuizDbContext>()
            .UseSqlServer(ConnectionString, sql =>
                sql.MigrationsAssembly(typeof(OpenQuizDbContext).Assembly.FullName))
            .Options;

        return new OpenQuizDbContext(options);
    }
}

/// <summary>
/// Every test class joins this collection so the database is built once and the
/// classes never race each other over the shared schema.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "sqlserver";
}
