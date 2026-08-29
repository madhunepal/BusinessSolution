using SmallBusiness.Domain.Common;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Domain.Entities;

public class SalesOrder : BaseEntity, IHasBusinessId
{
    public Guid BusinessId { get; set; }
    
    public string SalesOrderNumber { get; set; } = string.Empty;
    
    public Guid? QuoteId { get; set; }
    public Quote? Quote { get; set; }
    
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    
    // Customer Snapshot
    public string CustomerNumberSnapshot { get; set; } = string.Empty;
    public string CustomerNameSnapshot { get; set; } = string.Empty;
    public string? CustomerEmailSnapshot { get; set; }
    public string? CustomerPhoneSnapshot { get; set; }
    
    public DateTime OrderDate { get; set; }
    
    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;
    
    public string? Notes { get; set; }
    
    // Pricing
    public decimal TaxRate { get; set; }
    
    // Calculated Totals
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    
    // Lifecycle Timestamps
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    
    public ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();
}
