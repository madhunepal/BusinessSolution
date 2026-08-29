using SmallBusiness.Domain.Common;

namespace SmallBusiness.Domain.Entities;

public class InvoiceLine : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public Guid? SalesOrderLineId { get; set; }

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
