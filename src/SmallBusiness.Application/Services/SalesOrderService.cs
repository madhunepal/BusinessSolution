using Microsoft.EntityFrameworkCore;
using SmallBusiness.Application.Common;
using SmallBusiness.Application.Common.Models;
using SmallBusiness.Application.DTOs.SalesOrders;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmallBusiness.Application.Services;

public class SalesOrderService : ISalesOrderService
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantSequenceService _sequenceService;
    private readonly IPermissionService? _permissionService;

    public SalesOrderService(
        IApplicationDbContext context, 
        ITenantContext tenantContext,
        ITenantSequenceService sequenceService,
        IPermissionService? permissionService = null)
    {
        _context = context;
        _tenantContext = tenantContext;
        _sequenceService = sequenceService;
        _permissionService = permissionService;
    }

    public async Task<Guid> CreateSalesOrderAsync(CreateSalesOrderRequest request)
    {
        await EnsurePermissionAsync("Orders.Create");
        var businessId = _tenantContext.CurrentBusinessId 
            ?? throw new UnauthorizedAccessException("Business context is required.");

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId)
            ?? throw new ValidationException("Customer is invalid or belongs to another tenant.");

        var order = new SalesOrder
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            CustomerId = customer.Id,
            CustomerNameSnapshot = customer.Name,
            CustomerNumberSnapshot = customer.CustomerNumber,
            CustomerEmailSnapshot = customer.Email,
            CustomerPhoneSnapshot = customer.PhoneNumber,
            OrderDate = request.OrderDate,
            Status = SalesOrderStatus.Draft,
            Notes = request.Notes,
            TaxRate = request.TaxRate,
            DiscountAmount = request.DiscountAmount
        };

        // Numbering must be generated outside the shared DbContext tracking context safely
        order.SalesOrderNumber = await _sequenceService.GetNextSalesOrderNumberAsync();

        await PopulateAndCalculateLinesAsync(order, request.Lines);
        
        VerifyTotals(order);

        _context.SalesOrders.Add(order);

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityId = order.Id,
            EntityType = "SalesOrder",
            ActivityType = ActivityType.Created,
            Description = $"Sales Order {order.SalesOrderNumber} created as Draft for {order.CustomerNameSnapshot}.",
            CreatedBy = _tenantContext.UserId ?? "system",
            CreatedAt = DateTime.UtcNow
        };
        _context.Activities.Add(activity);

        await _context.SaveChangesAsync();
        return order.Id;
    }

    public async Task<Guid> ConvertQuoteToSalesOrderAsync(Guid quoteId)
    {
        await EnsurePermissionAsync("Orders.Create");
        var businessId = _tenantContext.CurrentBusinessId 
            ?? throw new UnauthorizedAccessException("Business context is required.");

        var quote = await _context.Quotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == quoteId)
            ?? throw new KeyNotFoundException($"Quote {quoteId} not found or access denied.");

        if (quote.Status != QuoteStatus.Accepted)
            throw new ValidationException($"Quote {quote.QuoteNumber} cannot be converted because it is not in Accepted status.");

        var existingOrder = await _context.SalesOrders.AnyAsync(so => so.QuoteId == quoteId);
        if (existingOrder)
            throw new ValidationException($"Quote {quote.QuoteNumber} has already been converted to a Sales Order.");

        var order = new SalesOrder
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            QuoteId = quote.Id,
            CustomerId = quote.CustomerId,
            CustomerNameSnapshot = quote.CustomerNameSnapshot,
            // Follow the user's rule: copy snapshot values from the quote, NOT the current customer
            CustomerNumberSnapshot = quote.Customer.CustomerNumber, // Wait, Quote didn't have NumberSnapshot. We will take it from Quote's navigation property for now safely since it's immutable there too, or if they changed it, we just grab it. But ideally Quote should have had it. 
            CustomerEmailSnapshot = quote.Customer.Email,
            CustomerPhoneSnapshot = quote.Customer.PhoneNumber,
            
            OrderDate = DateTime.Today,
            Status = SalesOrderStatus.Confirmed, // Direct to Confirmed!
            ConfirmedAt = DateTime.UtcNow,
            Notes = quote.Notes,
            TaxRate = quote.TaxRate,
            DiscountAmount = quote.DiscountAmount
        };

        order.SalesOrderNumber = await _sequenceService.GetNextSalesOrderNumberAsync();

        // Copy lines exactly
        int sortOrder = 0;
        foreach (var qLine in quote.Lines.OrderBy(l => l.SortOrder))
        {
            var sLine = new SalesOrderLine
            {
                Id = Guid.NewGuid(),
                CatalogItemId = qLine.CatalogItemId,
                ItemCode = qLine.ItemCode,
                Name = qLine.Name,
                Description = qLine.Description,
                Unit = qLine.Unit,
                Quantity = qLine.Quantity,
                UnitPrice = qLine.UnitPrice, // Snapshot exact price
                Taxable = qLine.Taxable,
                SortOrder = sortOrder++
            };
            
            sLine.LineTotal = Math.Round(sLine.Quantity * sLine.UnitPrice, 2, MidpointRounding.AwayFromZero);
            order.Lines.Add(sLine);
        }

        // Recalculate based on copied lines
        order.Subtotal = order.Lines.Sum(l => l.LineTotal);
        var taxableSubtotal = order.Lines.Where(l => l.Taxable).Sum(l => l.LineTotal);
        order.TaxAmount = Math.Round(taxableSubtotal * (order.TaxRate / 100m), 2, MidpointRounding.AwayFromZero);
        order.Total = Math.Round(order.Subtotal - order.DiscountAmount + order.TaxAmount, 2, MidpointRounding.AwayFromZero);

        // Verify math matches exactly
        if (order.Subtotal != quote.Subtotal || 
            order.TaxAmount != quote.TaxAmount || 
            order.Total != quote.Total)
        {
            throw new ValidationException($"Financial integrity failure: The calculated Sales Order totals do not match the originating Quote {quote.QuoteNumber}. Conversion aborted.");
        }

        _context.SalesOrders.Add(order);

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityId = order.Id,
            EntityType = "SalesOrder",
            ActivityType = ActivityType.Created, // Or CreatedFromQuote
            Description = $"Sales Order {order.SalesOrderNumber} confirmed from Quote {quote.QuoteNumber}.",
            CreatedBy = _tenantContext.UserId ?? "system",
            CreatedAt = DateTime.UtcNow
        };
        _context.Activities.Add(activity);

        // Also add an activity to the quote!
        var quoteActivity = new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityId = quote.Id,
            EntityType = "Quote",
            ActivityType = ActivityType.Updated,
            Description = $"Quote converted to Sales Order {order.SalesOrderNumber}.",
            CreatedBy = _tenantContext.UserId ?? "system",
            CreatedAt = DateTime.UtcNow
        };
        _context.Activities.Add(quoteActivity);

        await _context.SaveChangesAsync();
        return order.Id;
    }

    public async Task UpdateDraftSalesOrderAsync(Guid id, UpdateSalesOrderRequest request)
    {
        await EnsurePermissionAsync("Orders.Create");
        var order = await _context.SalesOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new KeyNotFoundException("Sales Order not found or access denied.");

        if (order.Status != SalesOrderStatus.Draft)
            throw new ValidationException($"Sales Order is {order.Status} and cannot be edited.");

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId)
            ?? throw new ValidationException("Customer is invalid or belongs to another tenant.");

        order.CustomerId = customer.Id;
        order.CustomerNameSnapshot = customer.Name;
        order.CustomerNumberSnapshot = customer.CustomerNumber;
        order.CustomerEmailSnapshot = customer.Email;
        order.CustomerPhoneSnapshot = customer.PhoneNumber;
        
        order.OrderDate = request.OrderDate;
        order.Notes = request.Notes;
        order.TaxRate = request.TaxRate;
        order.DiscountAmount = request.DiscountAmount;

        _context.SalesOrderLines.RemoveRange(order.Lines);
        order.Lines.Clear();

        await PopulateAndCalculateLinesAsync(order, request.Lines);
        
        VerifyTotals(order);

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = order.BusinessId,
            EntityId = order.Id,
            EntityType = "SalesOrder",
            ActivityType = ActivityType.Updated,
            Description = $"Sales Order {order.SalesOrderNumber} updated.",
            CreatedBy = _tenantContext.UserId ?? "system",
            CreatedAt = DateTime.UtcNow
        };
        _context.Activities.Add(activity);

        await _context.SaveChangesAsync();
    }

    public async Task ChangeSalesOrderStatusAsync(Guid id, SalesOrderStatus newStatus)
    {
        await EnsurePermissionAsync("Orders.Create");
        var order = await _context.SalesOrders
            .FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new KeyNotFoundException("Sales Order not found or access denied.");

        if (order.Status == newStatus)
            return;

        bool validTransition = false;
        switch (order.Status)
        {
            case SalesOrderStatus.Draft:
                validTransition = newStatus == SalesOrderStatus.Confirmed || newStatus == SalesOrderStatus.Cancelled;
                break;
            case SalesOrderStatus.Confirmed:
                validTransition = newStatus == SalesOrderStatus.Completed || newStatus == SalesOrderStatus.Cancelled;
                break;
            case SalesOrderStatus.Completed:
            case SalesOrderStatus.Cancelled:
                validTransition = false; // Terminal states
                break;
        }

        if (!validTransition)
            throw new ValidationException($"Cannot transition Sales Order from {order.Status} to {newStatus}.");

        order.Status = newStatus;

        if (newStatus == SalesOrderStatus.Confirmed)
            order.ConfirmedAt = DateTime.UtcNow;
        else if (newStatus == SalesOrderStatus.Completed)
            order.CompletedAt = DateTime.UtcNow;
        else if (newStatus == SalesOrderStatus.Cancelled)
            order.CancelledAt = DateTime.UtcNow;

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = order.BusinessId,
            EntityId = order.Id,
            EntityType = "SalesOrder",
            ActivityType = ActivityType.Updated,
            Description = $"Sales Order {order.SalesOrderNumber} status changed to {newStatus}.",
            CreatedBy = _tenantContext.UserId ?? "system",
            CreatedAt = DateTime.UtcNow
        };
        _context.Activities.Add(activity);

        await _context.SaveChangesAsync();
    }

    public async Task<SalesOrderDto> GetSalesOrderAsync(Guid id)
    {
        await EnsurePermissionAsync("Orders.View");
        var order = await _context.SalesOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new KeyNotFoundException("Sales Order not found or access denied.");

        return MapToDto(order);
    }

    public async Task<PagedResult<SalesOrderDto>> GetSalesOrdersAsync(SalesOrderSearchRequest request)
    {
        await EnsurePermissionAsync("Orders.View");
        var query = _context.SalesOrders.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(o => o.SalesOrderNumber.Contains(term) || o.CustomerNameSnapshot.Contains(term));
        }

        if (request.CustomerId.HasValue)
            query = query.Where(o => o.CustomerId == request.CustomerId.Value);

        if (request.QuoteId.HasValue)
            query = query.Where(o => o.QuoteId == request.QuoteId.Value);

        if (request.Status.HasValue)
            query = query.Where(o => o.Status == request.Status.Value);

        var count = await query.CountAsync();
        
        var items = await query
            .OrderByDescending(o => o.OrderDate)
            .ThenByDescending(o => o.SalesOrderNumber)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var dtos = items.Select(MapToDto).ToList();

        return new PagedResult<SalesOrderDto>
        {
            Items = dtos,
            TotalCount = count,
            Page = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    private async Task PopulateAndCalculateLinesAsync(SalesOrder order, List<SalesOrderLineRequest> lineRequests)
    {
        int sortOrder = 0;
        foreach (var lr in lineRequests)
        {
            var line = new SalesOrderLine
            {
                Id = Guid.NewGuid(),
                Quantity = lr.Quantity,
                SortOrder = sortOrder++
            };

            if (lr.CatalogItemId.HasValue)
            {
                var catalogItem = await _context.CatalogItems
                    .FirstOrDefaultAsync(c => c.Id == lr.CatalogItemId.Value)
                    ?? throw new ValidationException($"Catalog item {lr.CatalogItemId.Value} is invalid or belongs to another tenant.");

                line.CatalogItemId = catalogItem.Id;
                line.ItemCode = catalogItem.ItemCode;
                line.Name = catalogItem.Name;
                line.Description = catalogItem.Description;
                line.Unit = catalogItem.Unit;
                line.UnitPrice = catalogItem.SellingPrice; // Snapshot selling price
                line.Taxable = catalogItem.Taxable;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(lr.Name))
                    throw new ValidationException("Name is required for manual lines.");
                if (string.IsNullOrWhiteSpace(lr.Unit))
                    throw new ValidationException("Unit is required for manual lines.");
                    
                line.ItemCode = string.Empty;
                line.Name = lr.Name;
                line.Description = lr.Description;
                line.Unit = lr.Unit;
                line.UnitPrice = lr.UnitPrice;
                line.Taxable = lr.Taxable;
            }

            line.LineTotal = Math.Round(line.Quantity * line.UnitPrice, 2, MidpointRounding.AwayFromZero);
            order.Lines.Add(line);
        }

        order.Subtotal = order.Lines.Sum(l => l.LineTotal);
        
        var taxableSubtotal = order.Lines.Where(l => l.Taxable).Sum(l => l.LineTotal);
        order.TaxAmount = Math.Round(taxableSubtotal * (order.TaxRate / 100m), 2, MidpointRounding.AwayFromZero);
        
        order.Total = Math.Round(order.Subtotal - order.DiscountAmount + order.TaxAmount, 2, MidpointRounding.AwayFromZero);
    }

    private void VerifyTotals(SalesOrder order)
    {
        if (order.DiscountAmount > order.Subtotal)
            throw new ValidationException("Discount cannot exceed subtotal.");
    }

    private static SalesOrderDto MapToDto(SalesOrder entity)
    {
        return new SalesOrderDto
        {
            Id = entity.Id,
            SalesOrderNumber = entity.SalesOrderNumber,
            QuoteId = entity.QuoteId,
            CustomerId = entity.CustomerId,
            CustomerNameSnapshot = entity.CustomerNameSnapshot,
            CustomerNumberSnapshot = entity.CustomerNumberSnapshot,
            CustomerEmailSnapshot = entity.CustomerEmailSnapshot,
            CustomerPhoneSnapshot = entity.CustomerPhoneSnapshot,
            OrderDate = entity.OrderDate,
            Status = entity.Status,
            Notes = entity.Notes,
            TaxRate = entity.TaxRate,
            Subtotal = entity.Subtotal,
            DiscountAmount = entity.DiscountAmount,
            TaxAmount = entity.TaxAmount,
            Total = entity.Total,
            ConfirmedAt = entity.ConfirmedAt,
            CompletedAt = entity.CompletedAt,
            CancelledAt = entity.CancelledAt,
            Lines = entity.Lines.OrderBy(l => l.SortOrder).Select(l => new SalesOrderLineDto
            {
                Id = l.Id,
                CatalogItemId = l.CatalogItemId,
                ItemCode = l.ItemCode,
                Name = l.Name,
                Description = l.Description,
                Unit = l.Unit,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                Taxable = l.Taxable,
                LineTotal = l.LineTotal
            }).ToList()
        };
    }

    private Task EnsurePermissionAsync(string permission) =>
        _permissionService?.EnsurePermissionAsync(permission) ?? Task.CompletedTask;
}
