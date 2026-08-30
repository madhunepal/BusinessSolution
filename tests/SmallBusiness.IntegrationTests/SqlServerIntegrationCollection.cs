namespace SmallBusiness.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class SqlServerIntegrationCollection : ICollectionFixture<SqlServerTestFixture>
{
    public const string Name = "SQL Server integration tests";
}
