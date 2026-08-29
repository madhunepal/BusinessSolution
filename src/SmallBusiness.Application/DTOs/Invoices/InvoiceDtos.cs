using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Application.DTOs.Invoices;

public class InvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid SalesOrderId { get; set; }
    public string SalesOrderNumber { get; set; } = string.Empty;
    
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerNumber { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }

    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }

    public InvoiceStatus Status { get; set; }
    public bool IsOverdue { get; set; }
    
    public string? Terms { get; set; }
    public string? Notes { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }

    public List<InvoiceLineDto> Lines { get; set; } = new();
}

public class InvoiceLineDto
{
    public Guid Id { get; set; }
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

public class InvoiceSearchRequest
{
    public string? Keyword { get; set; }
    public InvoiceStatus? Status { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool OverdueOnly { get; set; }
}

public class UpdateInvoiceMetadataRequest
{
    public Guid Id { get; set; }
    public DateOnly DueDate { get; set; }
    public string? Terms { get; set; }
    public string? Notes { get; set; }
}
