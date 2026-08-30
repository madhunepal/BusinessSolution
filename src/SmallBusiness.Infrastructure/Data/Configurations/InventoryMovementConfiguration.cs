using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
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

        builder.Property(e => e.Quantity).HasPrecision(18, 4);
        builder.Property(e => e.UnitCost).HasPrecision(18, 4);

        builder.Property(e => e.ReferenceType).HasMaxLength(100);
        builder.Property(e => e.Reason).HasMaxLength(255);
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
    }
}
