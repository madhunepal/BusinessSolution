namespace SmallBusiness.IntegrationTests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    private const string ConnectionStringVariable = "SMALLBUSINESS_INTEGRATION_CONNECTION_STRING";

    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringVariable)))
        {
            Skip = $"Set {ConnectionStringVariable} to run SQL Server integration tests.";
        }
    }
}
