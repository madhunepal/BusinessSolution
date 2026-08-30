using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmallBusiness.Application.Interfaces;
using SmallBusiness.Domain.Common;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Infrastructure.Identity;

namespace SmallBusiness.Infrastructure.Data;

/// <summary>
/// Primary database context. Extends IdentityDbContext for ASP.NET Core Identity
/// and implements IApplicationDbContext for the Application layer.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    private readonly ITenantContext? _tenantContext;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<BusinessUser> BusinessUsers => Set<BusinessUser>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<TenantSequence> TenantSequences => Set<TenantSequence>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteLine> QuoteLines => Set<QuoteLine>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobTask> JobTasks => Set<JobTask>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentAssignment> AppointmentAssignments => Set<AppointmentAssignment>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<InventoryProfile> InventoryProfiles => Set<InventoryProfile>();
    public DbSet<InventoryLocation> InventoryLocations => Set<InventoryLocation>();
    public DbSet<InventoryLot> InventoryLots => Set<InventoryLot>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<InventoryStockLevel> InventoryStockLevels => Set<InventoryStockLevel>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply all IEntityTypeConfiguration<T> from this assembly
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Global query filter for tenant isolation on all IHasBusinessId entities
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(IHasBusinessId).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(ApplyTenantFilter),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);

                method.Invoke(this, [builder]);
            }
        }
    }

    private bool IsCrossTenantAdmin => _tenantContext?.IsCrossTenantAdmin ?? false;
    private Guid? CurrentTenantId => _tenantContext?.CurrentBusinessId;
    private bool HasTenantContext => _tenantContext != null;

    private void ApplyTenantFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, IHasBusinessId
    {
        builder.Entity<TEntity>().HasQueryFilter(
            e => HasTenantContext && (
                 IsCrossTenantAdmin || 
                 (CurrentTenantId != null && e.BusinessId == CurrentTenantId)));
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Automatically set timestamps on tracked entities
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    if (entry.Entity.Id == Guid.Empty)
                    {
                        entry.Entity.Id = Guid.NewGuid();
                    }
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
