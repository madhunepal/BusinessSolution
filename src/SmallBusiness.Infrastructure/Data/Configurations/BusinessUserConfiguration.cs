using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class BusinessUserConfiguration : IEntityTypeConfiguration<BusinessUser>
{
    public void Configure(EntityTypeBuilder<BusinessUser> builder)
    {
        builder.ToTable("BusinessUsers");

        builder.HasKey(bu => bu.Id);

        builder.Property(bu => bu.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(bu => bu.Role)
            .IsRequired()
            .HasMaxLength(100);

        // A user can only belong to a business once
        builder.HasIndex(bu => new { bu.BusinessId, bu.UserId })
            .IsUnique();

        // Index for looking up businesses by user
        builder.HasIndex(bu => bu.UserId);
    }
}
