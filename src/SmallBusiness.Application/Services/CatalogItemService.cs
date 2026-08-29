using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SmallBusiness.Application.Common.Models;
using SmallBusiness.Application.DTOs.CatalogItems;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Application.Services;

public class CatalogItemService : ICatalogItemService
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantSequenceService _sequenceService;

    public CatalogItemService(
        IApplicationDbContext context,
        ITenantContext tenantContext,
        ITenantSequenceService sequenceService)
    {
        _context = context;
        _tenantContext = tenantContext;
        _sequenceService = sequenceService;
    }

    public async Task<Guid> CreateCatalogItemAsync(CreateCatalogItemRequest request)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);

        var businessId = _tenantContext.CurrentBusinessId 
            ?? throw new UnauthorizedAccessException("No active business context.");

        string itemCode = string.IsNullOrWhiteSpace(request.ItemCode) 
            ? await _sequenceService.GetNextItemCodeAsync() 
            : request.ItemCode.Trim();

        // Enforce uniqueness for manual item code (case-insensitive where supported by DB collation)
        if (!string.IsNullOrWhiteSpace(request.ItemCode))
        {
            var exists = await _context.CatalogItems
                .AnyAsync(c => c.ItemCode == itemCode);
            if (exists)
            {
                throw new ValidationException($"Item Code '{itemCode}' is already in use.");
            }
        }

        var item = new CatalogItem
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            ItemCode = itemCode,
            Type = request.Type,
            Name = request.Name,
            Description = request.Description,
            Unit = request.Unit,
            Cost = request.Cost,
            SellingPrice = request.SellingPrice,
            Taxable = request.Taxable,
            IsActive = true
        };

        _context.CatalogItems.Add(item);
        
        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityType = "CatalogItem",
            EntityId = item.Id,
            ActivityType = ActivityType.Created,
            Description = $"Catalog Item created: {item.Name} ({item.ItemCode})",
            CreatedBy = _tenantContext.UserId ?? "System"
        };
        _context.Activities.Add(activity);

        await _context.SaveChangesAsync();

        return item.Id;
    }

    public async Task UpdateCatalogItemAsync(Guid id, UpdateCatalogItemRequest request)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);

        var item = await _context.CatalogItems
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException($"Catalog Item {id} not found.");

        item.Type = request.Type;
        item.Name = request.Name;
        item.Description = request.Description;
        item.Unit = request.Unit;
        item.Cost = request.Cost;
        item.SellingPrice = request.SellingPrice;
        item.Taxable = request.Taxable;
        item.IsActive = request.IsActive;

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = item.BusinessId,
            EntityType = "CatalogItem",
            EntityId = item.Id,
            ActivityType = ActivityType.Updated,
            Description = $"Catalog Item updated",
            CreatedBy = _tenantContext.UserId ?? "System"
        };
        _context.Activities.Add(activity);

        await _context.SaveChangesAsync();
    }

    public async Task<CatalogItemDto> GetCatalogItemAsync(Guid id)
    {
        var item = await _context.CatalogItems
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException($"Catalog Item {id} not found.");

        return MapToDto(item);
    }

    public async Task<PagedResult<CatalogItemDto>> GetCatalogItemsAsync(CatalogItemSearchRequest request)
    {
        var query = _context.CatalogItems.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            query = query.Where(c => 
                EF.Functions.Like(c.Name, $"%{request.Query}%") ||
                EF.Functions.Like(c.ItemCode, $"%{request.Query}%") ||
                EF.Functions.Like(c.Description, $"%{request.Query}%"));
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(c => c.IsActive == request.IsActive.Value);
        }
        
        if (request.Type.HasValue)
        {
            query = query.Where(c => c.Type == request.Type.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var dtos = items.Select(MapToDto).ToList();

        return new PagedResult<CatalogItemDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task DeactivateCatalogItemAsync(Guid id)
    {
        var item = await _context.CatalogItems
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException($"Catalog Item {id} not found.");

        if (item.IsActive)
        {
            item.IsActive = false;
            
            var activity = new Activity
            {
                Id = Guid.NewGuid(),
                BusinessId = item.BusinessId,
                EntityType = "CatalogItem",
                EntityId = item.Id,
                ActivityType = ActivityType.StatusChanged,
                Description = $"Catalog Item deactivated",
                CreatedBy = _tenantContext.UserId ?? "System"
            };
            _context.Activities.Add(activity);
            
            await _context.SaveChangesAsync();
        }
    }

    private static CatalogItemDto MapToDto(CatalogItem item)
    {
        return new CatalogItemDto
        {
            Id = item.Id,
            BusinessId = item.BusinessId,
            ItemCode = item.ItemCode,
            Type = item.Type,
            Name = item.Name,
            Description = item.Description,
            Unit = item.Unit,
            Cost = item.Cost,
            SellingPrice = item.SellingPrice,
            Taxable = item.Taxable,
            IsActive = item.IsActive,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }
}
