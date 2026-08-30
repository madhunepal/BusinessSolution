using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Application.DTOs.Payments;

public class PaymentDto
{
    public Guid Id { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; }
    public PaymentMethod Method { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
