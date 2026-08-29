using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmallBusiness.Domain.Entities;

namespace SmallBusiness.Infrastructure.Data.Configurations;

public class JobTaskConfiguration : IEntityTypeConfiguration<JobTask>
{
    public void Configure(EntityTypeBuilder<JobTask> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(1000);

        // Optional relationship to the originating line item for traceability
        // We do not set up a full navigation property to SalesOrderLine to keep the domain simple,
        // but we index it for quick lookups if needed.
        builder.HasIndex(x => x.SalesOrderLineId);
    }
}
