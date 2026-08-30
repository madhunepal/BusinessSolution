using SmallBusiness.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace SmallBusiness.Application.Interfaces;

/// <summary>
/// Application-facing database context interface.
/// Allows the Application layer to depend on an abstraction rather
/// than the concrete EF Core DbContext in Infrastructure.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Business> Businesses { get; }
    DbSet<BusinessUser> BusinessUsers { get; }
    DbSet<Activity> Activities { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Customer> Customers { get; }
    DbSet<CatalogItem> CatalogItems { get; }
    DbSet<TenantSequence> TenantSequences { get; }
    DbSet<Quote> Quotes { get; }
    DbSet<QuoteLine> QuoteLines { get; }
    DbSet<SalesOrder> SalesOrders { get; }
    DbSet<SalesOrderLine> SalesOrderLines { get; }
    DbSet<Job> Jobs { get; }
    DbSet<JobTask> JobTasks { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<AppointmentAssignment> AppointmentAssignments { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceLine> InvoiceLines { get; }
    DbSet<Payment> Payments { get; }
    DbSet<InventoryProfile> InventoryProfiles { get; }
    DbSet<InventoryLocation> InventoryLocations { get; }
    DbSet<InventoryLot> InventoryLots { get; }
    DbSet<InventoryMovement> InventoryMovements { get; }
    DbSet<InventoryStockLevel> InventoryStockLevels { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
