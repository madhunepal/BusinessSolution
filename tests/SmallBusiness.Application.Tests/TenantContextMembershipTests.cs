using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Infrastructure.Data;
using SmallBusiness.Infrastructure.Services;

namespace SmallBusiness.Application.Tests;

public class TenantContextMembershipTests
{
    private static ClaimsPrincipal User(string userId, Guid? businessId = null, bool isSysAdmin = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, $"{userId}@example.com")
        };

        if (businessId.HasValue)
        {
            claims.Add(new Claim("BusinessId", businessId.Value.ToString()));
        }

        if (isSysAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "SysAdmin"));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static DbContextOptions<ApplicationDbContext> Options() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static TenantContextService CreateTenantContext(
        DbContextOptions<ApplicationDbContext> options,
        ClaimsPrincipal principal)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return new TenantContextService(accessor, options);
    }

    [Fact]
    public async Task ValidMembershipAndClaim_ReturnsEffectiveTenant()
    {
        var options = Options();
        var businessId = Guid.NewGuid();
        await using (var context = new ApplicationDbContext(options))
        {
            context.BusinessUsers.Add(new BusinessUser
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                UserId = "user-1",
                Role = "Owner",
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        var tenantContext = CreateTenantContext(options, User("user-1", businessId));

        Assert.Equal(businessId, tenantContext.CurrentBusinessId);
    }

    [Fact]
    public async Task RevokedMembership_ReturnsNoTenant()
    {
        var options = Options();
        var businessId = Guid.NewGuid();
        await using (var context = new ApplicationDbContext(options))
        {
            context.BusinessUsers.Add(new BusinessUser
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                UserId = "user-1",
                Role = "Owner",
                IsActive = false
            });
            await context.SaveChangesAsync();
        }

        var tenantContext = CreateTenantContext(options, User("user-1", businessId));

        Assert.Null(tenantContext.CurrentBusinessId);
    }

    [Fact]
    public async Task ClaimForAnotherUserMembership_ReturnsNoTenant()
    {
        var options = Options();
        var businessId = Guid.NewGuid();
        await using (var context = new ApplicationDbContext(options))
        {
            context.BusinessUsers.Add(new BusinessUser
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                UserId = "other-user",
                Role = "Owner",
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        var tenantContext = CreateTenantContext(options, User("user-1", businessId));

        Assert.Null(tenantContext.CurrentBusinessId);
    }

    [Fact]
    public void MissingClaim_ReturnsNoTenant()
    {
        var tenantContext = CreateTenantContext(Options(), User("user-1"));

        Assert.Null(tenantContext.CurrentBusinessId);
    }

    [Fact]
    public void SysAdminBypass_ReturnsClaimedTenantWithoutMembership()
    {
        var businessId = Guid.NewGuid();
        var tenantContext = CreateTenantContext(Options(), User("admin-1", businessId, isSysAdmin: true));

        Assert.True(tenantContext.IsCrossTenantAdmin);
        Assert.Equal(businessId, tenantContext.CurrentBusinessId);
    }

    [Fact]
    public async Task RevokedUserCannotAccessTenantOwnedDataThroughGlobalFilters()
    {
        var options = Options();
        var businessId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.BusinessUsers.Add(new BusinessUser
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                UserId = "user-1",
                Role = "Owner",
                IsActive = false
            });
            setup.Customers.Add(new Customer
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                CustomerNumber = "CUST-1",
                Name = "Hidden Customer"
            });
            await setup.SaveChangesAsync();
        }

        var tenantContext = CreateTenantContext(options, User("user-1", businessId));
        await using var filtered = new ApplicationDbContext(options, tenantContext);

        Assert.Null(tenantContext.CurrentBusinessId);
        Assert.Empty(await filtered.Customers.ToListAsync());
    }
}
