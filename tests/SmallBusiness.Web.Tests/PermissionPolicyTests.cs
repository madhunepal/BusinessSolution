using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Infrastructure;
using SmallBusiness.Infrastructure.Identity;

namespace SmallBusiness.Web.Tests;

public class PermissionPolicyTests
{
    private static ServiceProvider BuildProvider(bool hasPermission)
    {
        var permissionService = new Mock<IPermissionService>();
        permissionService
            .Setup(x => x.HasPermissionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasPermission);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(permissionService.Object);
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddPermissionPolicies();

        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal AuthenticatedUser() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test"));

    [Fact]
    public async Task UnauthenticatedUser_IsDenied()
    {
        await using var provider = BuildProvider(hasPermission: true);
        var auth = provider.GetRequiredService<IAuthorizationService>();

        var result = await auth.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, Permissions.InventoryView);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AuthenticatedUserWithoutPermission_IsDenied()
    {
        await using var provider = BuildProvider(hasPermission: false);
        var auth = provider.GetRequiredService<IAuthorizationService>();

        var result = await auth.AuthorizeAsync(AuthenticatedUser(), null, Permissions.InventoryView);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AuthorizedUser_Succeeds()
    {
        await using var provider = BuildProvider(hasPermission: true);
        var auth = provider.GetRequiredService<IAuthorizationService>();

        var result = await auth.AuthorizeAsync(AuthenticatedUser(), null, Permissions.CustomersView);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task PolicyNamesResolveSuccessfully()
    {
        await using var provider = BuildProvider(hasPermission: true);
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        foreach (var permission in Permissions.All)
        {
            var policy = await policyProvider.GetPolicyAsync(permission);
            Assert.NotNull(policy);
            Assert.Contains(policy!.Requirements, r => r is PermissionRequirement requirement && requirement.Permission == permission);
        }
    }

    [Theory]
    [InlineData(Permissions.InventoryView)]
    [InlineData(Permissions.InventoryReceive)]
    [InlineData(Permissions.InventoryAdjust)]
    [InlineData(Permissions.InventoryTransfer)]
    public async Task InventoryPolicies_EvaluatePermissionService(string permission)
    {
        await using var provider = BuildProvider(hasPermission: true);
        var auth = provider.GetRequiredService<IAuthorizationService>();

        var result = await auth.AuthorizeAsync(AuthenticatedUser(), null, permission);

        Assert.True(result.Succeeded);
    }
}
