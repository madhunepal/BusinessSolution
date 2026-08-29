using SmallBusiness.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmallBusiness.Application.DTOs.SalesOrders;

public class SalesOrderDto
{
    public Guid Id { get; set; }
    public string SalesOrderNumber { get; set; } = string.Empty;
    public Guid? QuoteId { get; set; }
    
    public Guid CustomerId { get; set; }
    public string CustomerNameSnapshot { get; set; } = string.Empty;
    public string? CustomerNumberSnapshot { get; set; }
    public string? CustomerEmailSnapshot { get; set; }
    public string? CustomerPhoneSnapshot { get; set; }
    
    public DateTime OrderDate { get; set; }
    public SalesOrderStatus Status { get; set; }
    
    public string? Notes { get; set; }
    
    public decimal TaxRate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    
    public List<SalesOrderLineDto> Lines { get; set; } = new();
}

public class SalesOrderLineDto
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
}

public class SalesOrderSearchRequest
{
    public string? SearchTerm { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? QuoteId { get; set; }
    public SalesOrderStatus? Status { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class CreateSalesOrderRequest
{
    [Required]
    public Guid CustomerId { get; set; }
    
    [Required]
    public DateTime OrderDate { get; set; } = DateTime.Today;
    
    [StringLength(4000)]
    public string? Notes { get; set; }
    
    [Range(0, 100)]
    public decimal TaxRate { get; set; }
    
    [Range(0, 9999999)]
    public decimal DiscountAmount { get; set; }
    
    [Required]
    [MinLength(1, ErrorMessage = "At least one line item is required.")]
    public List<SalesOrderLineRequest> Lines { get; set; } = new();
}

public class UpdateSalesOrderRequest
{
    [Required]
    public Guid CustomerId { get; set; }
    
    [Required]
    public DateTime OrderDate { get; set; }
    
    [StringLength(4000)]
    public string? Notes { get; set; }
    
    [Range(0, 100)]
    public decimal TaxRate { get; set; }
    
    [Range(0, 9999999)]
    public decimal DiscountAmount { get; set; }
    
    [Required]
    [MinLength(1, ErrorMessage = "At least one line item is required.")]
    public List<SalesOrderLineRequest> Lines { get; set; } = new();
}

public class SalesOrderLineRequest
{
    public Guid? CatalogItemId { get; set; }
    
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(2000)]
    public string? Description { get; set; }
    
    [StringLength(50)]
    public string Unit { get; set; } = string.Empty;
    
    [Range(0.01, 999999)]
    public decimal Quantity { get; set; }
    
    [Range(0, 999999)]
    public decimal UnitPrice { get; set; }
    
    public bool Taxable { get; set; }
}
