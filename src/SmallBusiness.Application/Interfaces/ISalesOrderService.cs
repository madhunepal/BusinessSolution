using SmallBusiness.Application.Common.Models;
using SmallBusiness.Application.DTOs.SalesOrders;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Application.Interfaces;

public interface ISalesOrderService
{
    Task<Guid> CreateSalesOrderAsync(CreateSalesOrderRequest request);
    Task<Guid> ConvertQuoteToSalesOrderAsync(Guid quoteId);
    Task UpdateDraftSalesOrderAsync(Guid id, UpdateSalesOrderRequest request);
    Task ChangeSalesOrderStatusAsync(Guid id, SalesOrderStatus newStatus);
    
    Task<SalesOrderDto> GetSalesOrderAsync(Guid id);
    Task<PagedResult<SalesOrderDto>> GetSalesOrdersAsync(SalesOrderSearchRequest request);
}
