using Microsoft.EntityFrameworkCore;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Infrastructure.Data;
using SmallBusiness.Infrastructure.Identity;

namespace SmallBusiness.Infrastructure.Services;

public sealed class PermissionService : IPermissionService
{
    private readonly ITenantContext _tenantContext;
    private readonly DbContextOptions<ApplicationDbContext> _dbContextOptions;
    private readonly Dictionary<string, bool> _permissionCache = new();

    public PermissionService(
        ITenantContext tenantContext,
        DbContextOptions<ApplicationDbContext> dbContextOptions)
    {
        _tenantContext = tenantContext;
        _dbContextOptions = dbContextOptions;
    }

    public async Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default)
    {
        if (_permissionCache.TryGetValue(permission, out var cached))
        {
            return cached;
        }

        var allowed = await ResolvePermissionAsync(permission, cancellationToken);
        _permissionCache[permission] = allowed;
        return allowed;
    }

    public async Task EnsurePermissionAsync(string permission, CancellationToken cancellationToken = default)
    {
        if (!await HasPermissionAsync(permission, cancellationToken))
        {
            throw new UnauthorizedAccessException($"Permission '{permission}' is required.");
        }
    }

    private async Task<bool> ResolvePermissionAsync(string permission, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated)
        {
            return false;
        }

        if (_tenantContext.IsCrossTenantAdmin)
        {
            return true;
        }

        var businessId = _tenantContext.CurrentBusinessId;
        var userId = _tenantContext.UserId;

        if (businessId is null || string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        await using var context = new ApplicationDbContext(_dbContextOptions);
        var role = await context.BusinessUsers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(bu => bu.BusinessId == businessId.Value && bu.UserId == userId && bu.IsActive)
            .Select(bu => bu.Role)
            .FirstOrDefaultAsync(cancellationToken);

        return role is not null &&
            AppRoles.DefaultRolePermissions.TryGetValue(role, out var permissions) &&
            permissions.Contains(permission);
    }
}
