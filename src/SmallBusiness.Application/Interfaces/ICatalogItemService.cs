using SmallBusiness.Application.Common.Models;
using SmallBusiness.Application.DTOs.CatalogItems;

namespace SmallBusiness.Application.Interfaces;

public interface ICatalogItemService
{
    Task<Guid> CreateCatalogItemAsync(CreateCatalogItemRequest request);
    Task UpdateCatalogItemAsync(Guid id, UpdateCatalogItemRequest request);
    Task<CatalogItemDto> GetCatalogItemAsync(Guid id);
    Task<PagedResult<CatalogItemDto>> GetCatalogItemsAsync(CatalogItemSearchRequest request);
    Task DeactivateCatalogItemAsync(Guid id);
}
