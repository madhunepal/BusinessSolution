namespace SmallBusiness.Application.Interfaces;

/// <summary>
/// Provides the current authenticated user's tenant context.
/// Implemented in the Web/Infrastructure layer from ClaimsPrincipal.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// The ASP.NET Core Identity user ID.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// The currently active business (tenant) ID.
    /// </summary>
    Guid? CurrentBusinessId { get; }

    /// <summary>
    /// Whether the user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
    
    /// <summary>
    /// Whether the user has cross-tenant administration privileges.
    /// </summary>
    bool IsCrossTenantAdmin { get; }
}
