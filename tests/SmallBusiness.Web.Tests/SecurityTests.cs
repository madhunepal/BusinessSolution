using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace SmallBusiness.Web.Tests;

public class SecurityTests
{
    [Fact]
    public void ProtectedPages_HaveAuthorizeAttribute_Customers()
    {
        var pageType = typeof(SmallBusiness.Web.Components.Pages.Customers.Index);
        var attributes = pageType.GetCustomAttributes<AuthorizeAttribute>(inherit: true);
        Assert.NotEmpty(attributes);
    }

    [Fact]
    public void ProtectedPages_HaveAuthorizeAttribute_CatalogItems()
    {
        var pageType = typeof(SmallBusiness.Web.Components.Pages.CatalogItems.Index);
        var attributes = pageType.GetCustomAttributes<AuthorizeAttribute>(inherit: true);
        Assert.NotEmpty(attributes);
    }

    [Fact]
    public void ProtectedPages_HaveAuthorizeAttribute_Invoices()
    {
        var pageType = typeof(SmallBusiness.Web.Components.Pages.Invoices.Index);
        var attributes = pageType.GetCustomAttributes<AuthorizeAttribute>(inherit: true);
        Assert.NotEmpty(attributes);
    }

    [Fact]
    public void ProtectedPages_HaveAuthorizeAttribute_Jobs()
    {
        var pageType = typeof(SmallBusiness.Web.Components.Pages.Jobs.Index);
        var attributes = pageType.GetCustomAttributes<AuthorizeAttribute>(inherit: true);
        Assert.NotEmpty(attributes);
    }

    [Fact]
    public void ProtectedPages_HaveAuthorizeAttribute_Dashboard()
    {
        var pageType = typeof(SmallBusiness.Web.Components.Pages.Home);
        var attributes = pageType.GetCustomAttributes<AuthorizeAttribute>(inherit: true);
        Assert.NotEmpty(attributes);
    }

    [Fact]
    public void PublicPages_HaveAllowAnonymousAttribute_Index()
    {
        var pageType = typeof(SmallBusiness.Web.Components.Pages.Public.Index);
        var attributes = pageType.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true);
        Assert.NotEmpty(attributes);
    }

    [Fact]
    public void PublicPages_HaveAllowAnonymousAttribute_Crm()
    {
        var pageType = typeof(SmallBusiness.Web.Components.Pages.Public.Crm);
        var attributes = pageType.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true);
        Assert.NotEmpty(attributes);
    }

    [Fact]
    public void PublicPages_HaveAllowAnonymousAttribute_Finance()
    {
        var pageType = typeof(SmallBusiness.Web.Components.Pages.Public.Finance);
        var attributes = pageType.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true);
        Assert.NotEmpty(attributes);
    }

    [Fact]
    public void PublicPages_DoNotInjectTenantServices()
    {
        // Public pages should not have any fields/properties injected with tenant-scoped services.
        // They use PublicLayout and show static demo data only.
        var publicPageTypes = new[]
        {
            typeof(SmallBusiness.Web.Components.Pages.Public.Index),
            typeof(SmallBusiness.Web.Components.Pages.Public.Crm),
            typeof(SmallBusiness.Web.Components.Pages.Public.Sales),
            typeof(SmallBusiness.Web.Components.Pages.Public.Operations),
            typeof(SmallBusiness.Web.Components.Pages.Public.Scheduling),
            typeof(SmallBusiness.Web.Components.Pages.Public.Finance),
        };

        var tenantServiceNames = new[] { "ITenantContext", "ICustomerService", "ICatalogItemService", 
            "IQuoteService", "ISalesOrderService", "IJobService", "IAppointmentService", "IInvoiceService" };

        foreach (var pageType in publicPageTypes)
        {
            var properties = pageType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var prop in properties)
            {
                Assert.DoesNotContain(prop.PropertyType.Name, tenantServiceNames);
            }
        }
    }
}
