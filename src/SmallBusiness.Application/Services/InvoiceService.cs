using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SmallBusiness.Application.DTOs.Invoices;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Application.Services;

public class InvoiceService : IInvoiceService
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantSequenceService _sequenceService;

    public InvoiceService(IApplicationDbContext context, ITenantContext tenantContext, ITenantSequenceService sequenceService)
    {
        _context = context;
        _tenantContext = tenantContext;
        _sequenceService = sequenceService;
    }

    public async Task<InvoiceDto> CreateInvoiceFromSalesOrderAsync(Guid salesOrderId)
    {
        var businessId = _tenantContext.CurrentBusinessId ?? throw new UnauthorizedAccessException("Business context required.");

        var salesOrder = await _context.SalesOrders
            .Include(so => so.Lines)
            .FirstOrDefaultAsync(so => so.Id == salesOrderId && so.BusinessId == businessId)
            ?? throw new KeyNotFoundException("Sales Order not found.");

        if (salesOrder.Status != SalesOrderStatus.Completed)
        {
            throw new ValidationException("Cannot create an invoice from a Sales Order that is not Completed.");
        }

        // Duplicate protection
        var existingInvoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.SalesOrderId == salesOrderId && i.BusinessId == businessId && i.Status != InvoiceStatus.Void);

        if (existingInvoice != null)
        {
            throw new ValidationException($"An active invoice ({existingInvoice.InvoiceNumber}) already exists for this Sales Order.");
        }

        // Generate Number
        var invoiceNumber = await _sequenceService.GetNextInvoiceNumberAsync();

        // Copy snapshot
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            InvoiceNumber = invoiceNumber,
            SalesOrderId = salesOrderId,
            CustomerId = salesOrder.CustomerId,
            CustomerNumberSnapshot = salesOrder.CustomerNumberSnapshot,
            CustomerNameSnapshot = salesOrder.CustomerNameSnapshot,
            CustomerEmailSnapshot = salesOrder.CustomerEmailSnapshot,
            CustomerPhoneSnapshot = salesOrder.CustomerPhoneSnapshot,
            // Billing address snapshot could go here if we had it on SalesOrder, 
            // but we will pull it from current Customer if not on SalesOrder snapshot, wait rule says: 
            // "When Invoice is created from SalesOrder, copy the SalesOrder customer snapshot. Do not re-read current Customer values"
            // We'll leave the billing address empty if it's not on SalesOrder, or map it. Wait, SalesOrder doesn't have billing address snapshot.
            // Let's just copy what we have.
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)), // Default 15 days
            Terms = "Net 15",
            Status = InvoiceStatus.Draft,
            TaxRate = salesOrder.TaxRate,
            DiscountAmount = salesOrder.DiscountAmount
        };

        decimal subtotal = 0;
        foreach (var sol in salesOrder.Lines)
        {
            var lineTotal = Math.Round(sol.Quantity * sol.UnitPrice, 2, MidpointRounding.AwayFromZero);
            var il = new InvoiceLine
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                SalesOrderLineId = sol.Id,
                ItemCode = sol.ItemCode,
                Name = sol.Name,
                Description = sol.Description,
                Unit = sol.Unit,
                Quantity = sol.Quantity,
                UnitPrice = sol.UnitPrice,
                Taxable = sol.Taxable,
                LineTotal = lineTotal,
                SortOrder = sol.SortOrder
            };
            subtotal += lineTotal;
            invoice.Lines.Add(il);
        }

        invoice.Subtotal = subtotal;

        var taxableSubtotal = invoice.Lines.Where(l => l.Taxable).Sum(l => l.LineTotal);
        var taxAmount = Math.Round(taxableSubtotal * (invoice.TaxRate / 100m), 2, MidpointRounding.AwayFromZero);
        
        invoice.TaxAmount = taxAmount;
        invoice.Total = invoice.Subtotal - invoice.DiscountAmount + invoice.TaxAmount;

        invoice.AmountPaid = 0;
        invoice.BalanceDue = invoice.Total;

        // Verify equality
        if (invoice.Subtotal != salesOrder.Subtotal || 
            invoice.TaxAmount != salesOrder.TaxAmount || 
            invoice.Total != salesOrder.Total)
        {
            throw new InvalidOperationException("Invoice deterministic calculations do not match the Sales Order financial snapshot. Integrity violation.");
        }

        _context.Invoices.Add(invoice);

        _context.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityId = invoice.Id,
            EntityType = "Invoice",
            ActivityType = ActivityType.Created,
            Description = $"Invoice {invoice.InvoiceNumber} created from Sales Order {salesOrder.SalesOrderNumber}.",
            CreatedBy = _tenantContext.UserId
        });

        _context.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityId = salesOrder.Id,
            EntityType = "SalesOrder",
            ActivityType = ActivityType.Converted,
            Description = $"Sales Order converted to Invoice {invoice.InvoiceNumber}.",
            CreatedBy = _tenantContext.UserId
        });

        await _context.SaveChangesAsync();

        return MapToDto(invoice, salesOrder.SalesOrderNumber);
    }

    public async Task<InvoiceDto> UpdateDraftInvoiceMetadataAsync(UpdateInvoiceMetadataRequest request)
    {
        var businessId = _tenantContext.CurrentBusinessId ?? throw new UnauthorizedAccessException();
        
        var invoice = await _context.Invoices
            .Include(i => i.Lines)
            .Include(i => i.SalesOrder)
            .FirstOrDefaultAsync(i => i.Id == request.Id && i.BusinessId == businessId)
            ?? throw new KeyNotFoundException("Invoice not found.");

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new ValidationException("Only Draft invoices can be edited.");
        }

        if (request.DueDate < invoice.InvoiceDate)
        {
            throw new ValidationException("DueDate cannot be earlier than InvoiceDate.");
        }

        invoice.DueDate = request.DueDate;
        invoice.Terms = request.Terms;
        invoice.Notes = request.Notes;

        _context.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityId = invoice.Id,
            EntityType = "Invoice",
            ActivityType = ActivityType.Updated,
            Description = "Invoice metadata updated.",
            CreatedBy = _tenantContext.UserId
        });

        await _context.SaveChangesAsync();
        return MapToDto(invoice, invoice.SalesOrder?.SalesOrderNumber ?? "");
    }

    public async Task SendInvoiceAsync(Guid id)
    {
        var businessId = _tenantContext.CurrentBusinessId ?? throw new UnauthorizedAccessException();
        
        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.Id == id && i.BusinessId == businessId)
            ?? throw new KeyNotFoundException("Invoice not found.");

        if (invoice.Status != InvoiceStatus.Draft)
            throw new ValidationException("Only Draft invoices can be sent.");

        invoice.Status = InvoiceStatus.Sent;
        invoice.SentAt = DateTimeOffset.UtcNow;

        _context.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityId = invoice.Id,
            EntityType = "Invoice",
            ActivityType = ActivityType.StatusChanged,
            Description = $"Invoice {invoice.InvoiceNumber} marked as Sent.",
            CreatedBy = _tenantContext.UserId
        });

        await _context.SaveChangesAsync();
    }

    public async Task VoidInvoiceAsync(Guid id)
    {
        var businessId = _tenantContext.CurrentBusinessId ?? throw new UnauthorizedAccessException();
        
        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.Id == id && i.BusinessId == businessId)
            ?? throw new KeyNotFoundException("Invoice not found.");

        if (invoice.Status == InvoiceStatus.Void)
            throw new ValidationException("Invoice is already voided.");

        invoice.Status = InvoiceStatus.Void;
        invoice.VoidedAt = DateTimeOffset.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Action = "Void Invoice",
            EntityType = "Invoice",
            EntityId = invoice.Id,
            UserId = _tenantContext.UserId
        });

        _context.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            EntityId = invoice.Id,
            EntityType = "Invoice",
            ActivityType = ActivityType.StatusChanged,
            Description = $"Invoice {invoice.InvoiceNumber} was voided.",
            CreatedBy = _tenantContext.UserId
        });

        await _context.SaveChangesAsync();
    }

    public async Task<InvoiceDto> GetInvoiceAsync(Guid id)
    {
        var businessId = _tenantContext.CurrentBusinessId ?? throw new UnauthorizedAccessException();
        
        var invoice = await _context.Invoices
            .Include(i => i.Lines)
            .Include(i => i.SalesOrder)
            .FirstOrDefaultAsync(i => i.Id == id && i.BusinessId == businessId)
            ?? throw new KeyNotFoundException("Invoice not found.");

        return MapToDto(invoice, invoice.SalesOrder?.SalesOrderNumber ?? "");
    }

    public async Task<List<InvoiceDto>> GetInvoicesAsync(InvoiceSearchRequest request)
    {
        var businessId = _tenantContext.CurrentBusinessId ?? throw new UnauthorizedAccessException();
        
        var query = _context.Invoices
            .Include(i => i.SalesOrder)
            .Where(i => i.BusinessId == businessId);

        if (request.Status.HasValue)
            query = query.Where(i => i.Status == request.Status.Value);
            
        if (request.CustomerId.HasValue)
            query = query.Where(i => i.CustomerId == request.CustomerId.Value);

        if (request.SalesOrderId.HasValue)
            query = query.Where(i => i.SalesOrderId == request.SalesOrderId.Value);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            query = query.Where(i => i.InvoiceNumber.Contains(kw) || i.CustomerNameSnapshot.Contains(kw));
        }

        if (request.StartDate.HasValue)
            query = query.Where(i => i.InvoiceDate >= request.StartDate.Value);

        if (request.EndDate.HasValue)
            query = query.Where(i => i.InvoiceDate <= request.EndDate.Value);

        var invoices = await query
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.InvoiceNumber)
            .ToListAsync();

        var result = invoices.Select(i => MapToDto(i, i.SalesOrder?.SalesOrderNumber ?? "")).ToList();

        if (request.OverdueOnly)
        {
            result = result.Where(i => i.IsOverdue).ToList();
        }

        return result;
    }

    private InvoiceDto MapToDto(Invoice invoice, string salesOrderNumber)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var isOverdue = invoice.Status == InvoiceStatus.Sent && invoice.DueDate < today && invoice.BalanceDue > 0;

        return new InvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            SalesOrderId = invoice.SalesOrderId,
            SalesOrderNumber = salesOrderNumber,
            CustomerName = invoice.CustomerNameSnapshot,
            CustomerNumber = invoice.CustomerNumberSnapshot,
            CustomerEmail = invoice.CustomerEmailSnapshot,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            Status = invoice.Status,
            IsOverdue = isOverdue,
            Terms = invoice.Terms,
            Notes = invoice.Notes,
            Subtotal = invoice.Subtotal,
            DiscountAmount = invoice.DiscountAmount,
            TaxAmount = invoice.TaxAmount,
            Total = invoice.Total,
            AmountPaid = invoice.AmountPaid,
            BalanceDue = invoice.BalanceDue,
            Lines = invoice.Lines?.Select(l => new InvoiceLineDto
            {
                Id = l.Id,
                ItemCode = l.ItemCode,
                Name = l.Name,
                Description = l.Description,
                Unit = l.Unit,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                Taxable = l.Taxable,
                LineTotal = l.LineTotal,
                SortOrder = l.SortOrder
            }).OrderBy(l => l.SortOrder).ToList() ?? new List<InvoiceLineDto>()
        };
    }
}
