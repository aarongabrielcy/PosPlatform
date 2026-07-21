using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class RolePermissionRecordConfiguration : IEntityTypeConfiguration<RolePermissionRecord>
{
    public void Configure(EntityTypeBuilder<RolePermissionRecord> builder)
    {
        builder.ToTable("role_permissions");

        builder.HasKey(record => new { record.RoleId, record.Permission });

        builder.Property(record => record.RoleId)
            .HasColumnName("role_id")
            .IsRequired();

        builder.Property(record => record.Permission)
            .HasColumnName("permission")
            .HasMaxLength(80)
            .IsRequired();

        builder.HasIndex(record => record.RoleId);
    }
}
