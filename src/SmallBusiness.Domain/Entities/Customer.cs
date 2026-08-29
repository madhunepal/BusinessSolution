using SmallBusiness.Domain.Common;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Domain.Entities;

public class Customer : BaseEntity, IHasBusinessId
{
    public Guid BusinessId { get; set; }
    
    public CustomerType CustomerType { get; set; }
    
    public string CustomerNumber { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    
    public string? PrimaryContactName { get; set; }
    
    public string? Email { get; set; }
    
    public string? PhoneNumber { get; set; }
    
    public string? AddressStreet { get; set; }
    public string? AddressCity { get; set; }
    public string? AddressState { get; set; }
    public string? AddressPostalCode { get; set; }
    public string? AddressCountry { get; set; }
    
    public string? Notes { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Navigation
    public Business Business { get; set; } = null!;
}
