using SmallBusiness.Domain.Common;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Domain.Entities;

public class CatalogItem : BusinessOwnedEntity
{
    public string ItemCode { get; set; } = string.Empty;
    public CatalogItemType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Unit { get; set; } = string.Empty;
    
    public decimal Cost { get; set; }
    public decimal SellingPrice { get; set; }
    
    public bool Taxable { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public Business Business { get; set; } = null!;
}
