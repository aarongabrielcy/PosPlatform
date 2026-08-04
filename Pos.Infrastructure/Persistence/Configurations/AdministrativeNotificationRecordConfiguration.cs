using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Infrastructure.Persistence.Conversions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class AdministrativeNotificationRecordConfiguration : IEntityTypeConfiguration<AdministrativeNotificationRecord>
{
    public void Configure(EntityTypeBuilder<AdministrativeNotificationRecord> builder)
    {
        builder.ToTable("administrative_notifications");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(record => record.OrganizationId)
            .HasColumnName("organization_id")
            .IsRequired();

        builder.Property(record => record.ProductAuditEventId)
            .HasColumnName("product_audit_event_id")
            .IsRequired();

        builder.Property(record => record.CreatedAtUtc)
            .HasColumnName("created_at_utc_ticks")
            .HasConversion<DateTimeOffsetToTicksConverter>()
            .IsRequired();

        // Módulo administrativo global ordenado por fecha (TAREA 24E, sección 18).
        builder.HasIndex(record => new { record.OrganizationId, record.CreatedAtUtc });

        // Máximo una Notification por ProductAuditEvent (TAREA 24E, sección 17).
        builder.HasIndex(record => record.ProductAuditEventId).IsUnique();

        // Append-only: nunca Cascade desde Organization/ProductAuditEvent hacia Notification
        // (TAREA 24E, sección 16), igual criterio que product_audit_events.
        builder.HasOne<OrganizationRecord>()
            .WithMany()
            .HasForeignKey(record => record.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProductAuditEventRecord>()
            .WithMany()
            .HasForeignKey(record => record.ProductAuditEventId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relación estructural Notification -> Recipients (composición, no referencia histórica):
        // Cascade es apropiado aquí porque las filas hijas no tienen sentido sin su Notification
        // padre, aunque no se exponga ninguna API de Delete (TAREA 24E, sección 16/19), igual
        // criterio que product_audit_events -> product_audit_changes.
        builder.HasMany(record => record.Recipients)
            .WithOne(recipient => recipient.Notification)
            .HasForeignKey(recipient => recipient.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
