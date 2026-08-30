using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class InventoryLotConfiguration : IEntityTypeConfiguration<InventoryLot>
{
    public void Configure(EntityTypeBuilder<InventoryLot> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.InventoryProfile)
            .WithMany()
            .HasForeignKey(e => e.InventoryProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.LotNumber).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.UnitCost).HasPrecision(18, 4);
    }
}
