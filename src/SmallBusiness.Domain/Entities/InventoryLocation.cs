using SmallBusiness.Domain.Common;

namespace SmallBusiness.Domain.Entities;

public class InventoryLocation : BusinessOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}
