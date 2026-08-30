using SmallBusiness.Domain.Common;

namespace SmallBusiness.Domain.Entities;

public class InventoryProfile : BusinessOwnedEntity
{
    public Guid CatalogItemId { get; set; }
    
    public decimal ReorderLevel { get; set; }
    public decimal? PreferredStockLevel { get; set; }
    
    public bool TrackLots { get; set; }
    public bool TrackExpiration { get; set; }
    public bool AllowNegativeStock { get; set; } = false;
    
    public bool IsActive { get; set; } = true;

    // Navigation
    public CatalogItem CatalogItem { get; set; } = null!;
}
