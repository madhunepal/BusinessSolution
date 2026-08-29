using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;
using SmallBusiness.Domain.Enums;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.JobNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => new { x.BusinessId, x.JobNumber })
            .IsUnique();

        builder.Property(x => x.CustomerNameSnapshot)
            .HasMaxLength(200);

        builder.Property(x => x.CustomerPhoneSnapshot)
            .HasMaxLength(50);

        builder.Property(x => x.CustomerEmailSnapshot)
            .HasMaxLength(200);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(4000);
            
        builder.Property(x => x.CompletionNotes)
            .HasMaxLength(4000);

        builder.Property(x => x.ServiceStreet).HasMaxLength(200);
        builder.Property(x => x.ServiceCity).HasMaxLength(100);
        builder.Property(x => x.ServiceState).HasMaxLength(50);
        builder.Property(x => x.ServicePostalCode).HasMaxLength(20);
        builder.Property(x => x.ServiceCountry).HasMaxLength(100);
        builder.Property(x => x.AccessInstructions).HasMaxLength(1000);

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SalesOrder)
            .WithMany()
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Enforce 1 active Job per SalesOrder at the DB level
        // SQLite uses INTEGER for Enums. JobStatus.Cancelled = 4.
        builder.HasIndex(x => x.SalesOrderId)
            .IsUnique()
            .HasFilter($"[SalesOrderId] IS NOT NULL AND [Status] != {(int)JobStatus.Cancelled}");

        builder.HasMany(x => x.Tasks)
            .WithOne(x => x.Job)
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
