using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pos.Domain.RegisterSessions;
using Pos.Infrastructure.Persistence.Conversions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class RegisterSessionRecordConfiguration : IEntityTypeConfiguration<RegisterSessionRecord>
{
    public void Configure(EntityTypeBuilder<RegisterSessionRecord> builder)
    {
        builder.ToTable("register_sessions");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(record => record.RegisterId)
            .HasColumnName("register_id")
            .IsRequired();

        builder.Property(record => record.OpenedByUserId)
            .HasColumnName("opened_by_user_id")
            .IsRequired();

        builder.Property(record => record.ClosedByUserId)
            .HasColumnName("closed_by_user_id");

        builder.Property(record => record.OpeningFloatAmount)
            .HasColumnName("opening_float_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(record => record.OpeningFloatCurrency)
            .HasColumnName("opening_float_currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(record => record.ExpectedCashAmount)
            .HasColumnName("expected_cash_amount")
            .HasPrecision(18, 2);

        builder.Property(record => record.ExpectedCashCurrency)
            .HasColumnName("expected_cash_currency")
            .HasMaxLength(3);

        builder.Property(record => record.CountedCashAmount)
            .HasColumnName("counted_cash_amount")
            .HasPrecision(18, 2);

        builder.Property(record => record.CountedCashCurrency)
            .HasColumnName("counted_cash_currency")
            .HasMaxLength(3);

        builder.Property(record => record.CashDifferenceAmount)
            .HasColumnName("cash_difference_amount")
            .HasPrecision(18, 2);

        builder.Property(record => record.CashDifferenceCurrency)
            .HasColumnName("cash_difference_currency")
            .HasMaxLength(3);

        builder.Property(record => record.Status)
            .HasColumnName("status")
            .HasConversion<EnumToStringConverter<RegisterSessionStatus>>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(record => record.OpenedAtUtc)
            .HasColumnName("opened_at_utc_ticks")
            .HasConversion<DateTimeOffsetToTicksConverter>()
            .IsRequired();

        builder.Property(record => record.ClosedAtUtc)
            .HasColumnName("closed_at_utc_ticks")
            .HasConversion<DateTimeOffsetToTicksConverter>();

        builder.HasIndex(record => record.RegisterId);
        builder.HasIndex(record => record.OpenedByUserId);
        builder.HasIndex(record => record.ClosedByUserId);
        builder.HasIndex(record => record.Status);
        builder.HasIndex(record => record.OpenedAtUtc);
        builder.HasIndex(record => record.ClosedAtUtc);

        builder.HasOne<RegisterRecord>()
            .WithMany()
            .HasForeignKey(record => record.RegisterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UserRecord>()
            .WithMany()
            .HasForeignKey(record => record.OpenedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UserRecord>()
            .WithMany()
            .HasForeignKey(record => record.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
