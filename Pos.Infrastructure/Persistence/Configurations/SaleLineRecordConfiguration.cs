using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class SaleLineRecordConfiguration : IEntityTypeConfiguration<SaleLineRecord>
{
    public void Configure(EntityTypeBuilder<SaleLineRecord> builder)
    {
        builder.ToTable("sale_lines");

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(record => record.SaleId)
            .HasColumnName("sale_id")
            .IsRequired();

        builder.Property(record => record.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.Property(record => record.ProductSku)
            .HasColumnName("product_sku")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(record => record.ProductName)
            .HasColumnName("product_name")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(record => record.Quantity)
            .HasColumnName("quantity")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(record => record.UnitPriceAmount)
            .HasColumnName("unit_price_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(record => record.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.HasIndex(record => record.SaleId);
        builder.HasIndex(record => record.ProductId);
        builder.HasIndex(record => new { record.SaleId, record.ProductId })
            .IsUnique();

        // ProductId es una referencia lógica real (además del snapshot de Sku/Name/Price).
        // Restrict evita eliminar un Product mientras exista una línea de venta histórica
        // que lo referencie.
        builder.HasOne<ProductRecord>()
            .WithMany()
            .HasForeignKey(record => record.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
