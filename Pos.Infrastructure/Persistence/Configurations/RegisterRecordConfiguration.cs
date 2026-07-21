using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Infrastructure.Persistence.Conversions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class RegisterRecordConfiguration : IEntityTypeConfiguration<RegisterRecord>
{
    public void Configure(EntityTypeBuilder<RegisterRecord> builder)
    {
        builder.ToTable("registers");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(record => record.BranchId)
            .HasColumnName("branch_id")
            .IsRequired();

        builder.Property(record => record.Name)
            .HasColumnName("name")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(record => record.Code)
            .HasColumnName("code")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(record => record.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(record => record.CreatedAtUtc)
            .HasColumnName("created_at_utc_ticks")
            .HasConversion<DateTimeOffsetToTicksConverter>()
            .IsRequired();

        builder.HasIndex(record => new { record.BranchId, record.Name });
        builder.HasIndex(record => record.IsActive);

        builder.HasOne<BranchRecord>()
            .WithMany()
            .HasForeignKey(record => record.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
