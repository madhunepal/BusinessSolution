using SmallBusiness.Application.Common;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Application.Services;

/// <summary>
/// Application service for Business (tenant/organization) operations.
/// </summary>
public class BusinessService
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    public BusinessService(IApplicationDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Creates a new business and associates the current user as the Owner.
    /// </summary>
    public async Task<Result<Guid>> CreateBusinessAsync(
        string name,
        string? phone = null,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Guid>("Business name is required.");
        }

        if (_tenantContext.UserId is null)
        {
            return Result.Failure<Guid>("User must be authenticated to create a business.");
        }

        var business = new Business
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Phone = phone,
            Email = email,
            Status = BusinessStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var businessUser = new BusinessUser
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            UserId = _tenantContext.UserId,
            Role = "Owner",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Businesses.Add(business);
        _context.BusinessUsers.Add(businessUser);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(business.Id);
    }

    /// <summary>
    /// Gets the business profile for the current user's active business.
    /// </summary>
    public async Task<Result<Business>> GetCurrentBusinessAsync(CancellationToken cancellationToken = default)
    {
        if (_tenantContext.CurrentBusinessId is null)
        {
            return Result.Failure<Business>("No active business selected.");
        }

        var business = await _context.Businesses.FindAsync(
            [_tenantContext.CurrentBusinessId.Value], cancellationToken);

        if (business is null)
        {
            return Result.Failure<Business>("Business not found.");
        }

        return Result.Success(business);
    }
}
