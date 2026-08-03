using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pos.Domain.ProductAudit;
using Pos.Infrastructure.Persistence.Conversions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class ProductAuditEventRecordConfiguration : IEntityTypeConfiguration<ProductAuditEventRecord>
{
    public void Configure(EntityTypeBuilder<ProductAuditEventRecord> builder)
    {
        builder.ToTable("product_audit_events");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(record => record.OrganizationId)
            .HasColumnName("organization_id")
            .IsRequired();

        builder.Property(record => record.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.Property(record => record.ActorUserId)
            .HasColumnName("actor_user_id")
            .IsRequired();

        builder.Property(record => record.ActorUsernameSnapshot)
            .HasColumnName("actor_username_snapshot")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(record => record.ActorDisplayNameSnapshot)
            .HasColumnName("actor_display_name_snapshot")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(record => record.ProductSkuSnapshot)
            .HasColumnName("product_sku_snapshot")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(record => record.ProductNameSnapshot)
            .HasColumnName("product_name_snapshot")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(record => record.Action)
            .HasColumnName("action")
            .HasConversion<EnumToStringConverter<ProductAuditAction>>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(record => record.OccurredAtUtc)
            .HasColumnName("occurred_at_utc_ticks")
            .HasConversion<DateTimeOffsetToTicksConverter>()
            .IsRequired();

        // Índices pensados para los accesos reales (TAREA 24D, sección 18): módulo administrativo
        // global ordenado por fecha (OrganizationId+OccurredAtUtc), navegación directa desde
        // Productos (ProductId+OccurredAtUtc), filtro por usuario (ActorUserId+OccurredAtUtc) y
        // filtro por acción (Action+OccurredAtUtc). Sin índices adicionales sueltos: cada prefijo ya
        // queda cubierto por uno de estos compuestos.
        builder.HasIndex(record => new { record.OrganizationId, record.OccurredAtUtc });
        builder.HasIndex(record => new { record.ProductId, record.OccurredAtUtc });
        builder.HasIndex(record => new { record.ActorUserId, record.OccurredAtUtc });
        builder.HasIndex(record => new { record.Action, record.OccurredAtUtc });

        // Auditoría append-only: nunca Cascade desde Organization/Product/User hacia audit (TAREA
        // 24D, sección 17). Restrict impide eliminar una Organization/Product/User mientras exista
        // un evento de auditoría que lo referencie.
        builder.HasOne<OrganizationRecord>()
            .WithMany()
            .HasForeignKey(record => record.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProductRecord>()
            .WithMany()
            .HasForeignKey(record => record.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UserRecord>()
            .WithMany()
            .HasForeignKey(record => record.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relación estructural Event -> Changes (composición, no referencia histórica): Cascade es
        // apropiado aquí porque las filas hijas no tienen sentido sin su evento padre, aunque no se
        // exponga ninguna API de Delete a nivel de repositorio (TAREA 24D, sección 17/19).
        builder.HasMany(record => record.Changes)
            .WithOne(change => change.ProductAuditEvent)
            .HasForeignKey(change => change.ProductAuditEventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
