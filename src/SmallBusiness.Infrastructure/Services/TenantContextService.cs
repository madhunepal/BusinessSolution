using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Infrastructure.Data;

namespace SmallBusiness.Infrastructure.Services;

/// <summary>
/// Extracts the current user's identity and tenant context from HttpContext.
/// Registered as scoped in DI.
/// </summary>
public class TenantContextService : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly DbContextOptions<ApplicationDbContext> _dbContextOptions;
    private bool _tenantResolved;
    private Guid? _effectiveBusinessId;

    public TenantContextService(
        IHttpContextAccessor httpContextAccessor,
        DbContextOptions<ApplicationDbContext> dbContextOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContextOptions = dbContextOptions;
    }

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public Guid? CurrentBusinessId
    {
        get
        {
            if (_tenantResolved)
            {
                return _effectiveBusinessId;
            }

            _tenantResolved = true;
            _effectiveBusinessId = ResolveEffectiveBusinessId();
            return _effectiveBusinessId;
        }
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
        
    public bool IsCrossTenantAdmin =>
        IsAuthenticated &&
        (_httpContextAccessor.HttpContext?.User?.HasClaim(c => c.Type == ClaimTypes.Role && c.Value == "SysAdmin") ?? false);

    private Guid? ResolveEffectiveBusinessId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var userId = UserId;
        var claim = user?.FindFirstValue("BusinessId");

        if (!IsAuthenticated || string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(claim, out var claimedBusinessId))
        {
            return null;
        }

        if (IsCrossTenantAdmin)
        {
            return claimedBusinessId;
        }

        using var context = new ApplicationDbContext(_dbContextOptions);
        var hasActiveMembership = context.BusinessUsers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Any(bu => bu.BusinessId == claimedBusinessId && bu.UserId == userId && bu.IsActive);

        return hasActiveMembership ? claimedBusinessId : null;
    }
}
