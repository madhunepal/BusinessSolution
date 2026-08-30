namespace SmallBusiness.Application.DTOs.Inventory;

public class InventoryProfileDto
{
    public Guid Id { get; set; }
    public Guid CatalogItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string BaseUnit { get; set; } = string.Empty;
    
    public decimal ReorderLevel { get; set; }
    public decimal? PreferredStockLevel { get; set; }
    
    public bool TrackLots { get; set; }
    public bool TrackExpiration { get; set; }
    public bool AllowNegativeStock { get; set; }
    public bool IsActive { get; set; }
    
    public decimal TotalQuantityOnHand { get; set; }
}

public class CreateInventoryProfileDto
{
    public Guid CatalogItemId { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal? PreferredStockLevel { get; set; }
    public bool TrackLots { get; set; }
    public bool TrackExpiration { get; set; }
    public bool AllowNegativeStock { get; set; }
}

public class UpdateInventoryProfileDto
{
    public Guid Id { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal? PreferredStockLevel { get; set; }
    public bool TrackLots { get; set; }
    public bool TrackExpiration { get; set; }
    public bool AllowNegativeStock { get; set; }
    public bool IsActive { get; set; }
}
