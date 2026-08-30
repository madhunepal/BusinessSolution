using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moq;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Infrastructure.Data;

namespace SmallBusiness.IntegrationTests;

public sealed class SqlServerTestFixture : IAsyncLifetime
{
    public const string DatabaseName = "SmallBusinessIntegrationTests";
    private const string ConnectionStringVariable = "SMALLBUSINESS_INTEGRATION_CONNECTION_STRING";

    public string ConnectionString { get; private set; } = string.Empty;
    public string SqlServerVersion { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        ConnectionString = ResolveConnectionString();
        await ResetAndMigrateAsync();

        await using var context = CreateContext(MockTenantContext(Guid.NewGuid()).Object);
        SqlServerVersion = await context.Database.SqlQueryRaw<string>("SELECT @@VERSION AS Value").SingleAsync();
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            await DropDatabaseAsync();
        }
    }

    public ApplicationDbContext CreateContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString, sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;

        return new ApplicationDbContext(options, tenantContext);
    }

    public DbContextOptions<ApplicationDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString, sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;
    }

    public async Task ResetAndMigrateAsync()
    {
        await DropDatabaseAsync();
        await using var context = CreateContext(MockTenantContext(Guid.NewGuid()).Object);
        await context.Database.MigrateAsync();
    }

    public static Mock<ITenantContext> MockTenantContext(Guid? businessId, string? userId = "integration-user", bool isAuthenticated = true, bool isCrossTenantAdmin = false)
    {
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(x => x.CurrentBusinessId).Returns(businessId);
        tenantContext.Setup(x => x.UserId).Returns(userId);
        tenantContext.Setup(x => x.IsAuthenticated).Returns(isAuthenticated);
        tenantContext.Setup(x => x.IsCrossTenantAdmin).Returns(isCrossTenantAdmin);
        return tenantContext;
    }

    private static string ResolveConnectionString()
    {
        var value = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Set {ConnectionStringVariable} to a SQL Server connection string whose Initial Catalog/Database is '{DatabaseName}'.");
        }

        var builder = new SqlConnectionStringBuilder(value);
        if (!string.Equals(builder.InitialCatalog, DatabaseName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Integration tests may only run against database '{DatabaseName}'. Current database is '{builder.InitialCatalog}'.");
        }

        return builder.ConnectionString;
    }

    private async Task DropDatabaseAsync()
    {
        var builder = new SqlConnectionStringBuilder(ConnectionString);
        builder.InitialCatalog = "master";

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID(N'{DatabaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{DatabaseName}];
            END
            """;
        await command.ExecuteNonQueryAsync();
    }
}
