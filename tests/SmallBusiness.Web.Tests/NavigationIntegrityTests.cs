using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using SmallBusiness.Infrastructure.Identity;
using SmallBusiness.Web.Components.Layout;

namespace SmallBusiness.Web.Tests;

public class NavigationIntegrityTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));

    [Theory]
    [InlineData("/customers", "Customers")]
    [InlineData("/catalog", "Catalog")]
    [InlineData("/quotes", "Quotes")]
    [InlineData("/sales-orders", "Sales Orders")]
    [InlineData("/jobs", "Jobs")]
    [InlineData("/schedule", "Schedule")]
    [InlineData("/invoices", "Invoices")]
    [InlineData("/inventory", "Inventory")]
    public void Dashboard_LinksToImplementedModuleRoutes(string route, string label)
    {
        var source = ReadComponent("Pages/Home.razor");

        Assert.Contains($"href=\"{route}\"", source);
        Assert.Contains(label, source);
    }

    [Theory]
    [InlineData("customers")]
    [InlineData("catalog")]
    [InlineData("quotes")]
    [InlineData("sales-orders")]
    [InlineData("jobs")]
    [InlineData("schedule")]
    [InlineData("invoices")]
    [InlineData("inventory")]
    public void NavMenu_LinksToImplementedModuleRoutes(string route)
    {
        var source = ReadComponent("Layout/NavMenu.razor");

        Assert.Contains($"href=\"{route}\"", source);
    }

    [Fact]
    public void NavMenu_DashboardDoesNotPointToPublicHome()
    {
        var source = ReadComponent("Layout/NavMenu.razor");

        Assert.DoesNotContain("href=\"\" Match=\"NavLinkMatch.All\"", source);
        Assert.Contains("href=\"dashboard\"", source);
    }

    [Fact]
    public void NavMenu_DoesNotLinkObsoleteAuthenticatedPlaceholders()
    {
        var source = ReadComponent("Layout/NavMenu.razor");

        Assert.DoesNotContain("href=\"leads\"", source);
        Assert.DoesNotContain("href=\"products\"", source);
        Assert.DoesNotContain("href=\"orders\"", source);
        Assert.DoesNotContain("href=\"payments\"", source);
        Assert.DoesNotContain("href=\"expenses\"", source);
        Assert.DoesNotContain("href=\"employees\"", source);
        Assert.DoesNotContain("href=\"purchasing\"", source);
        Assert.DoesNotContain("href=\"reports\"", source);
    }

    [Fact]
    public void PublicSales_RemainsAnonymousAndStatic()
    {
        var attributes = typeof(SmallBusiness.Web.Components.Pages.Public.Sales)
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true);
        var source = ReadComponent("Pages/Public/Sales.razor");

        Assert.NotEmpty(attributes);
        Assert.Contains("DEMO DATA", source);
        Assert.Contains("PublicCta", source);
    }

    [Fact]
    public void PublicFinance_RemainsAnonymousAndStatic()
    {
        var attributes = typeof(SmallBusiness.Web.Components.Pages.Public.Finance)
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true);
        var source = ReadComponent("Pages/Public/Finance.razor");

        Assert.NotEmpty(attributes);
        Assert.Contains("DEMO DATA", source);
        Assert.Contains("PublicCta", source);
    }

    [Fact]
    public void PublicCta_AuthenticatedSalesAndFinanceDoNotUsePublicDemoTargets()
    {
        var sales = ReadComponent("Pages/Public/Sales.razor");
        var finance = ReadComponent("Pages/Public/Finance.razor");

        Assert.Contains("PrimaryText=\"Open Sales\"", sales);
        Assert.Contains("PrimaryUrl=\"/quotes\"", sales);
        Assert.Contains("PrimaryText=\"Open Finance\"", finance);
        Assert.Contains("PrimaryUrl=\"/invoices\"", finance);
    }

    [Fact]
    public void CriticalPageRoutes_AreUnique()
    {
        var routes = Directory.GetFiles(Path.Combine(RepositoryRoot, "src", "SmallBusiness.Web", "Components"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), "^@page\\s+\"([^\"]+)\"", RegexOptions.Multiline)
                .Select(match => (Route: match.Groups[1].Value, File: file)))
            .ToList();

        var duplicates = routes
            .GroupBy(route => route.Route, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.DoesNotContain("/dashboard", duplicates);
        Assert.DoesNotContain("/customers", duplicates);
        Assert.DoesNotContain("/catalog", duplicates);
        Assert.DoesNotContain("/quotes", duplicates);
        Assert.DoesNotContain("/sales-orders", duplicates);
        Assert.DoesNotContain("/jobs", duplicates);
        Assert.DoesNotContain("/schedule", duplicates);
        Assert.DoesNotContain("/invoices", duplicates);
        Assert.DoesNotContain("/inventory", duplicates);
    }

    [Fact]
    public void OwnerRole_IncludesImplementedModulePermissions()
    {
        var ownerPermissions = AppRoles.DefaultRolePermissions[AppRoles.Owner];

        Assert.Contains(Permissions.CustomersView, ownerPermissions);
        Assert.Contains(Permissions.InventoryManage, ownerPermissions);
        Assert.Contains(Permissions.QuotesView, ownerPermissions);
        Assert.Contains(Permissions.OrdersView, ownerPermissions);
        Assert.Contains(Permissions.InvoicesView, ownerPermissions);
        Assert.Contains(Permissions.JobsView, ownerPermissions);
        Assert.Contains(Permissions.ScheduleView, ownerPermissions);
    }

    private static string ReadComponent(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "SmallBusiness.Web", "Components", relativePath));
}
