using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Application.Services;
using SmallBusiness.Infrastructure.Data;
using SmallBusiness.Web.Components.Layout;

namespace SmallBusiness.Web.Tests;

public class LogoutFlowTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));

    [Fact]
    public void AuthenticatedLayoutsExposeLogoutAsFreshHttpNavigation()
    {
        var mainLayout = ReadComponent("Layout/MainLayout.razor");
        var publicLayout = ReadComponent("Layout/PublicLayout.razor");
        var onboardingLayout = ReadComponent("Layout/OnboardingLayout.razor");

        Assert.Contains("href=\"/Account/Logout?returnUrl=%2F\"", mainLayout);
        Assert.Contains("href=\"/Account/Logout?returnUrl=%2F\"", publicLayout);
        Assert.Contains("href=\"/Account/Logout?returnUrl=%2F\"", onboardingLayout);
        Assert.Contains("data-enhance-nav=\"false\"", mainLayout);
        Assert.Contains("data-enhance-nav=\"false\"", publicLayout);
        Assert.Contains("data-enhance-nav=\"false\"", onboardingLayout);
        Assert.DoesNotContain("form method=\"post\" action=\"/Account/Logout\"", mainLayout);
        Assert.DoesNotContain("form method=\"post\" action=\"/Account/Logout\"", publicLayout);
        Assert.DoesNotContain("form method=\"post\" action=\"/Account/Logout\"", onboardingLayout);
    }

    [Fact]
    public void LogoutGetEndpointRendersFreshAntiforgeryPostForm()
    {
        var endpoints = ReadComponent("Account/IdentityComponentsEndpointRouteBuilderExtensions.cs");

        Assert.Contains("accountGroup.MapGet(\"/Logout\"", endpoints);
        Assert.Contains("antiforgery.GetAndStoreTokens(context)", endpoints);
        Assert.Contains("<form method=\"post\" action=\"/Account/Logout?returnUrl={{encodedReturnUrl}}\" data-enhance=\"false\">", endpoints);
        Assert.Contains("<input name=\"{{fieldName}}\" type=\"hidden\" value=\"{{requestToken}}\" />", endpoints);
    }

    [Fact]
    public void LogoutGetDoesNotPerformSignOutSideEffect()
    {
        var endpoints = ReadComponent("Account/IdentityComponentsEndpointRouteBuilderExtensions.cs");
        var getStart = endpoints.IndexOf("accountGroup.MapGet(\"/Logout\"", StringComparison.Ordinal);
        var postStart = endpoints.IndexOf("accountGroup.MapPost(\"/Logout\"", StringComparison.Ordinal);
        Assert.True(getStart >= 0);
        Assert.True(postStart > getStart);
        var getEndpoint = endpoints[getStart..postStart];

        Assert.DoesNotContain("SignOutAsync", getEndpoint);
    }

    [Fact]
    public void LogoutPostSignsOutWithAuthorizationAndAntiforgeryStillEnabled()
    {
        var endpoints = ReadComponent("Account/IdentityComponentsEndpointRouteBuilderExtensions.cs");
        var program = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "SmallBusiness.Web", "Program.cs"));
        var postStart = endpoints.IndexOf("accountGroup.MapPost(\"/Logout\"", StringComparison.Ordinal);
        var selectBusinessStart = endpoints.IndexOf("accountGroup.MapGet(\"/SelectBusiness\"", StringComparison.Ordinal);
        Assert.True(postStart >= 0);
        Assert.True(selectBusinessStart > postStart);
        var logoutPostEndpoint = endpoints[postStart..selectBusinessStart];

        Assert.Contains("accountGroup.MapPost(\"/Logout\"", logoutPostEndpoint);
        Assert.Contains("[FromQuery] string? returnUrl", logoutPostEndpoint);
        Assert.DoesNotContain("[FromForm] string returnUrl", logoutPostEndpoint);
        Assert.Contains("antiforgery.ValidateRequestAsync(context)", logoutPostEndpoint);
        Assert.Contains("signInManager.SignOutAsync()", logoutPostEndpoint);
        Assert.Contains("}).RequireAuthorization();", logoutPostEndpoint);
        Assert.Contains("app.UseAntiforgery();", program);
    }

    [Fact]
    public void LogoutFlowRemainsCompatibleWithBusinessClaimRefreshEndpoints()
    {
        var endpoints = ReadComponent("Account/IdentityComponentsEndpointRouteBuilderExtensions.cs");
        var mainLayout = ReadComponent("Layout/MainLayout.razor");
        var publicLayout = ReadComponent("Layout/PublicLayout.razor");

        Assert.Contains("MapGet(\"/SelectBusiness\"", endpoints);
        Assert.Contains("AddClaimAsync(user, new Claim(\"BusinessId\"", endpoints);
        Assert.Contains("RefreshSignInAsync(user)", endpoints);
        Assert.Contains("href=\"/Account/Logout?returnUrl=%2F\"", mainLayout);
        Assert.Contains("href=\"/Account/Logout?returnUrl=%2F\"", publicLayout);
    }

    [Fact]
    public void PublicHomeAfterClearedAuthenticationRendersAnonymousControls()
    {
        using var context = CreatePublicLayoutContext(isAuthenticated: false);

        var cut = context.Render<PublicLayout>(parameters => parameters.Add(p => p.Body, builder =>
        {
            builder.OpenComponent<SmallBusiness.Web.Components.Pages.Public.Index>(0);
            builder.CloseComponent();
        }));

        Assert.Contains("Log in", cut.Markup);
        Assert.Contains("Get Started", cut.Markup);
        Assert.DoesNotContain("Log out", cut.Markup);
        Assert.DoesNotContain("Switch Business", cut.Markup);
    }

    private static BunitContext CreatePublicLayoutContext(bool isAuthenticated)
    {
        var context = new BunitContext();
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.CurrentBusinessId).Returns((Guid?)null);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var dbContext = new ApplicationDbContext(options, tenantContext.Object);

        context.Services.AddSingleton<ITenantContext>(tenantContext.Object);
        var authorization = context.AddAuthorization();
        if (isAuthenticated)
        {
            authorization.SetAuthorized("user@example.test");
        }
        else
        {
            authorization.SetNotAuthorized();
        }

        context.Services.AddSingleton<IApplicationDbContext>(dbContext);
        context.Services.AddSingleton<IApplicationDbContextFactory>(new TestApplicationDbContextFactory(options, tenantContext.Object));
        context.Services.AddSingleton(new BusinessService(new TestApplicationDbContextFactory(options, tenantContext.Object), tenantContext.Object));
        return context;
    }

    private static string ReadComponent(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "SmallBusiness.Web", "Components", relativePath));
}
