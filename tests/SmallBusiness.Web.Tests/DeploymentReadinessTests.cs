namespace SmallBusiness.Web.Tests;

public class DeploymentReadinessTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));

    [Fact]
    public void AppSettings_DoesNotCommitDatabaseConnectionStringOrPassword()
    {
        var appSettings = ReadFile("src", "SmallBusiness.Web", "appsettings.json");

        Assert.DoesNotContain("ConnectionStrings", appSettings);
        Assert.DoesNotContain("Password=", appSettings, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("YourStrong@Password1", appSettings);
    }

    [Fact]
    public void ProductionSettings_DoNotContainSecrets()
    {
        var productionSettings = ReadFile("src", "SmallBusiness.Web", "appsettings.Production.json");

        Assert.DoesNotContain("ConnectionStrings", productionSettings);
        Assert.DoesNotContain("Password=", productionSettings, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DockerCompose_UsesEnvironmentVariableForSqlPassword()
    {
        var compose = ReadFile("docker-compose.yml");

        Assert.Contains("MSSQL_SA_PASSWORD: \"${MSSQL_SA_PASSWORD:", compose);
        Assert.DoesNotContain("YourStrong@Password1", compose);
        Assert.Contains("sqlserver-data:/var/opt/mssql", compose);
    }

    [Fact]
    public void GitIgnore_CoversLocalSecretsAndPublishProfiles()
    {
        var gitignore = ReadFile(".gitignore");

        Assert.Contains("appsettings.Development.json", gitignore);
        Assert.Contains("appsettings.*.local.json", gitignore);
        Assert.Contains("*.pubxml", gitignore);
        Assert.Contains("*.publishsettings", gitignore);
        Assert.Contains(".env", gitignore);
        Assert.Contains(".env.*", gitignore);
        Assert.Contains("secrets/", gitignore);
    }

    [Fact]
    public void Program_ConfiguresAzureFriendlyHttpAndHealthEndpoint()
    {
        var program = ReadFile("src", "SmallBusiness.Web", "Program.cs");

        Assert.Contains("builder.Services.AddHealthChecks();", program);
        Assert.Contains("app.MapHealthChecks(\"/health\");", program);
        Assert.Contains("UseForwardedHeaders", program);
        Assert.Contains("ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto", program);
        Assert.Contains("app.UseHsts();", program);
        Assert.Contains("app.UseHttpsRedirection();", program);
    }

    [Fact]
    public void Program_DoesNotRunMigrationsAutomaticallyAtStartup()
    {
        var program = ReadFile("src", "SmallBusiness.Web", "Program.cs");

        Assert.DoesNotContain("Database.Migrate", program);
        Assert.DoesNotContain("MigrateAsync", program);
        Assert.DoesNotContain("EnsureCreated", program);
    }

    [Fact]
    public void ProjectTargetsNet9ForAzureRuntimeSelection()
    {
        var props = ReadFile("Directory.Build.props");

        Assert.Contains("<TargetFramework>net9.0</TargetFramework>", props);
    }

    private static string ReadFile(params string[] pathParts) =>
        File.ReadAllText(Path.Combine([RepositoryRoot, .. pathParts]));
}
