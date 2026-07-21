using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Infrastructure.Persistence.Conversions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class ProductRecordConfiguration : IEntityTypeConfiguration<ProductRecord>
{
    public void Configure(EntityTypeBuilder<ProductRecord> builder)
    {
        builder.ToTable("products");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(record => record.OrganizationId)
            .HasColumnName("organization_id")
            .IsRequired();

        builder.Property(record => record.Sku)
            .HasColumnName("sku")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(record => record.Barcode)
            .HasColumnName("barcode")
            .HasMaxLength(32);

        builder.Property(record => record.Name)
            .HasColumnName("name")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(record => record.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(record => record.SalePriceAmount)
            .HasColumnName("sale_price_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(record => record.SalePriceCurrency)
            .HasColumnName("sale_price_currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(record => record.CostAmount)
            .HasColumnName("cost_amount")
            .HasPrecision(18, 2);

        builder.Property(record => record.CostCurrency)
            .HasColumnName("cost_currency")
            .HasMaxLength(3);

        builder.Property(record => record.TracksInventory)
            .HasColumnName("tracks_inventory")
            .IsRequired();

        builder.Property(record => record.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(record => record.CreatedAtUtc)
            .HasColumnName("created_at_utc_ticks")
            .HasConversion<DateTimeOffsetToTicksConverter>()
            .IsRequired();

        builder.HasIndex(record => record.IsActive);
        builder.HasIndex(record => record.Name);

        // No se agrega un índice adicional solo por OrganizationId: los índices compuestos
        // (OrganizationId, Sku) y (OrganizationId, Barcode) ya cubren ese prefijo.
        builder.HasIndex(record => new { record.OrganizationId, record.Sku })
            .IsUnique();

        builder.HasIndex(record => new { record.OrganizationId, record.Barcode });

        builder.HasOne<OrganizationRecord>()
            .WithMany()
            .HasForeignKey(record => record.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
