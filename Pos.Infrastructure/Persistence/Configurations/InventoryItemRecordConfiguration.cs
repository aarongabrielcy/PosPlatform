using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Infrastructure.Persistence.Conversions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class InventoryItemRecordConfiguration : IEntityTypeConfiguration<InventoryItemRecord>
{
    public void Configure(EntityTypeBuilder<InventoryItemRecord> builder)
    {
        builder.ToTable("inventory_items");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(record => record.BranchId)
            .HasColumnName("branch_id")
            .IsRequired();

        builder.Property(record => record.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.Property(record => record.Quantity)
            .HasColumnName("quantity")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(record => record.ReorderPoint)
            .HasColumnName("reorder_point")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(record => record.CreatedAtUtc)
            .HasColumnName("created_at_utc_ticks")
            .HasConversion<DateTimeOffsetToTicksConverter>()
            .IsRequired();

        builder.Property(record => record.UpdatedAtUtc)
            .HasColumnName("updated_at_utc_ticks")
            .HasConversion<DateTimeOffsetToTicksConverter>()
            .IsRequired();

        builder.HasIndex(record => new { record.BranchId, record.ProductId })
            .IsUnique();

        builder.HasIndex(record => record.ProductId);

        // El índice único (BranchId, ProductId) ya cubre el prefijo BranchId; no se agrega
        // un índice simple redundante para esa FK.
        builder.HasOne<BranchRecord>()
            .WithMany()
            .HasForeignKey(record => record.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProductRecord>()
            .WithMany()
            .HasForeignKey(record => record.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
