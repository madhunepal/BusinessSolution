using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Authentication;
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
using SmallBusiness.Domain.Entities;
using SmallBusiness.Infrastructure.Data;
using SmallBusiness.Infrastructure.Identity;
using SmallBusiness.Web.Components.Shared;

namespace SmallBusiness.Web.Tests;

public class BusinessSelectorTests
{
    [Fact]
    public async Task CreateButton_ClickForFirstBusiness_CreatesOwnerMembershipAndEstablishesActiveBusiness()
    {
        await using var dbContext = CreateDbContext();
        using var context = CreateComponentContext(
            dbContext,
            "new-user",
            out var userManager,
            out var signInManager,
            out var authenticationState);

        var cut = context.Render<BusinessSelector>(parameters => parameters.AddCascadingValue(authenticationState));

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
                m => m.AddClaimAsync(
                    It.Is<ApplicationUser>(u => u.Id == "new-user"),
                It.Is<Claim>(c => IsBusinessIdClaim(c))),
            Times.Once);
        signInManager.Verify(m => m.RefreshSignInAsync(It.Is<ApplicationUser>(u => u.Id == "new-user")), Times.Once);

        var navigation = context.Services.GetRequiredService<NavigationManager>() as BunitNavigationManager;
        Assert.NotNull(navigation);
        Assert.Single(navigation.History);
        Assert.True(navigation.History.First().Options.ForceLoad);
    }

    [Fact]
    public async Task BusinessSelection_ForExistingUser_UpdatesActiveBusinessClaimAndRefreshesSignIn()
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
            out var authenticationState,
            [new Claim("BusinessId", firstBusiness.Id.ToString())]);

        var cut = context.Render<BusinessSelector>(parameters => parameters.AddCascadingValue(authenticationState));

        cut.WaitForAssertion(() => Assert.Contains("Selected Business", cut.Markup));
        cut.FindAll("button.list-group-item")[1].Click();

        await cut.WaitForAssertionAsync(() =>
        {
            userManager.Verify(
                m => m.RemoveClaimAsync(
                    It.Is<ApplicationUser>(u => u.Id == "existing-user"),
                    It.Is<Claim>(c => c.Type == "BusinessId" && c.Value == firstBusiness.Id.ToString())),
                Times.Once);
            userManager.Verify(
                m => m.AddClaimAsync(
                    It.Is<ApplicationUser>(u => u.Id == "existing-user"),
                    It.Is<Claim>(c => c.Type == "BusinessId" && c.Value == secondBusiness.Id.ToString())),
                Times.Once);
            signInManager.Verify(m => m.RefreshSignInAsync(It.Is<ApplicationUser>(u => u.Id == "existing-user")), Times.Once);
        });
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.CurrentBusinessId).Returns((Guid?)null);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, tenantContext.Object);
    }

    private static BunitContext CreateComponentContext(
        ApplicationDbContext dbContext,
        string userId,
        out Mock<UserManager<ApplicationUser>> userManager,
        out Mock<SignInManager<ApplicationUser>> signInManager,
        out Task<AuthenticationState> authenticationState,
        IReadOnlyCollection<Claim>? existingClaims = null)
    {
        var context = new BunitContext();
        var user = new ApplicationUser { Id = userId, UserName = $"{userId}@example.test", Email = $"{userId}@example.test" };
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        authenticationState = Task.FromResult(new AuthenticationState(principal));

        userManager = CreateUserManager();
        userManager.Setup(m => m.GetUserAsync(principal)).ReturnsAsync(user);
        userManager.Setup(m => m.GetClaimsAsync(user)).ReturnsAsync(existingClaims?.ToList() ?? []);
        userManager.Setup(m => m.RemoveClaimAsync(user, It.IsAny<Claim>())).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.AddClaimAsync(user, It.IsAny<Claim>())).ReturnsAsync(IdentityResult.Success);

        signInManager = CreateSignInManager(userManager.Object);
        signInManager.Setup(m => m.RefreshSignInAsync(user)).Returns(Task.CompletedTask);

        context.Services.AddSingleton<IApplicationDbContext>(dbContext);
        context.Services.AddSingleton<ITenantContext>(new Mock<ITenantContext>().Object);
        context.Services.AddSingleton(userManager.Object);
        context.Services.AddSingleton(signInManager.Object);
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
}
