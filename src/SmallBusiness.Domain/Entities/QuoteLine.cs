namespace SmallBusiness.Domain.Entities;

public class QuoteLine
{
    public Guid Id { get; set; }
    public Guid QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    public Guid? CatalogItemId { get; set; }
    
    // Snapshots
    public string ItemCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Unit { get; set; } = string.Empty;
    
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public bool Taxable { get; set; }
    
    // Calculated
    public decimal LineTotal { get; set; }
    
    public int SortOrder { get; set; }
}
