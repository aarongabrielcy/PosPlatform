using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Persistence.Conversions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class InventoryMovementRecordConfiguration : IEntityTypeConfiguration<InventoryMovementRecord>
{
    public void Configure(EntityTypeBuilder<InventoryMovementRecord> builder)
    {
        builder.ToTable("inventory_movements");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(record => record.InventoryItemId)
            .HasColumnName("inventory_item_id")
            .IsRequired();

        builder.Property(record => record.BranchId)
            .HasColumnName("branch_id")
            .IsRequired();

        builder.Property(record => record.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.Property(record => record.PerformedByUserId)
            .HasColumnName("performed_by_user_id")
            .IsRequired();

        builder.Property(record => record.Type)
            .HasColumnName("movement_type")
            .HasConversion<EnumToStringConverter<InventoryMovementType>>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(record => record.Quantity)
            .HasColumnName("quantity")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(record => record.QuantityBefore)
            .HasColumnName("quantity_before")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(record => record.QuantityAfter)
            .HasColumnName("quantity_after")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(record => record.SaleId)
            .HasColumnName("sale_id");

        builder.Property(record => record.SaleLineId)
            .HasColumnName("sale_line_id");

        builder.Property(record => record.OccurredAtUtc)
            .HasColumnName("occurred_at_utc_ticks")
            .HasConversion<DateTimeOffsetToTicksConverter>()
            .IsRequired();

        builder.HasIndex(record => record.InventoryItemId);
        builder.HasIndex(record => new { record.BranchId, record.ProductId });
        builder.HasIndex(record => record.OccurredAtUtc);
        builder.HasIndex(record => record.SaleId);
        builder.HasIndex(record => record.SaleLineId);

        builder.HasOne<InventoryItemRecord>()
            .WithMany()
            .HasForeignKey(record => record.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
