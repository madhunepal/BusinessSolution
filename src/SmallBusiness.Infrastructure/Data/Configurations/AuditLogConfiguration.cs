using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityType)
            .HasMaxLength(100);

        builder.Property(a => a.UserId)
            .HasMaxLength(450);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45);

        // Index for querying audit logs by entity
        builder.HasIndex(a => new { a.EntityType, a.EntityId });

        // Index for querying audit logs by user
        builder.HasIndex(a => a.UserId);

        // Index for querying audit logs by timestamp
        builder.HasIndex(a => a.Timestamp);
    }
}
