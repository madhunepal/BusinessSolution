using System.Text.RegularExpressions;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SmallBusiness.Application.DTOs.Appointments;
using SmallBusiness.Application.DTOs.Inventory;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Domain.Enums;
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
        Assert.Contains("href=\"/dashboard\"", source);
    }

    [Fact]
    public void NavMenu_BrandLinksToPublicHome()
    {
        var source = ReadComponent("Layout/NavMenu.razor");

        Assert.Contains("class=\"navbar-brand\" href=\"/\"", source);
    }

    [Fact]
    public void NavMenu_DashboardSidebarLinkRemainsPresentWithoutDuplicateDashboardCta()
    {
        var source = ReadComponent("Layout/NavMenu.razor");
        var dashboardLinks = Regex.Matches(source, ">\\s*Dashboard\\s*<", RegexOptions.Multiline);

        Assert.Contains("href=\"/dashboard\"", source);
        Assert.Single(dashboardLinks);
    }

    [Fact]
    public void PublicHome_HidesAuthenticatedBodyDashboardCta()
    {
        var source = ReadComponent("Pages/Public/Index.razor");

        Assert.Contains("ShowAuthorizedActions=\"false\"", source);
    }

    [Fact]
    public void PublicHome_RemainsAnonymousAndStatic()
    {
        var attributes = typeof(SmallBusiness.Web.Components.Pages.Public.Index)
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true);
        var source = ReadComponent("Pages/Public/Index.razor");

        Assert.NotEmpty(attributes);
        Assert.Contains("@page \"/\"", source);
        Assert.Contains("Small Business Management System", source);
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
        Assert.DoesNotContain("href=\"admin/users\"", source);
        Assert.DoesNotContain("href=\"admin/settings\"", source);
    }

    [Fact]
    public void NavMenu_UsesRequestedShellGroups()
    {
        var source = ReadComponent("Layout/NavMenu.razor");

        Assert.Contains("class=\"nav-section-label text-uppercase fw-bold\">Sales</small>", source);
        Assert.Contains("Customers", source);
        Assert.Contains("Catalog", source);
        Assert.Contains("Quotes", source);
        Assert.Contains("Sales Orders", source);
        Assert.Contains("Invoices", source);
        Assert.Contains("class=\"nav-section-label text-uppercase fw-bold\">Operations</small>", source);
        Assert.Contains("Jobs", source);
        Assert.Contains("Schedule", source);
        Assert.Contains("Inventory", source);
        Assert.Contains("class=\"nav-section-label text-uppercase fw-bold\">Business</small>", source);
        Assert.Contains("Switch Business", source);
    }

    [Fact]
    public void NavMenu_SectionHeadingClassHasReadableLightContrast()
    {
        var css = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "SmallBusiness.Web", "Components", "Layout", "NavMenu.razor.css"));

        Assert.Contains(".nav-section-label", css);
        Assert.Contains("rgba(255, 255, 255, 0.92)", css);
    }

    [Fact]
    public void NavMenu_DoesNotDuplicateInventoryLocationsAsTopLevelModule()
    {
        var source = ReadComponent("Layout/NavMenu.razor");

        Assert.DoesNotContain("inventory/locations", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NavMenu_MobileToggleMarkupRemainsUsable()
    {
        var source = ReadComponent("Layout/NavMenu.razor");

        Assert.Contains("class=\"navbar-toggler\"", source);
        Assert.Contains("aria-label=\"Toggle navigation\"", source);
        Assert.Contains("nav-scrollable", source);
    }

    [Fact]
    public void NavMenu_MarksScheduleActiveForAppointmentsRoutes()
    {
        using var context = new BunitContext();
        var navigation = context.Services.GetRequiredService<NavigationManager>() as BunitNavigationManager;
        Assert.NotNull(navigation);
        navigation.NavigateTo("appointments/00000000-0000-0000-0000-000000000001");

        var cut = context.Render<NavMenu>();

        var scheduleLink = cut.Find("a[href=\"schedule\"]");
        Assert.Contains("active", scheduleLink.ClassList);
    }

    [Fact]
    public void NavMenu_MarksInventoryActiveForNestedInventoryRoutes()
    {
        using var context = new BunitContext();
        var navigation = context.Services.GetRequiredService<NavigationManager>() as BunitNavigationManager;
        Assert.NotNull(navigation);
        navigation.NavigateTo("/inventory/locations");

        var cut = context.Render<NavMenu>();

        var inventoryLink = cut.Find("a[href=\"inventory\"]");
        Assert.Contains("active", inventoryLink.ClassList);
    }

    [Fact]
    public void InventoryPage_LoadsSuccessfully()
    {
        using var context = new BunitContext();
        context.Services.AddSingleton<IInventoryService>(new StubInventoryService());

        var cut = context.Render<SmallBusiness.Web.Components.Pages.Inventory.Index>();

        cut.WaitForAssertion(() => Assert.Contains("Inventory", cut.Markup));
    }

    [Fact]
    public void SchedulePage_LoadsSuccessfully()
    {
        using var context = new BunitContext();
        context.Services.AddSingleton<IAppointmentService>(new StubAppointmentService());

        var cut = context.Render<SmallBusiness.Web.Components.Pages.Schedule.Index>();

        cut.WaitForAssertion(() => Assert.Contains("Schedule", cut.Markup));
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

    private sealed class StubInventoryService : IInventoryService
    {
        public Task<List<InventoryProfileDto>> GetInventoryProfilesAsync() => Task.FromResult<List<InventoryProfileDto>>([]);
        public Task<InventoryProfileDto> GetInventoryProfileAsync(Guid id) => throw new NotImplementedException();
        public Task<InventoryProfileDto> CreateInventoryProfileAsync(CreateInventoryProfileDto request) => throw new NotImplementedException();
        public Task<InventoryProfileDto> UpdateInventoryProfileAsync(UpdateInventoryProfileDto request) => throw new NotImplementedException();
        public Task<List<InventoryLocationDto>> GetLocationsAsync() => Task.FromResult<List<InventoryLocationDto>>([]);
        public Task<InventoryLocationDto> CreateLocationAsync(CreateInventoryLocationDto request) => throw new NotImplementedException();
        public Task<InventoryMovementDto> ReceiveStockAsync(StockReceiptDto request) => throw new NotImplementedException();
        public Task<InventoryMovementDto> RecordUsageAsync(StockUsageDto request) => throw new NotImplementedException();
        public Task<InventoryMovementDto> RecordWasteAsync(StockWasteDto request) => throw new NotImplementedException();
        public Task<InventoryMovementDto> AdjustStockAsync(StockAdjustmentDto request) => throw new NotImplementedException();
        public Task<List<InventoryMovementDto>> TransferStockAsync(StockTransferDto request) => throw new NotImplementedException();
        public Task<List<InventoryMovementDto>> GetMovementHistoryAsync(Guid profileId) => Task.FromResult<List<InventoryMovementDto>>([]);
        public Task<List<InventoryStockLevelDto>> GetStockLevelsAsync(Guid profileId) => Task.FromResult<List<InventoryStockLevelDto>>([]);
        public Task<List<InventoryProfileDto>> GetLowStockProfilesAsync() => Task.FromResult<List<InventoryProfileDto>>([]);
        public Task<List<InventoryLotDto>> GetExpiringLotsAsync(int daysToExpiration = 30) => Task.FromResult<List<InventoryLotDto>>([]);
    }

    private sealed class StubAppointmentService : IAppointmentService
    {
        public Task<Guid> CreateAppointmentAsync(CreateAppointmentRequest request, bool ignoreConflicts = false) => throw new NotImplementedException();
        public Task<AppointmentDto> GetAppointmentAsync(Guid id) => throw new NotImplementedException();
        public Task<List<AppointmentDto>> GetAppointmentsAsync(AppointmentSearchRequest request) => Task.FromResult<List<AppointmentDto>>([]);
        public Task UpdateAppointmentTimeAsync(Guid id, UpdateAppointmentTimeRequest request, bool ignoreConflicts = false) => Task.CompletedTask;
        public Task ChangeAppointmentStatusAsync(Guid id, AppointmentStatus status) => Task.CompletedTask;
        public Task AssignTechnicianAsync(Guid appointmentId, string userId, bool ignoreConflicts = false) => Task.CompletedTask;
        public Task RemoveTechnicianAsync(Guid appointmentId, string userId) => Task.CompletedTask;
    }
}
