using SmallBusiness.Domain.Common;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Domain.Entities;

public class Payment : BaseEntity, IHasBusinessId
{
    public Guid BusinessId { get; set; }

    public string PaymentNumber { get; set; } = string.Empty;

    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateOnly PaymentDate { get; set; }

    public PaymentMethod Method { get; set; }

    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
}
