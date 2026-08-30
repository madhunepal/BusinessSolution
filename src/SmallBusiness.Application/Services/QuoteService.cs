using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SmallBusiness.Application.Common.Models;
using SmallBusiness.Application.DTOs.Quotes;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Application.Services;

public class QuoteService : IQuoteService
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantSequenceService _sequenceService;
    private readonly IPermissionService? _permissionService;

    public QuoteService(
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

    public async Task<Guid> CreateQuoteAsync(CreateQuoteRequest request)
    {
        await EnsurePermissionAsync("Quotes.Create");
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);

        var businessId = _tenantContext.CurrentBusinessId 
            ?? throw new UnauthorizedAccessException("No active business context.");

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId)
            ?? throw new ValidationException("Invalid Customer.");

        if (request.ExpirationDate.HasValue && request.ExpirationDate.Value.Date < request.QuoteDate.Date)
        {
            throw new ValidationException("Expiration date cannot be before Quote date.");
        }

        var quoteNumber = await _sequenceService.GetNextQuoteNumberAsync();

        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            QuoteNumber = quoteNumber,
            CustomerId = customer.Id,
            CustomerNumberSnapshot = customer.CustomerNumber,
            CustomerNameSnapshot = customer.Name,
            CustomerEmailSnapshot = customer.Email,
            CustomerPhoneSnapshot = customer.PhoneNumber,
            QuoteDate = request.QuoteDate,
            ExpirationDate = request.ExpirationDate,
            Status = QuoteStatus.Draft,
            Notes = request.Notes,
            TaxRate = request.TaxRate,
            DiscountAmount = request.DiscountAmount
        };

        await PopulateAndCalculateLinesAsync(quote, request.Lines);

        _context.Quotes.Add(quote);
        
        LogActivity(quote.Id, businessId, ActivityType.Created, $"Quote {quote.QuoteNumber} created in Draft.");

        await _context.SaveChangesAsync();

        return quote.Id;
    }

    public async Task UpdateDraftQuoteAsync(Guid id, UpdateQuoteRequest request)
    {
        await EnsurePermissionAsync("Quotes.Edit");
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);

        var quote = await _context.Quotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == id)
            ?? throw new KeyNotFoundException($"Quote {id} not found.");

        if (quote.Status != QuoteStatus.Draft)
        {
            throw new ValidationException("Only Draft quotes can be edited.");
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId)
            ?? throw new ValidationException("Invalid Customer.");

        if (request.ExpirationDate.HasValue && request.ExpirationDate.Value.Date < request.QuoteDate.Date)
        {
            throw new ValidationException("Expiration date cannot be before Quote date.");
        }

        quote.CustomerId = customer.Id;
        quote.CustomerNumberSnapshot = customer.CustomerNumber;
        quote.CustomerNameSnapshot = customer.Name;
        quote.CustomerEmailSnapshot = customer.Email;
        quote.CustomerPhoneSnapshot = customer.PhoneNumber;
        quote.QuoteDate = request.QuoteDate;
        quote.ExpirationDate = request.ExpirationDate;
        quote.Notes = request.Notes;
        quote.TaxRate = request.TaxRate;
        quote.DiscountAmount = request.DiscountAmount;

        // Rebuild lines entirely for V1 simplicity
        _context.QuoteLines.RemoveRange(quote.Lines);
        quote.Lines.Clear();

        await PopulateAndCalculateLinesAsync(quote, request.Lines);

        LogActivity(quote.Id, quote.BusinessId, ActivityType.Updated, $"Quote {quote.QuoteNumber} updated.");

        await _context.SaveChangesAsync();
    }

    public async Task<QuoteDto> GetQuoteAsync(Guid id)
    {
        await EnsurePermissionAsync("Quotes.View");
        var quote = await _context.Quotes
            .Include(q => q.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id)
            ?? throw new KeyNotFoundException($"Quote {id} not found.");

        return MapToDto(quote);
    }

    public async Task<PagedResult<QuoteDto>> GetQuotesAsync(QuoteSearchRequest request)
    {
        await EnsurePermissionAsync("Quotes.View");
        var query = _context.Quotes
            .AsNoTracking()
            .AsQueryable();

        if (request.CustomerId.HasValue)
        {
            query = query.Where(q => q.CustomerId == request.CustomerId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(q => q.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var term = $"%{request.Query}%";
            query = query.Where(q => 
                EF.Functions.Like(q.QuoteNumber, term) ||
                EF.Functions.Like(q.CustomerNameSnapshot, term)
            );
        }

        var totalCount = await query.CountAsync();

        var quotes = await query
            .OrderByDescending(q => q.QuoteDate)
            .ThenByDescending(q => q.QuoteNumber)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var dtos = quotes.Select(MapToDto).ToList();

        return new PagedResult<QuoteDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task ChangeQuoteStatusAsync(Guid id, QuoteStatus newStatus)
    {
        await EnsurePermissionAsync(newStatus == QuoteStatus.Accepted ? "Quotes.Approve" : "Quotes.Edit");
        var quote = await _context.Quotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == id)
            ?? throw new KeyNotFoundException($"Quote {id} not found.");

        ValidateStatusTransition(quote, newStatus);

        quote.Status = newStatus;
        
        var now = DateTime.UtcNow;
        switch (newStatus)
        {
            case QuoteStatus.Sent:
                quote.SentAt = now;
                break;
            case QuoteStatus.Accepted:
                quote.AcceptedAt = now;
                break;
            case QuoteStatus.Rejected:
                quote.RejectedAt = now;
                break;
            case QuoteStatus.Cancelled:
                quote.CancelledAt = now;
                break;
        }

        LogActivity(quote.Id, quote.BusinessId, ActivityType.StatusChanged, $"Quote {quote.QuoteNumber} status changed to {newStatus}.");

        await _context.SaveChangesAsync();
    }

    private async Task PopulateAndCalculateLinesAsync(Quote quote, List<QuoteLineRequest> lineRequests)
    {
        quote.Subtotal = 0;
        decimal taxableSubtotal = 0;
        
        int sortOrder = 0;

        foreach (var lr in lineRequests)
        {
            Validator.ValidateObject(lr, new ValidationContext(lr), validateAllProperties: true);

            var line = new QuoteLine
            {
                Id = Guid.NewGuid(),
                QuoteId = quote.Id,
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
            
            quote.Subtotal += line.LineTotal;
            if (line.Taxable)
            {
                taxableSubtotal += line.LineTotal;
            }
            
            quote.Lines.Add(line);
        }

        if (quote.DiscountAmount > quote.Subtotal)
        {
            throw new ValidationException("Discount cannot exceed subtotal.");
        }

        quote.TaxAmount = Math.Round(taxableSubtotal * (quote.TaxRate / 100m), 2, MidpointRounding.AwayFromZero);
        quote.Total = Math.Round(quote.Subtotal - quote.DiscountAmount + quote.TaxAmount, 2, MidpointRounding.AwayFromZero);
    }

    private void ValidateStatusTransition(Quote quote, QuoteStatus newStatus)
    {
        var currentStatus = quote.Status;

        if (currentStatus == newStatus) return;

        bool isValid = false;

        switch (currentStatus)
        {
            case QuoteStatus.Draft:
                isValid = newStatus == QuoteStatus.Sent || newStatus == QuoteStatus.Cancelled;
                if (newStatus == QuoteStatus.Sent && !quote.Lines.Any())
                {
                    throw new ValidationException("Cannot send a quote without line items.");
                }
                break;
            case QuoteStatus.Sent:
                isValid = newStatus == QuoteStatus.Accepted || newStatus == QuoteStatus.Rejected || newStatus == QuoteStatus.Cancelled;
                
                if (newStatus == QuoteStatus.Accepted && quote.ExpirationDate.HasValue && quote.ExpirationDate.Value.Date < DateTime.UtcNow.Date)
                {
                    throw new ValidationException("Expired quotes cannot be accepted.");
                }
                break;
            case QuoteStatus.Accepted:
            case QuoteStatus.Rejected:
            case QuoteStatus.Cancelled:
                isValid = false; // Terminal states
                break;
        }

        if (!isValid)
        {
            throw new ValidationException($"Cannot transition quote from {currentStatus} to {newStatus}.");
        }
    }

    private void LogActivity(Guid entityId, Guid businessId, ActivityType type, string description)
    {
        _context.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityType = "Quote",
            EntityId = entityId,
            ActivityType = type,
            Description = description,
            CreatedBy = _tenantContext.UserId ?? "System"
        });
    }

    private static QuoteDto MapToDto(Quote q)
    {
        return new QuoteDto
        {
            Id = q.Id,
            BusinessId = q.BusinessId,
            QuoteNumber = q.QuoteNumber,
            CustomerId = q.CustomerId,
            CustomerNumberSnapshot = q.CustomerNumberSnapshot,
            CustomerNameSnapshot = q.CustomerNameSnapshot,
            CustomerEmailSnapshot = q.CustomerEmailSnapshot,
            CustomerPhoneSnapshot = q.CustomerPhoneSnapshot,
            QuoteDate = q.QuoteDate,
            ExpirationDate = q.ExpirationDate,
            Status = q.Status,
            Notes = q.Notes,
            TaxRate = q.TaxRate,
            Subtotal = q.Subtotal,
            DiscountAmount = q.DiscountAmount,
            TaxAmount = q.TaxAmount,
            Total = q.Total,
            SentAt = q.SentAt,
            AcceptedAt = q.AcceptedAt,
            RejectedAt = q.RejectedAt,
            CancelledAt = q.CancelledAt,
            CreatedAt = q.CreatedAt,
            UpdatedAt = q.UpdatedAt,
            Lines = q.Lines.OrderBy(l => l.SortOrder).Select(MapToDto).ToList()
        };
    }

    private static QuoteLineDto MapToDto(QuoteLine l)
    {
        return new QuoteLineDto
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
            LineTotal = l.LineTotal,
            SortOrder = l.SortOrder
        };
    }

    private Task EnsurePermissionAsync(string permission) =>
        _permissionService?.EnsurePermissionAsync(permission) ?? Task.CompletedTask;
}
