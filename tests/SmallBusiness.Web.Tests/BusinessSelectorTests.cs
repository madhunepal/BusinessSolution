using System.Security.Claims;
using System.Runtime.CompilerServices;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Application.Services;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Infrastructure.Data;
using SmallBusiness.Infrastructure.Identity;
using SmallBusiness.Web.Components.Layout;
using SmallBusiness.Web.Components.Shared;

namespace SmallBusiness.Web.Tests;

public class BusinessSelectorTests
{
    private static readonly ConditionalWeakTable<ApplicationDbContext, DbContextOptions<ApplicationDbContext>> ContextOptions = new();
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        ".."));

    [Fact]
    public async Task CreateButton_ClickForFirstBusiness_CreatesOwnerMembershipAndEstablishesActiveBusiness()
    {
        await using var dbContext = CreateDbContext();
        using var context = CreateComponentContext(
            dbContext,
            "new-user",
            out var userManager,
            out var signInManager);

        var cut = context.Render<BusinessSelector>(parameters => parameters
            .Add(p => p.OnboardingMode, true)
            .Add(p => p.SuccessUrl, "/dashboard"));

        cut.WaitForElement("input").Change("First Business");
        cut.Find("button.btn-success").Click();

        await cut.WaitForAssertionAsync(async () =>
        {
            var business = await dbContext.Businesses.IgnoreQueryFilters().SingleAsync();
            var membership = await dbContext.BusinessUsers.IgnoreQueryFilters().SingleAsync();

            Assert.Equal("First Business", business.Name);
            Assert.Equal(business.Id, membership.BusinessId);
            Assert.Equal("new-user", membership.UserId);
            Assert.Equal("Owner", membership.Role);
            Assert.True(membership.IsActive);
        });

        userManager.Verify(
            m => m.AddClaimAsync(It.IsAny<ApplicationUser>(), It.IsAny<Claim>()),
            Times.Never);
        signInManager.Verify(m => m.RefreshSignInAsync(It.IsAny<ApplicationUser>()), Times.Never);

        var navigation = context.Services.GetRequiredService<NavigationManager>() as BunitNavigationManager;
        Assert.NotNull(navigation);
        Assert.Single(navigation.History);
        Assert.Contains("/Account/SelectBusiness?", navigation.History.First().Uri);
        Assert.Contains("returnUrl=%2Fdashboard", navigation.History.First().Uri);
        Assert.True(navigation.History.First().Options.ForceLoad);
    }

    [Fact]
    public async Task CreateButton_DoubleClick_CreatesOnlyOneBusiness()
    {
        await using var dbContext = CreateDbContext();
        using var context = CreateComponentContext(
            dbContext,
            "new-user",
            out _,
            out _);

        var cut = context.Render<BusinessSelector>(parameters => parameters
            .Add(p => p.OnboardingMode, true)
            .Add(p => p.SuccessUrl, "/dashboard"));

        cut.WaitForElement("input").Change("First Business");
        cut.Find("button.btn-success").Click();
        cut.Find("button.btn-success").Click();

        await cut.WaitForAssertionAsync(async () =>
        {
            Assert.Equal(1, await dbContext.Businesses.IgnoreQueryFilters().CountAsync());
            Assert.Equal(1, await dbContext.BusinessUsers.IgnoreQueryFilters().CountAsync());
        });
    }

    [Fact]
    public void CreateButton_WhenUserCannotBeResolved_DisplaysVisibleError()
    {
        using var context = CreateComponentContextWithPrincipal(
            new ClaimsPrincipal(new ClaimsIdentity()),
            out _,
            out _);

        var cut = context.Render<BusinessSelector>(parameters => parameters.Add(p => p.OnboardingMode, true));

        cut.WaitForElement("input").Change("First Business");
        cut.Find("button.btn-success").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Please sign in before creating a business.", cut.Markup));
    }

    [Fact]
    public async Task BusinessSelection_ForExistingUser_NavigatesToSecureSelectionEndpoint()
    {
        await using var dbContext = CreateDbContext();
        var firstBusiness = new Business { Id = Guid.NewGuid(), Name = "Existing Business" };
        var secondBusiness = new Business { Id = Guid.NewGuid(), Name = "Selected Business" };

        dbContext.Businesses.AddRange(firstBusiness, secondBusiness);
        dbContext.BusinessUsers.AddRange(
            new BusinessUser { BusinessId = firstBusiness.Id, UserId = "existing-user", Role = "Owner" },
            new BusinessUser { BusinessId = secondBusiness.Id, UserId = "existing-user", Role = "Owner" });
        await dbContext.SaveChangesAsync();

        using var context = CreateComponentContext(
            dbContext,
            "existing-user",
            out var userManager,
            out var signInManager,
            [new Claim("BusinessId", firstBusiness.Id.ToString())]);

        var cut = context.Render<BusinessSelector>();

        cut.WaitForAssertion(() => Assert.Contains("Selected Business", cut.Markup));
        cut.FindAll("button.list-group-item")[1].Click();

        var navigation = context.Services.GetRequiredService<NavigationManager>() as BunitNavigationManager;
        Assert.NotNull(navigation);
        Assert.Single(navigation.History);
        Assert.Contains("/Account/SelectBusiness?", navigation.History.First().Uri);
        Assert.Contains($"businessId={secondBusiness.Id}", navigation.History.First().Uri);
        Assert.True(navigation.History.First().Options.ForceLoad);

        userManager.Verify(m => m.RemoveClaimAsync(It.IsAny<ApplicationUser>(), It.IsAny<Claim>()), Times.Never);
        userManager.Verify(m => m.AddClaimAsync(It.IsAny<ApplicationUser>(), It.IsAny<Claim>()), Times.Never);
        signInManager.Verify(m => m.RefreshSignInAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public void MainLayout_ForAuthenticatedUserWithoutBusiness_RedirectsToOnboarding()
    {
        using var context = CreateLayoutContext(isAuthenticated: true, currentBusinessId: null);

        context.Render<MainLayout>(parameters => parameters.Add(p => p.Body, _ => { }));

        var navigation = context.Services.GetRequiredService<NavigationManager>() as BunitNavigationManager;
        Assert.NotNull(navigation);
        Assert.Single(navigation.History);
        Assert.EndsWith("/onboarding/business", navigation.History.First().Uri);
    }

    [Fact]
    public void MainLayout_ForTenantUser_RendersApplicationNavigation()
    {
        var businessId = Guid.NewGuid();
        using var context = CreateLayoutContext(isAuthenticated: true, currentBusinessId: businessId, out var dbContext);
        dbContext.Businesses.Add(new Business { Id = businessId, Name = "Acme Services" });
        dbContext.SaveChanges();

        var cut = context.Render<MainLayout>(parameters => parameters.Add(p => p.Body, _ => { }));

        Assert.Contains("Dashboard", cut.Markup);
        Assert.Contains("Acme Services", cut.Markup);
        Assert.DoesNotContain(businessId.ToString(), cut.Markup);
        Assert.Contains("Switch Business", cut.Markup);
    }

    [Fact]
    public async Task PublicLayout_ForAnonymousUser_ShowsLoginAndGetStarted()
    {
        await using var dbContext = CreateDbContext();
        using var context = CreatePublicLayoutContext(isAuthenticated: false, dbContext, null);

        var cut = context.Render<PublicLayout>(parameters => parameters.Add(p => p.Body, _ => { }));

        Assert.Contains("Log in", cut.Markup);
        Assert.Contains("Get Started", cut.Markup);
    }

    [Fact]
    public async Task PublicLayout_PublicMobileMenuInitiallyClosed()
    {
        await using var dbContext = CreateDbContext();
        using var context = CreatePublicLayoutContext(isAuthenticated: false, dbContext, null);

        var cut = context.Render<PublicLayout>(parameters => parameters.Add(p => p.Body, _ => { }));

        Assert.Equal("false", cut.Find(".navbar-toggler").GetAttribute("aria-expanded"));
        Assert.Equal("collapse navbar-collapse", cut.Find("#publicNavbar").GetAttribute("class"));
    }

    [Fact]
    public async Task PublicLayout_HamburgerOpensAndClosesPublicMobileMenu()
    {
        await using var dbContext = CreateDbContext();
        using var context = CreatePublicLayoutContext(isAuthenticated: false, dbContext, null);
        var cut = context.Render<PublicLayout>(parameters => parameters.Add(p => p.Body, _ => { }));

        cut.Find(".navbar-toggler").Click();

        Assert.Equal("true", cut.Find(".navbar-toggler").GetAttribute("aria-expanded"));
        Assert.Equal("collapse navbar-collapse show", cut.Find("#publicNavbar").GetAttribute("class"));

        cut.Find(".navbar-toggler").Click();

        Assert.Equal("false", cut.Find(".navbar-toggler").GetAttribute("aria-expanded"));
        Assert.Equal("collapse navbar-collapse", cut.Find("#publicNavbar").GetAttribute("class"));
    }

    [Fact]
    public async Task PublicLayout_ClickingPublicNavItemClosesMobileMenu()
    {
        await using var dbContext = CreateDbContext();
        using var context = CreatePublicLayoutContext(isAuthenticated: false, dbContext, null);
        var cut = context.Render<PublicLayout>(parameters => parameters.Add(p => p.Body, _ => { }));

        cut.Find(".navbar-toggler").Click();
        cut.Find("a[href=\"/crm\"]").Click();

        Assert.Equal("false", cut.Find(".navbar-toggler").GetAttribute("aria-expanded"));
        Assert.Equal("collapse navbar-collapse", cut.Find("#publicNavbar").GetAttribute("class"));
    }

    [Fact]
    public void PublicLayout_DoesNotDependOnBootstrapCollapseJavaScript()
    {
        var source = ReadComponent("Layout/PublicLayout.razor");

        Assert.DoesNotContain("data-bs-toggle=\"collapse\"", source);
        Assert.DoesNotContain("data-bs-toggle=\"dropdown\"", source);
        Assert.DoesNotContain("data-bs-target=\"#publicNavbar\"", source);
        Assert.Contains("@onclick=\"TogglePublicMenu\"", source);
        Assert.Contains("aria-controls=\"publicNavbar\"", source);
    }

    [Fact]
    public async Task PublicLayout_ForTenantUser_ShowsBusinessMenuWithoutAnonymousActions()
    {
        await using var dbContext = CreateDbContext();
        var business = new Business { Id = Guid.NewGuid(), Name = "Acme Services" };
        dbContext.Businesses.Add(business);
        await dbContext.SaveChangesAsync();
        using var context = CreatePublicLayoutContext(isAuthenticated: true, dbContext, business.Id);

        var cut = context.Render<PublicLayout>(parameters => parameters.Add(p => p.Body, _ => { }));

        cut.WaitForAssertion(() => Assert.Contains("Acme Services", cut.Markup));
        Assert.Single(cut.FindAll("a[href=\"/dashboard\"]"));
        Assert.Contains("Switch Business", cut.Markup);
        Assert.Contains("Account/Profile", cut.Markup);
        Assert.Single(cut.FindAll("a[href=\"/Account/Logout?returnUrl=%2F\"]"));
        Assert.DoesNotContain("Get Started", cut.Markup);
        Assert.DoesNotContain(">Log in<", cut.Markup);
    }

    [Fact]
    public async Task PublicLayout_AuthenticatedAccountDropdownOpensAndClosesWithBlazorState()
    {
        await using var dbContext = CreateDbContext();
        var business = new Business { Id = Guid.NewGuid(), Name = "Acme Services" };
        dbContext.Businesses.Add(business);
        await dbContext.SaveChangesAsync();
        using var context = CreatePublicLayoutContext(isAuthenticated: true, dbContext, business.Id);
        var cut = context.Render<PublicLayout>(parameters => parameters.Add(p => p.Body, _ => { }));

        cut.WaitForAssertion(() => Assert.Contains("Acme Services", cut.Markup));
        var accountButton = cut.Find("button[aria-controls=\"publicAccountMenu\"]");

        Assert.Equal("false", accountButton.GetAttribute("aria-expanded"));
        Assert.DoesNotContain("show", cut.Find("#publicAccountMenu").ClassList);

        accountButton.Click();

        Assert.Equal("true", cut.Find("button[aria-controls=\"publicAccountMenu\"]").GetAttribute("aria-expanded"));
        Assert.Contains("show", cut.Find("#publicAccountMenu").ClassList);

        cut.Find("a[href=\"/Account/Manage\"]").Click();

        Assert.Equal("false", cut.Find("button[aria-controls=\"publicAccountMenu\"]").GetAttribute("aria-expanded"));
        Assert.DoesNotContain("show", cut.Find("#publicAccountMenu").ClassList);
    }

    [Fact]
    public async Task PublicHome_ForAuthenticatedTenantUser_RendersAuthenticatedHeaderBehavior()
    {
        await using var dbContext = CreateDbContext();
        var business = new Business { Id = Guid.NewGuid(), Name = "Acme Services" };
        dbContext.Businesses.Add(business);
        await dbContext.SaveChangesAsync();
        using var context = CreatePublicLayoutContext(isAuthenticated: true, dbContext, business.Id);

        var cut = context.Render<PublicLayout>(parameters => parameters.Add(p => p.Body, builder =>
        {
            builder.OpenComponent<SmallBusiness.Web.Components.Pages.Public.Index>(0);
            builder.CloseComponent();
        }));

        cut.WaitForAssertion(() => Assert.Contains("Acme Services", cut.Markup));
        Assert.Contains("Small Business Management System", cut.Markup);
        Assert.Single(cut.FindAll("a[href=\"/dashboard\"]"));
        Assert.Contains("Account/Profile", cut.Markup);
        Assert.Single(cut.FindAll("a[href=\"/Account/Logout?returnUrl=%2F\"]"));
        Assert.DoesNotContain("Get Started", cut.Markup);
        Assert.DoesNotContain(">Log in<", cut.Markup);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        return CreateDbContext(out _);
    }

    private static ApplicationDbContext CreateDbContext(out DbContextOptions<ApplicationDbContext> options)
    {
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.CurrentBusinessId).Returns((Guid?)null);

        return CreateDbContext(tenantContext.Object, out options);
    }

    private static ApplicationDbContext CreateDbContext(
        ITenantContext tenantContext,
        out DbContextOptions<ApplicationDbContext> options)
    {

        options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options, tenantContext);
        ContextOptions.Add(context, options);
        return context;
    }

    private static BunitContext CreateComponentContext(
        ApplicationDbContext dbContext,
        string userId,
        out Mock<UserManager<ApplicationUser>> userManager,
        out Mock<SignInManager<ApplicationUser>> signInManager,
        IReadOnlyCollection<Claim>? existingClaims = null)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var context = CreateComponentContextWithPrincipal(principal, out userManager, out signInManager, dbContext, existingClaims);

        return context;
    }

    private static BunitContext CreateComponentContextWithPrincipal(
        ClaimsPrincipal principal,
        out Mock<UserManager<ApplicationUser>> userManager,
        out Mock<SignInManager<ApplicationUser>> signInManager,
        ApplicationDbContext? dbContext = null,
        IReadOnlyCollection<Claim>? existingClaims = null)
    {
        var context = new BunitContext();
        DbContextOptions<ApplicationDbContext> dbOptions;
        if (dbContext is null)
        {
            dbContext = CreateDbContext(out dbOptions);
        }
        else
        {
            dbOptions = GetOptions(dbContext);
        }
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = string.IsNullOrWhiteSpace(userId)
            ? null
            : new ApplicationUser { Id = userId, UserName = $"{userId}@example.test", Email = $"{userId}@example.test" };

        userManager = CreateUserManager();
        userManager.Setup(m => m.GetUserAsync(principal)).ReturnsAsync(user);
        if (user is not null)
        {
            userManager.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
            userManager.Setup(m => m.GetClaimsAsync(user)).ReturnsAsync(existingClaims?.ToList() ?? []);
            userManager.Setup(m => m.RemoveClaimAsync(user, It.IsAny<Claim>())).ReturnsAsync(IdentityResult.Success);
            userManager.Setup(m => m.AddClaimAsync(user, It.IsAny<Claim>())).ReturnsAsync(IdentityResult.Success);
        }

        signInManager = CreateSignInManager(userManager.Object);
        if (user is not null)
        {
            signInManager.Setup(m => m.RefreshSignInAsync(user)).Returns(Task.CompletedTask);
        }

        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.CurrentBusinessId).Returns((Guid?)null);
        tenantContext.Setup(t => t.UserId).Returns(userId);

        context.Services.AddSingleton<IApplicationDbContext>(dbContext);
        context.Services.AddSingleton<IApplicationDbContextFactory>(new TestApplicationDbContextFactory(dbOptions, tenantContext.Object));
        context.Services.AddSingleton<ITenantContext>(tenantContext.Object);
        context.Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(principal));
        context.Services.AddSingleton(new BusinessService(new TestApplicationDbContextFactory(dbOptions, tenantContext.Object), tenantContext.Object));
        context.Services.AddSingleton(userManager.Object);
        context.Services.AddSingleton(signInManager.Object);
        context.Services.AddLogging();
        return context;
    }

    private static BunitContext CreateLayoutContext(bool isAuthenticated, Guid? currentBusinessId)
    {
        return CreateLayoutContext(isAuthenticated, currentBusinessId, out _);
    }

    private static BunitContext CreateLayoutContext(
        bool isAuthenticated,
        Guid? currentBusinessId,
        out ApplicationDbContext dbContext)
    {
        var context = new BunitContext();
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.CurrentBusinessId).Returns(currentBusinessId);
        dbContext = CreateDbContext(tenantContext.Object, out var options);

        var identity = isAuthenticated
            ? new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-id"), new Claim(ClaimTypes.Name, "user@example.test")], "TestAuth")
            : new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);

        context.Services.AddSingleton<ITenantContext>(tenantContext.Object);
        var authorization = context.AddAuthorization();
        if (isAuthenticated)
        {
            authorization.SetAuthorized("user@example.test");
            authorization.SetClaims(principal.Claims.ToArray());
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

    private static BunitContext CreatePublicLayoutContext(
        bool isAuthenticated,
        ApplicationDbContext dbContext,
        Guid? currentBusinessId)
    {
        var context = new BunitContext();
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.CurrentBusinessId).Returns(currentBusinessId);
        var options = GetOptions(dbContext);

        var identity = isAuthenticated
            ? new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-id"), new Claim(ClaimTypes.Name, "user@example.test")], "TestAuth")
            : new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);

        context.Services.AddSingleton<ITenantContext>(tenantContext.Object);
        var authorization = context.AddAuthorization();
        if (isAuthenticated)
        {
            authorization.SetAuthorized("user@example.test");
            authorization.SetClaims(principal.Claims.ToArray());
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

    private static Mock<UserManager<ApplicationUser>> CreateUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();

        return new Mock<UserManager<ApplicationUser>>(
            store.Object,
            Options.Create(new IdentityOptions()),
            new Mock<IPasswordHasher<ApplicationUser>>().Object,
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new Mock<ILookupNormalizer>().Object,
            new IdentityErrorDescriber(),
            new Mock<IServiceProvider>().Object,
            new Mock<ILogger<UserManager<ApplicationUser>>>().Object);
    }

    private static Mock<SignInManager<ApplicationUser>> CreateSignInManager(UserManager<ApplicationUser> userManager)
    {
        return new Mock<SignInManager<ApplicationUser>>(
            userManager,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            Options.Create(new IdentityOptions()),
            new Mock<ILogger<SignInManager<ApplicationUser>>>().Object,
            new Mock<IAuthenticationSchemeProvider>().Object,
            new Mock<IUserConfirmation<ApplicationUser>>().Object);
    }

    private static bool IsBusinessIdClaim(Claim claim) =>
        claim.Type == "BusinessId" && Guid.TryParse(claim.Value, out _);

    private static DbContextOptions<ApplicationDbContext> GetOptions(ApplicationDbContext dbContext)
    {
        if (!ContextOptions.TryGetValue(dbContext, out var options))
        {
            throw new InvalidOperationException("Test DbContext options were not captured.");
        }

        return options;
    }

    private static string ReadComponent(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "SmallBusiness.Web", "Components", relativePath));

    private sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly AuthenticationState _authenticationState;

        public TestAuthenticationStateProvider(ClaimsPrincipal principal)
        {
            _authenticationState = new AuthenticationState(principal);
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(_authenticationState);
    }
}
