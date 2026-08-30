using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class InventoryStockLevelConfiguration : IEntityTypeConfiguration<InventoryStockLevel>
{
    public void Configure(EntityTypeBuilder<InventoryStockLevel> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.InventoryProfile)
            .WithMany()
            .HasForeignKey(e => e.InventoryProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.InventoryLocation)
            .WithMany()
            .HasForeignKey(e => e.InventoryLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.InventoryLot)
            .WithMany()
            .HasForeignKey(e => e.InventoryLotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.QuantityOnHand).HasPrecision(18, 4);
        
        builder.Property(e => e.RowVersion)
            .IsRowVersion()
            .IsRequired();

        // Enforce uniqueness for one stock bucket per Business + Profile + Location + Lot
        // Since LotId can be null, we need two filtered indexes for SQL Server.
        
        builder.HasIndex(e => new { e.BusinessId, e.InventoryProfileId, e.InventoryLocationId, e.InventoryLotId })
            .IsUnique()
            .HasFilter("[InventoryLotId] IS NOT NULL");

        builder.HasIndex(e => new { e.BusinessId, e.InventoryProfileId, e.InventoryLocationId })
            .IsUnique()
            .HasFilter("[InventoryLotId] IS NULL");
    }
}
