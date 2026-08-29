using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.ToTable("Businesses");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(b => b.Phone).HasMaxLength(50);
        builder.Property(b => b.Email).HasMaxLength(200);
        builder.Property(b => b.Website).HasMaxLength(500);
        builder.Property(b => b.AddressLine1).HasMaxLength(200);
        builder.Property(b => b.AddressLine2).HasMaxLength(200);
        builder.Property(b => b.City).HasMaxLength(100);
        builder.Property(b => b.State).HasMaxLength(100);
        builder.Property(b => b.PostalCode).HasMaxLength(20);
        builder.Property(b => b.Country).HasMaxLength(100);
        builder.Property(b => b.TaxId).HasMaxLength(50);
        builder.Property(b => b.BusinessRegistrationNumber).HasMaxLength(100);

        builder.HasMany(b => b.BusinessUsers)
            .WithOne(bu => bu.Business)
            .HasForeignKey(bu => bu.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Activities)
            .WithOne(a => a.Business)
            .HasForeignKey(a => a.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
