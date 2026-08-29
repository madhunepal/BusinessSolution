using SmallBusiness.Application.DTOs.Invoices;

namespace SmallBusiness.Application.Interfaces;

public interface IInvoiceService
{
    Task<InvoiceDto> CreateInvoiceFromSalesOrderAsync(Guid salesOrderId);
    Task<InvoiceDto> UpdateDraftInvoiceMetadataAsync(UpdateInvoiceMetadataRequest request);
    Task<InvoiceDto> GetInvoiceAsync(Guid id);
    Task<List<InvoiceDto>> GetInvoicesAsync(InvoiceSearchRequest request);
    Task SendInvoiceAsync(Guid id);
    Task VoidInvoiceAsync(Guid id);
}
