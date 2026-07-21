using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Infrastructure.Persistence.Conversions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class RoleRecordConfiguration : IEntityTypeConfiguration<RoleRecord>
{
    public void Configure(EntityTypeBuilder<RoleRecord> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(record => record.OrganizationId)
            .HasColumnName("organization_id")
            .IsRequired();

        builder.Property(record => record.Name)
            .HasColumnName("name")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(record => record.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(record => record.CreatedAtUtc)
            .HasColumnName("created_at_utc_ticks")
            .HasConversion<DateTimeOffsetToTicksConverter>()
            .IsRequired();

        builder.HasIndex(record => record.OrganizationId);
        builder.HasIndex(record => record.IsActive);

        builder.HasOne<OrganizationRecord>()
            .WithMany()
            .HasForeignKey(record => record.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(record => record.Permissions)
            .WithOne(permission => permission.Role)
            .HasForeignKey(permission => permission.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
