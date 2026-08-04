using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Infrastructure.Persistence.Conversions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class AdministrativeNotificationRecipientRecordConfiguration
    : IEntityTypeConfiguration<AdministrativeNotificationRecipientRecord>
{
    public void Configure(EntityTypeBuilder<AdministrativeNotificationRecipientRecord> builder)
    {
        builder.ToTable("administrative_notification_recipients");

        // PK compuesta (NotificationId, UserId): no puede haber un destinatario duplicado por
        // Notification (TAREA 24E, sección 17).
        builder.HasKey(record => new { record.NotificationId, record.UserId });

        builder.Property(record => record.NotificationId)
            .HasColumnName("notification_id")
            .IsRequired();

        builder.Property(record => record.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(record => record.ReadAtUtc)
            .HasColumnName("read_at_utc_ticks")
            .HasConversion<DateTimeOffsetToTicksConverter>();

        // Bandeja de un User ordenada/filtrada por leído (TAREA 24E, sección 18).
        builder.HasIndex(record => new { record.UserId, record.ReadAtUtc });

        // No eliminar historial porque se elimine un User (TAREA 24E, sección 16): Restrict, nunca
        // Cascade.
        builder.HasOne<UserRecord>()
            .WithMany()
            .HasForeignKey(record => record.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
