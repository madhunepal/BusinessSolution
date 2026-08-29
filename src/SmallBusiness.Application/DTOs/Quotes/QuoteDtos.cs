using System.ComponentModel.DataAnnotations;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Application.DTOs.Quotes;

public class QuoteDto
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public string QuoteNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerNameSnapshot { get; set; } = string.Empty;
    public DateTime QuoteDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public QuoteStatus Status { get; set; }
    public string? Notes { get; set; }
    
    public decimal TaxRate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    
    public DateTime? SentAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    public List<QuoteLineDto> Lines { get; set; } = new();
}

public class QuoteLineDto
{
    public Guid Id { get; set; }
    public Guid? CatalogItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public bool Taxable { get; set; }
    public decimal LineTotal { get; set; }
    public int SortOrder { get; set; }
}

public class CreateQuoteRequest
{
    [Required]
    public Guid CustomerId { get; set; }
    
    [Required]
    public DateTime QuoteDate { get; set; } = DateTime.Today;
    
    public DateTime? ExpirationDate { get; set; }
    
    [StringLength(4000)]
    public string? Notes { get; set; }
    
    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Tax Rate cannot be negative.")]
    public decimal TaxRate { get; set; }
    
    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Discount cannot be negative.")]
    public decimal DiscountAmount { get; set; }
    
    public List<QuoteLineRequest> Lines { get; set; } = new();
}

public class UpdateQuoteRequest
{
    [Required]
    public Guid CustomerId { get; set; }
    
    [Required]
    public DateTime QuoteDate { get; set; }
    
    public DateTime? ExpirationDate { get; set; }
    
    [StringLength(4000)]
    public string? Notes { get; set; }
    
    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Tax Rate cannot be negative.")]
    public decimal TaxRate { get; set; }
    
    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Discount cannot be negative.")]
    public decimal DiscountAmount { get; set; }
    
    public List<QuoteLineRequest> Lines { get; set; } = new();
}

public class QuoteLineRequest
{
    public Guid? CatalogItemId { get; set; }
    
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(2000)]
    public string? Description { get; set; }
    
    [StringLength(50)]
    public string Unit { get; set; } = string.Empty;
    
    [Range(0.0001, (double)decimal.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; } = 1m;
    
    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Unit Price cannot be negative.")]
    public decimal UnitPrice { get; set; }
    
    public bool Taxable { get; set; }
}

public class QuoteSearchRequest
{
    public string? Query { get; set; }
    public Guid? CustomerId { get; set; }
    public QuoteStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
