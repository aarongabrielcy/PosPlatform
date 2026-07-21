using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence.Conversions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class SaleRecordConfiguration : IEntityTypeConfiguration<SaleRecord>
{
    public void Configure(EntityTypeBuilder<SaleRecord> builder)
    {
        builder.ToTable("sales");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(record => record.OrganizationId)
            .HasColumnName("organization_id")
            .IsRequired();

        builder.Property(record => record.BranchId)
            .HasColumnName("branch_id")
            .IsRequired();

        builder.Property(record => record.RegisterSessionId)
            .HasColumnName("register_session_id")
            .IsRequired();

        builder.Property(record => record.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(record => record.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(record => record.Status)
            .HasColumnName("sale_status")
            .HasConversion<EnumToStringConverter<SaleStatus>>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(record => record.CreatedAtUtc)
            .HasColumnName("created_at_utc_ticks")
            .HasConversion<DateTimeOffsetToTicksConverter>()
            .IsRequired();

        builder.Property(record => record.CompletedAtUtc)
            .HasColumnName("completed_at_utc_ticks")
            .HasConversion<DateTimeOffsetToTicksConverter>();

        builder.HasIndex(record => record.OrganizationId);
        builder.HasIndex(record => record.BranchId);
        builder.HasIndex(record => record.RegisterSessionId);
        builder.HasIndex(record => record.CreatedByUserId);
        builder.HasIndex(record => record.CreatedAtUtc);
        builder.HasIndex(record => record.Status);

        // Sale es historial: Restrict impide eliminar el catálogo referenciado
        // (Organization/Branch/RegisterSession/User) mientras existan ventas, sin
        // permitir nunca que su eliminación borre historial de ventas.
        builder.HasOne<OrganizationRecord>()
            .WithMany()
            .HasForeignKey(record => record.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<BranchRecord>()
            .WithMany()
            .HasForeignKey(record => record.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RegisterSessionRecord>()
            .WithMany()
            .HasForeignKey(record => record.RegisterSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UserRecord>()
            .WithMany()
            .HasForeignKey(record => record.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(record => record.Lines)
            .WithOne(line => line.Sale)
            .HasForeignKey(line => line.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(record => record.Payments)
            .WithOne(payment => payment.Sale)
            .HasForeignKey(payment => payment.SaleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
