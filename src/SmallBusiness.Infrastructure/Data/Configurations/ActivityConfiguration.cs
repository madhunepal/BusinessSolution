using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("Activities");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.ActivityType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(a => a.Description)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(a => a.EntityType)
            .HasMaxLength(100);

        builder.Property(a => a.CreatedBy)
            .HasMaxLength(450);

        // Index for querying activities by entity (polymorphic lookup)
        builder.HasIndex(a => new { a.EntityType, a.EntityId });

        // Index for querying activities by business (timeline)
        builder.HasIndex(a => new { a.BusinessId, a.CreatedAt });
    }
}
