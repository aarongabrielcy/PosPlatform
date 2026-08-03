using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pos.Domain.ProductAudit;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class ProductAuditChangeRecordConfiguration : IEntityTypeConfiguration<ProductAuditChangeRecord>
{
    public void Configure(EntityTypeBuilder<ProductAuditChangeRecord> builder)
    {
        builder.ToTable("product_audit_changes");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(record => record.ProductAuditEventId)
            .HasColumnName("product_audit_event_id")
            .IsRequired();

        builder.Property(record => record.FieldName)
            .HasColumnName("field_name")
            .HasConversion<EnumToStringConverter<ProductAuditField>>()
            .HasMaxLength(32)
            .IsRequired();

        // 500 = mismo límite que Product.Description (el campo auditable más largo).
        builder.Property(record => record.OldValue)
            .HasColumnName("old_value")
            .HasMaxLength(500);

        builder.Property(record => record.NewValue)
            .HasColumnName("new_value")
            .HasMaxLength(500);

        builder.HasIndex(record => record.ProductAuditEventId);
    }
}
