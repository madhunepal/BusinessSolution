using System.ComponentModel.DataAnnotations;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Application.DTOs.Payments;

public class CreatePaymentDto
{
    [Required]
    public Guid InvoiceId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Payment amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Required]
    public DateOnly PaymentDate { get; set; }

    [Required]
    public PaymentMethod Method { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
