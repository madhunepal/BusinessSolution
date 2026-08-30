using SmallBusiness.Application.DTOs.Payments;

namespace SmallBusiness.Application.Interfaces;

public interface IPaymentService
{
    Task<PaymentDto> CreatePaymentAsync(CreatePaymentDto request);
    Task<List<PaymentDto>> GetPaymentsForInvoiceAsync(Guid invoiceId);
    Task<PaymentDto> GetPaymentAsync(Guid id);
}
