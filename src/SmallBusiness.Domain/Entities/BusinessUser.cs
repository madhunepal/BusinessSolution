using SmallBusiness.Domain.Common;

namespace SmallBusiness.Domain.Entities;

/// <summary>
/// Associates an Identity user with a Business (tenant).
/// A user may belong to multiple businesses; a business has multiple users.
/// </summary>
public class BusinessUser : BaseEntity, IHasBusinessId
{
    public Guid BusinessId { get; set; }

    /// <summary>
    /// The ASP.NET Core Identity user ID (string, from IdentityUser).
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The role this user holds within this specific business.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    // Navigation
    public Business Business { get; set; } = null!;
}
