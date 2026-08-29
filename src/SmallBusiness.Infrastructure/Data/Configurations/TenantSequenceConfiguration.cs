using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class TenantSequenceConfiguration : IEntityTypeConfiguration<TenantSequence>
{
    public void Configure(EntityTypeBuilder<TenantSequence> builder)
    {
        builder.HasKey(e => new { e.BusinessId, e.EntityType });
        
        builder.Property(e => e.EntityType).HasMaxLength(100);
        
        builder.Property(e => e.RowVersion)
            .IsRowVersion()
            .IsRequired();
    }
}
