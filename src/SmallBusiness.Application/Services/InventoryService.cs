using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SmallBusiness.Application.DTOs.Inventory;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly IApplicationDbContextFactory _contextFactory;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService? _permissionService;

    public InventoryService(
        IApplicationDbContextFactory contextFactory,
        ITenantContext tenantContext,
        IPermissionService? permissionService = null)
    {
        _contextFactory = contextFactory;
        _tenantContext = tenantContext;
        _permissionService = permissionService;
    }

    private Guid GetBusinessId() => _tenantContext.CurrentBusinessId ?? throw new UnauthorizedAccessException();

    public async Task<List<InventoryProfileDto>> GetInventoryProfilesAsync()
    {
        await EnsurePermissionAsync("Inventory.View");
        var businessId = GetBusinessId();
        await using var context = await _contextFactory.CreateDbContextAsync();
        var profiles = await context.InventoryProfiles
            .Include(p => p.CatalogItem)
            .Where(p => p.BusinessId == businessId)
            .ToListAsync();
            
        var stockLevels = await context.InventoryStockLevels
            .Where(s => s.BusinessId == businessId)
            .GroupBy(s => s.InventoryProfileId)
            .Select(g => new { ProfileId = g.Key, TotalQty = g.Sum(s => s.QuantityOnHand) })
            .ToDictionaryAsync(k => k.ProfileId, v => v.TotalQty);

        return profiles.Select(p => MapProfileToDto(p, stockLevels.GetValueOrDefault(p.Id, 0m))).ToList();
    }

    public async Task<InventoryProfileDto> GetInventoryProfileAsync(Guid id)
    {
        await EnsurePermissionAsync("Inventory.View");
        var businessId = GetBusinessId();
        await using var context = await _contextFactory.CreateDbContextAsync();
        var profile = await context.InventoryProfiles
            .Include(p => p.CatalogItem)
            .FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == businessId)
            ?? throw new KeyNotFoundException("Inventory profile not found.");

        var totalQty = await context.InventoryStockLevels
            .Where(s => s.BusinessId == businessId && s.InventoryProfileId == id)
            .SumAsync(s => s.QuantityOnHand);

        return MapProfileToDto(profile, totalQty);
    }

    public async Task<InventoryProfileDto> CreateInventoryProfileAsync(CreateInventoryProfileDto request)
    {
        await EnsurePermissionAsync("Inventory.Manage");
        var businessId = GetBusinessId();
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        var catalogItem = await context.CatalogItems
            .FirstOrDefaultAsync(c => c.Id == request.CatalogItemId && c.BusinessId == businessId)
            ?? throw new KeyNotFoundException("Catalog item not found.");
            
        if (catalogItem.Type != CatalogItemType.Product)
            throw new ValidationException("Inventory profiles can only be created for Product catalog items.");

        if (request.TrackExpiration && !request.TrackLots)
            throw new ValidationException("TrackExpiration requires TrackLots to be true.");

        var existing = await context.InventoryProfiles
            .AnyAsync(p => p.CatalogItemId == request.CatalogItemId && p.BusinessId == businessId);
            
        if (existing)
            throw new ValidationException("An inventory profile already exists for this catalog item.");

        var profile = new InventoryProfile
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            CatalogItemId = request.CatalogItemId,
            ReorderLevel = request.ReorderLevel,
            PreferredStockLevel = request.PreferredStockLevel,
            TrackLots = request.TrackLots,
            TrackExpiration = request.TrackExpiration,
            AllowNegativeStock = request.AllowNegativeStock,
            IsActive = true
        };

        context.InventoryProfiles.Add(profile);
        
        context.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityId = profile.Id,
            EntityType = "InventoryProfile",
            ActivityType = ActivityType.Created,
            Description = $"Inventory profile created for {catalogItem.Name}",
            CreatedBy = _tenantContext.UserId ?? "System"
        });

        await context.SaveChangesAsync();
        
        // Load navigation property for mapping
        profile.CatalogItem = catalogItem;

        return MapProfileToDto(profile, 0);
    }

    public async Task<InventoryProfileDto> UpdateInventoryProfileAsync(UpdateInventoryProfileDto request)
    {
        await EnsurePermissionAsync("Inventory.Manage");
        var businessId = GetBusinessId();
        await using var context = await _contextFactory.CreateDbContextAsync();
        var profile = await context.InventoryProfiles
            .Include(p => p.CatalogItem)
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.BusinessId == businessId)
            ?? throw new KeyNotFoundException("Inventory profile not found.");

        if (request.TrackExpiration && !request.TrackLots)
            throw new ValidationException("TrackExpiration requires TrackLots to be true.");

        profile.ReorderLevel = request.ReorderLevel;
        profile.PreferredStockLevel = request.PreferredStockLevel;
        profile.TrackLots = request.TrackLots;
        profile.TrackExpiration = request.TrackExpiration;
        profile.AllowNegativeStock = request.AllowNegativeStock;
        profile.IsActive = request.IsActive;
        
        context.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityId = profile.Id,
            EntityType = "InventoryProfile",
            ActivityType = ActivityType.Updated,
            Description = $"Inventory profile updated for {profile.CatalogItem.Name}",
            CreatedBy = _tenantContext.UserId ?? "System"
        });

        await context.SaveChangesAsync();

        var totalQty = await context.InventoryStockLevels
            .Where(s => s.BusinessId == businessId && s.InventoryProfileId == profile.Id)
            .SumAsync(s => s.QuantityOnHand);

        return MapProfileToDto(profile, totalQty);
    }

    public async Task<List<InventoryLocationDto>> GetLocationsAsync()
    {
        await EnsurePermissionAsync("Inventory.View");
        var businessId = GetBusinessId();
        await using var context = await _contextFactory.CreateDbContextAsync();
        var locations = await context.InventoryLocations
            .Where(l => l.BusinessId == businessId)
            .ToListAsync();
            
        return locations.Select(MapLocationToDto).ToList();
    }

    public async Task<InventoryLocationDto> CreateLocationAsync(CreateInventoryLocationDto request)
    {
        await EnsurePermissionAsync("Inventory.Manage");
        var businessId = GetBusinessId();
        await using var context = await _contextFactory.CreateDbContextAsync();

        var locationName = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(locationName))
        {
            throw new ValidationException("Location name is required.");
        }

        var duplicateNameExists = await context.InventoryLocations
            .AnyAsync(l => l.BusinessId == businessId && l.Name.ToLower() == locationName.ToLower());

        if (duplicateNameExists)
        {
            throw new ValidationException("An inventory location with this name already exists.");
        }
        
        if (request.IsDefault)
        {
            var defaults = await context.InventoryLocations
                .Where(l => l.BusinessId == businessId && l.IsDefault)
                .ToListAsync();
            foreach (var d in defaults)
                d.IsDefault = false;
        }

        var location = new InventoryLocation
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Name = locationName,
            Description = request.Description?.Trim(),
            IsDefault = request.IsDefault,
            IsActive = true
        };

        context.InventoryLocations.Add(location);
        await context.SaveChangesAsync();
        
        return MapLocationToDto(location);
    }

    public async Task<InventoryMovementDto> ReceiveStockAsync(StockReceiptDto request)
    {
        await EnsurePermissionAsync("Inventory.Receive");
        if (request.Quantity <= 0)
            throw new ValidationException("Quantity must be greater than zero.");
            
        return await ProcessStockOperationAsync(
            request.InventoryProfileId,
            request.InventoryLocationId,
            request.ExistingLotId,
            request.NewLot,
            InventoryMovementType.Receipt,
            request.Quantity,
            request.UnitCost,
            request.Notes,
            null);
    }

    public async Task<InventoryMovementDto> RecordUsageAsync(StockUsageDto request)
    {
        await EnsurePermissionAsync("Inventory.Adjust");
        if (request.Quantity <= 0)
            throw new ValidationException("Quantity must be greater than zero.");
            
        return await ProcessStockOperationAsync(
            request.InventoryProfileId,
            request.InventoryLocationId,
            request.InventoryLotId,
            null,
            InventoryMovementType.Usage,
            -request.Quantity, // issue is negative
            null,
            request.Notes,
            request.Reason);
    }

    public async Task<InventoryMovementDto> RecordWasteAsync(StockWasteDto request)
    {
        await EnsurePermissionAsync("Inventory.Adjust");
        if (request.Quantity <= 0)
            throw new ValidationException("Quantity must be greater than zero.");
            
        return await ProcessStockOperationAsync(
            request.InventoryProfileId,
            request.InventoryLocationId,
            request.InventoryLotId,
            null,
            InventoryMovementType.Waste,
            -request.Quantity, // waste is negative
            null,
            request.Notes,
            request.Reason);
    }

    public async Task<InventoryMovementDto> AdjustStockAsync(StockAdjustmentDto request)
    {
        await EnsurePermissionAsync("Inventory.Adjust");
        if (request.QuantityDifference == 0)
            throw new ValidationException("Adjustment quantity cannot be zero.");
            
        var movementType = request.QuantityDifference > 0 
            ? InventoryMovementType.AdjustmentIncrease 
            : InventoryMovementType.AdjustmentDecrease;
            
        return await ProcessStockOperationAsync(
            request.InventoryProfileId,
            request.InventoryLocationId,
            request.InventoryLotId,
            null,
            movementType,
            request.QuantityDifference,
            null,
            request.Notes,
            request.Reason);
    }

    public async Task<List<InventoryMovementDto>> TransferStockAsync(StockTransferDto request)
    {
        await EnsurePermissionAsync("Inventory.Transfer");
        if (request.Quantity <= 0)
            throw new ValidationException("Transfer quantity must be greater than zero.");
            
        if (request.SourceLocationId == request.DestinationLocationId)
            throw new ValidationException("Source and destination locations cannot be the same.");

        var businessId = GetBusinessId();
        await using var context = await _contextFactory.CreateDbContextAsync();
        int maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var profile = await context.InventoryProfiles
                    .FirstOrDefaultAsync(p => p.Id == request.InventoryProfileId && p.BusinessId == businessId)
                    ?? throw new KeyNotFoundException("Inventory profile not found.");

                await ValidateLocationAsync(context, request.SourceLocationId, businessId);
                await ValidateLocationAsync(context, request.DestinationLocationId, businessId);

                if (profile.TrackLots && !request.InventoryLotId.HasValue)
                    throw new ValidationException("Lot selection is required for this inventory profile.");
                    
                if (request.InventoryLotId.HasValue)
                {
                    var lot = await context.InventoryLots
                        .FirstOrDefaultAsync(l => l.Id == request.InventoryLotId.Value && l.BusinessId == businessId)
                        ?? throw new ValidationException("Invalid lot.");
                    if (lot.InventoryProfileId != profile.Id)
                        throw new ValidationException("Lot does not belong to this profile.");
                }

                // 1. Process Source bucket
                var sourceBucket = await GetOrCreateBucketAsync(context, businessId, profile.Id, request.SourceLocationId, request.InventoryLotId);
                
                var newSourceQty = sourceBucket.QuantityOnHand - request.Quantity;
                if (!profile.AllowNegativeStock && newSourceQty < 0)
                    throw new ValidationException("Insufficient stock in source location.");
                sourceBucket.QuantityOnHand = newSourceQty;

                var outMovement = new InventoryMovement
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    InventoryProfileId = profile.Id,
                    InventoryLocationId = request.SourceLocationId,
                    InventoryLotId = request.InventoryLotId,
                    MovementType = InventoryMovementType.TransferOut,
                    Quantity = -request.Quantity,
                    Notes = request.Notes,
                    OccurredAt = DateTimeOffset.UtcNow,
                    CreatedBy = _tenantContext.UserId ?? "System"
                };
                context.InventoryMovements.Add(outMovement);

                // 2. Process Destination bucket
                var destBucket = await GetOrCreateBucketAsync(context, businessId, profile.Id, request.DestinationLocationId, request.InventoryLotId);
                destBucket.QuantityOnHand += request.Quantity;

                var inMovement = new InventoryMovement
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    InventoryProfileId = profile.Id,
                    InventoryLocationId = request.DestinationLocationId,
                    InventoryLotId = request.InventoryLotId,
                    MovementType = InventoryMovementType.TransferIn,
                    Quantity = request.Quantity,
                    Notes = request.Notes,
                    OccurredAt = DateTimeOffset.UtcNow,
                    CreatedBy = _tenantContext.UserId ?? "System"
                };
                context.InventoryMovements.Add(inMovement);
                
                context.Activities.Add(new Activity
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    EntityId = profile.Id,
                    EntityType = "InventoryProfile",
                    ActivityType = ActivityType.StockTransferred,
                    Description = $"Transferred {request.Quantity} from {request.SourceLocationId} to {request.DestinationLocationId}",
                    CreatedBy = _tenantContext.UserId ?? "System"
                });

                await context.SaveChangesAsync();
                
                // Load nav properties for DTOs
                outMovement = await LoadMovementNavigations(context, outMovement.Id);
                inMovement = await LoadMovementNavigations(context, inMovement.Id);
                
                return new List<InventoryMovementDto> { MapMovementToDto(outMovement), MapMovementToDto(inMovement) };
            }
            catch (DbUpdateConcurrencyException)
            {
                if (i == maxRetries - 1) throw new InvalidOperationException("Failed to transfer stock due to concurrent updates.");
                
                ClearTrackedState(context);
                await Task.Delay(Random.Shared.Next(50, 150));
            }
        }
        
        throw new InvalidOperationException("Failed to process transfer.");
    }

    private async Task<InventoryMovementDto> ProcessStockOperationAsync(
        Guid profileId,
        Guid locationId,
        Guid? existingLotId,
        CreateInventoryLotDto? newLotDto,
        InventoryMovementType type,
        decimal quantityChange,
        decimal? unitCost,
        string? notes,
        string? reason)
    {
        var businessId = GetBusinessId();
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        int maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var profile = await context.InventoryProfiles
                    .FirstOrDefaultAsync(p => p.Id == profileId && p.BusinessId == businessId)
                    ?? throw new KeyNotFoundException("Inventory profile not found.");

                await ValidateLocationAsync(context, locationId, businessId);

                Guid? finalLotId = existingLotId;

                if (profile.TrackLots)
                {
                    if (existingLotId == null && newLotDto == null)
                        throw new ValidationException("Lot selection is required for this inventory profile.");
                        
                    if (existingLotId.HasValue)
                    {
                        var existing = await context.InventoryLots
                            .FirstOrDefaultAsync(l => l.Id == existingLotId.Value && l.BusinessId == businessId)
                            ?? throw new ValidationException("Invalid lot.");
                        if (existing.InventoryProfileId != profile.Id)
                            throw new ValidationException("Lot does not belong to this profile.");
                    }
                    else if (newLotDto != null)
                    {
                        // Validate new lot
                        if (string.IsNullOrWhiteSpace(newLotDto.LotNumber))
                            throw new ValidationException("Lot number is required.");
                            
                        if (profile.TrackExpiration && !newLotDto.ExpirationDate.HasValue)
                            throw new ValidationException("Expiration date is required.");
                            
                        if (newLotDto.ProductionDate.HasValue && newLotDto.ExpirationDate.HasValue && newLotDto.ExpirationDate.Value < newLotDto.ProductionDate.Value)
                            throw new ValidationException("Expiration date cannot precede production date.");

                        // To avoid concurrent insert of same lot, one might need a lock, but we assume unique LotNumber if needed, 
                        // though prompt doesn't explicitly mandate global lot uniqueness, just lot tracking.
                        var lot = new InventoryLot
                        {
                            Id = Guid.NewGuid(),
                            BusinessId = businessId,
                            InventoryProfileId = profileId,
                            LotNumber = newLotDto.LotNumber,
                            ReceivedDate = newLotDto.ReceivedDate,
                            ProductionDate = newLotDto.ProductionDate,
                            ExpirationDate = newLotDto.ExpirationDate,
                            UnitCost = newLotDto.UnitCost,
                            Notes = newLotDto.Notes
                        };
                        context.InventoryLots.Add(lot);
                        finalLotId = lot.Id;
                    }
                }
                else
                {
                    if (existingLotId.HasValue || newLotDto != null)
                        throw new ValidationException("This profile does not track lots.");
                }

                var bucket = await GetOrCreateBucketAsync(context, businessId, profileId, locationId, finalLotId);
                
                var newQty = bucket.QuantityOnHand + quantityChange;
                
                if (!profile.AllowNegativeStock && newQty < 0)
                    throw new ValidationException($"Insufficient stock. Operation would result in {newQty} stock.");
                    
                bucket.QuantityOnHand = newQty;

                var movement = new InventoryMovement
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    InventoryProfileId = profileId,
                    InventoryLocationId = locationId,
                    InventoryLotId = finalLotId,
                    MovementType = type,
                    Quantity = quantityChange,
                    UnitCost = unitCost,
                    Notes = notes,
                    Reason = reason,
                    OccurredAt = DateTimeOffset.UtcNow,
                    CreatedBy = _tenantContext.UserId ?? "System"
                };

                context.InventoryMovements.Add(movement);

                var activityType = type switch
                {
                    InventoryMovementType.Receipt => ActivityType.StockReceived,
                    InventoryMovementType.Usage => ActivityType.StockUsed,
                    InventoryMovementType.Waste => ActivityType.StockWasted,
                    InventoryMovementType.AdjustmentIncrease or InventoryMovementType.AdjustmentDecrease => ActivityType.StockAdjusted,
                    _ => ActivityType.Updated
                };

                context.Activities.Add(new Activity
                {
                    Id = Guid.NewGuid(),
                    BusinessId = businessId,
                    EntityId = profile.Id,
                    EntityType = "InventoryProfile",
                    ActivityType = activityType,
                    Description = $"{type} of {quantityChange} recorded at location.",
                    CreatedBy = _tenantContext.UserId ?? "System"
                });

                await context.SaveChangesAsync();
                
                movement = await LoadMovementNavigations(context, movement.Id);
                return MapMovementToDto(movement);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (i == maxRetries - 1) throw new InvalidOperationException("Failed to process stock movement due to concurrent updates.");
                
                ClearTrackedState(context);
                await Task.Delay(Random.Shared.Next(50, 150));
            }
        }
        
        throw new InvalidOperationException("Failed to process operation.");
    }

    private async Task<InventoryStockLevel> GetOrCreateBucketAsync(IApplicationDbContext context, Guid businessId, Guid profileId, Guid locationId, Guid? lotId)
    {
        var bucket = await context.InventoryStockLevels
            .FirstOrDefaultAsync(s => s.BusinessId == businessId && 
                                      s.InventoryProfileId == profileId && 
                                      s.InventoryLocationId == locationId && 
                                      s.InventoryLotId == lotId);
                                      
        if (bucket == null)
        {
            bucket = new InventoryStockLevel
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                InventoryProfileId = profileId,
                InventoryLocationId = locationId,
                InventoryLotId = lotId,
                QuantityOnHand = 0,
                RowVersion = new byte[8]
            };
            context.InventoryStockLevels.Add(bucket);
            // Calling SaveChanges to insert the row isn't strictly necessary here if we save at the end, 
            // but for concurrent inserts of the same bucket, we might hit unique constraint violations.
            // EF will handle unique constraint violation on SaveChangesAsync(). We let the caller handle it.
        }
        return bucket;
    }

    private async Task<InventoryMovement> LoadMovementNavigations(IApplicationDbContext context, Guid movementId)
    {
        return await context.InventoryMovements
            .Include(m => m.InventoryLocation)
            .Include(m => m.InventoryLot)
            .FirstAsync(m => m.Id == movementId);
    }

    public async Task<List<InventoryMovementDto>> GetMovementHistoryAsync(Guid profileId)
    {
        await EnsurePermissionAsync("Inventory.View");
        var businessId = GetBusinessId();
        await using var context = await _contextFactory.CreateDbContextAsync();
        var movements = await context.InventoryMovements
            .Include(m => m.InventoryLocation)
            .Include(m => m.InventoryLot)
            .Where(m => m.BusinessId == businessId && m.InventoryProfileId == profileId)
            .OrderByDescending(m => m.OccurredAt)
            .ThenByDescending(m => m.CreatedAt)
            .ToListAsync();
            
        return movements.Select(MapMovementToDto).ToList();
    }

    public async Task<List<InventoryStockLevelDto>> GetStockLevelsAsync(Guid profileId)
    {
        await EnsurePermissionAsync("Inventory.View");
        var businessId = GetBusinessId();
        await using var context = await _contextFactory.CreateDbContextAsync();
        var levels = await context.InventoryStockLevels
            .Include(s => s.InventoryLocation)
            .Include(s => s.InventoryLot)
            .Where(s => s.BusinessId == businessId && s.InventoryProfileId == profileId)
            .ToListAsync();
            
        return levels.Select(s => new InventoryStockLevelDto
        {
            InventoryProfileId = s.InventoryProfileId,
            InventoryLocationId = s.InventoryLocationId,
            LocationName = s.InventoryLocation.Name,
            InventoryLotId = s.InventoryLotId,
            LotNumber = s.InventoryLot?.LotNumber,
            QuantityOnHand = s.QuantityOnHand
        }).ToList();
    }

    public async Task<List<InventoryProfileDto>> GetLowStockProfilesAsync()
    {
        await EnsurePermissionAsync("Inventory.View");
        var businessId = GetBusinessId();
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        var profiles = await context.InventoryProfiles
            .Include(p => p.CatalogItem)
            .Where(p => p.BusinessId == businessId && p.IsActive)
            .ToListAsync();
            
        var stockLevels = await context.InventoryStockLevels
            .Where(s => s.BusinessId == businessId)
            .GroupBy(s => s.InventoryProfileId)
            .Select(g => new { ProfileId = g.Key, TotalQty = g.Sum(s => s.QuantityOnHand) })
            .ToDictionaryAsync(k => k.ProfileId, v => v.TotalQty);

        return profiles
            .Select(p => MapProfileToDto(p, stockLevels.GetValueOrDefault(p.Id, 0m)))
            .Where(dto => dto.TotalQuantityOnHand <= dto.ReorderLevel)
            .ToList();
    }

    public async Task<List<InventoryLotDto>> GetExpiringLotsAsync(int daysToExpiration = 30)
    {
        await EnsurePermissionAsync("Inventory.View");
        var businessId = GetBusinessId();
        var thresholdDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysToExpiration));
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        var lots = await context.InventoryLots
            .Where(l => l.BusinessId == businessId && 
                        l.ExpirationDate != null && 
                        l.ExpirationDate <= thresholdDate)
            .OrderBy(l => l.ExpirationDate)
            .ToListAsync();
            
        return lots.Select(l => new InventoryLotDto
        {
            Id = l.Id,
            InventoryProfileId = l.InventoryProfileId,
            LotNumber = l.LotNumber,
            ReceivedDate = l.ReceivedDate,
            ProductionDate = l.ProductionDate,
            ExpirationDate = l.ExpirationDate,
            UnitCost = l.UnitCost,
            Notes = l.Notes
        }).ToList();
    }

    private static InventoryProfileDto MapProfileToDto(InventoryProfile p, decimal totalQty) => new()
    {
        Id = p.Id,
        CatalogItemId = p.CatalogItemId,
        ItemCode = p.CatalogItem.ItemCode,
        ItemName = p.CatalogItem.Name,
        BaseUnit = p.CatalogItem.Unit, // Fallback to CatalogItem unit to avoid duplication
        ReorderLevel = p.ReorderLevel,
        PreferredStockLevel = p.PreferredStockLevel,
        TrackLots = p.TrackLots,
        TrackExpiration = p.TrackExpiration,
        AllowNegativeStock = p.AllowNegativeStock,
        IsActive = p.IsActive,
        TotalQuantityOnHand = totalQty
    };

    private static InventoryLocationDto MapLocationToDto(InventoryLocation l) => new()
    {
        Id = l.Id,
        Name = l.Name,
        Description = l.Description,
        IsDefault = l.IsDefault,
        IsActive = l.IsActive
    };

    private static InventoryMovementDto MapMovementToDto(InventoryMovement m) => new()
    {
        Id = m.Id,
        InventoryProfileId = m.InventoryProfileId,
        InventoryLocationId = m.InventoryLocationId,
        LocationName = m.InventoryLocation?.Name ?? string.Empty,
        InventoryLotId = m.InventoryLotId,
        LotNumber = m.InventoryLot?.LotNumber,
        MovementType = m.MovementType,
        Quantity = m.Quantity,
        UnitCost = m.UnitCost,
        ReferenceType = m.ReferenceType,
        ReferenceId = m.ReferenceId,
        Reason = m.Reason,
        Notes = m.Notes,
        OccurredAt = m.OccurredAt,
        CreatedBy = m.CreatedBy
    };

    private async Task ValidateLocationAsync(IApplicationDbContext context, Guid locationId, Guid businessId)
    {
        var exists = await context.InventoryLocations
            .AnyAsync(l => l.Id == locationId && l.BusinessId == businessId && l.IsActive);

        if (!exists)
        {
            throw new ValidationException("Inventory location is invalid or belongs to another tenant.");
        }
    }

    private Task EnsurePermissionAsync(string permission) =>
        _permissionService?.EnsurePermissionAsync(permission) ?? Task.CompletedTask;

    private static void ClearTrackedState(IApplicationDbContext context)
    {
        if (context is DbContext dbContext)
        {
            dbContext.ChangeTracker.Clear();
            return;
        }

        context.InventoryProfiles.Local.Clear();
        context.InventoryLocations.Local.Clear();
        context.InventoryLots.Local.Clear();
        context.InventoryMovements.Local.Clear();
        context.InventoryStockLevels.Local.Clear();
        context.Activities.Local.Clear();
        context.AuditLogs.Local.Clear();
    }
}
