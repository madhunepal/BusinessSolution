using SmallBusiness.Domain.Common;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Domain.Entities;

public class Quote : BaseEntity, IHasBusinessId
{
    public Guid BusinessId { get; set; }
    
    public string QuoteNumber { get; set; } = string.Empty;
    
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    
    // Customer Snapshot
    public string CustomerNumberSnapshot { get; set; } = string.Empty;
    public string CustomerNameSnapshot { get; set; } = string.Empty;
    public string? CustomerEmailSnapshot { get; set; }
    public string? CustomerPhoneSnapshot { get; set; }
    
    public DateTime QuoteDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    
    public QuoteStatus Status { get; set; } = QuoteStatus.Draft;
    
    public string? Notes { get; set; }
    
    // Pricing
    public decimal TaxRate { get; set; }
    
    // Calculated Totals
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    
    // Lifecycle Timestamps
    public DateTime? SentAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    
    public ICollection<QuoteLine> Lines { get; set; } = new List<QuoteLine>();
}
