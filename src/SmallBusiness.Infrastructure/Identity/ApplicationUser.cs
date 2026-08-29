using Microsoft.AspNetCore.Identity;

namespace SmallBusiness.Infrastructure.Identity;

/// <summary>
/// Extended Identity user with profile fields.
/// Links to BusinessUser for multi-tenant context.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public string FullName => $"{FirstName} {LastName}".Trim();
}
