using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SmallBusiness.Application.Interfaces;

namespace SmallBusiness.Infrastructure.Services;

/// <summary>
/// Extracts the current user's identity and tenant context from HttpContext.
/// Registered as scoped in DI.
/// </summary>
public class TenantContextService : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public Guid? CurrentBusinessId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("BusinessId");
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
        
    public bool IsCrossTenantAdmin =>
        _httpContextAccessor.HttpContext?.User?.HasClaim(c => c.Type == ClaimTypes.Role && c.Value == "SysAdmin") ?? false;
}
