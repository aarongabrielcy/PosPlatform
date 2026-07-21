using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence.Conversions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class PaymentRecordConfiguration : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(record => record.SaleId)
            .HasColumnName("sale_id")
            .IsRequired();

        builder.Property(record => record.Method)
            .HasColumnName("payment_method")
            .HasConversion<EnumToStringConverter<PaymentMethod>>()
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

        builder.Property(record => record.PaidAtUtc)
            .HasColumnName("paid_at_utc_ticks")
            .HasConversion<DateTimeOffsetToTicksConverter>()
            .IsRequired();

        builder.HasIndex(record => record.SaleId);
        builder.HasIndex(record => record.Method);
        builder.HasIndex(record => record.PaidAtUtc);
    }
}
