using SmallBusiness.Application.Common.Models;
using SmallBusiness.Application.DTOs.Customers;

namespace SmallBusiness.Application.Interfaces;

public interface ICustomerService
{
    Task<CustomerDto> GetCustomerAsync(Guid id);
    Task<PagedResult<CustomerDto>> GetCustomersAsync(CustomerSearchRequest request);
    Task<Guid> CreateCustomerAsync(CreateCustomerRequest request);
    Task UpdateCustomerAsync(Guid id, UpdateCustomerRequest request);
    Task DeactivateCustomerAsync(Guid id);
}
