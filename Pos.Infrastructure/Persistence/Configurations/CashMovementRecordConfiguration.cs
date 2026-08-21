using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pos.Domain.CashMovements;
using Pos.Infrastructure.Persistence.Conversions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class CashMovementRecordConfiguration : IEntityTypeConfiguration<CashMovementRecord>
{
    public void Configure(EntityTypeBuilder<CashMovementRecord> builder)
    {
        builder.ToTable("cash_movements");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(record => record.RegisterSessionId)
            .HasColumnName("register_session_id")
            .IsRequired();

        builder.Property(record => record.ActorUserId)
            .HasColumnName("actor_user_id")
            .IsRequired();

        builder.Property(record => record.Type)
            .HasColumnName("movement_type")
            .HasConversion<EnumToStringConverter<CashMovementType>>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(record => record.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(record => record.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(record => record.Reason)
            .HasColumnName("reason")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(record => record.CreatedAtUtc)
            .HasColumnName("created_at_utc_ticks")
            .HasConversion<DateTimeOffsetToTicksConverter>()
            .IsRequired();

        builder.HasIndex(record => record.RegisterSessionId);
        builder.HasIndex(record => record.ActorUserId);
        builder.HasIndex(record => record.CreatedAtUtc);

        // Restrict en ambas FK: un CashMovement es un hecho histórico inmutable (sección 5/30 de la
        // tarea) que nunca debe desaparecer en cascada porque su RegisterSession o su UserId se
        // eliminen; ningún flujo actual borra RegisterSession/User físicamente, pero la restricción
        // documenta la invariante igual que RegisterSessionRecordConfiguration/
        // InventoryMovementRecordConfiguration.
        builder.HasOne<RegisterSessionRecord>()
            .WithMany()
            .HasForeignKey(record => record.RegisterSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UserRecord>()
            .WithMany()
            .HasForeignKey(record => record.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
