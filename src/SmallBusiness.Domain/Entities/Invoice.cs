using SmallBusiness.Domain.Common;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Domain.Entities;

public class Invoice : BaseEntity, IHasBusinessId
{
    public Guid BusinessId { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public Guid SalesOrderId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    // Customer Snapshot
    public string CustomerNumberSnapshot { get; set; } = string.Empty;
    public string CustomerNameSnapshot { get; set; } = string.Empty;
    public string? CustomerEmailSnapshot { get; set; }
    public string? CustomerPhoneSnapshot { get; set; }
    public string? BillingStreetSnapshot { get; set; }
    public string? BillingCitySnapshot { get; set; }
    public string? BillingStateSnapshot { get; set; }
    public string? BillingPostalCodeSnapshot { get; set; }
    public string? BillingCountrySnapshot { get; set; }

    // Dates
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public string? Terms { get; set; }
    public string? Notes { get; set; }

    // Pricing
    public decimal TaxRate { get; set; }

    // Calculated Totals
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }

    // Phase 7 Payments conceptual state
    public decimal AmountPaid { get; set; } // Will be 0 in Phase 7
    public decimal BalanceDue { get; set; } // Will equal Total in Phase 7

    // Lifecycle
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
}
