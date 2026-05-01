using MarketNest.Base.Common;
using MarketNest.Catalog.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarketNest.Catalog.Infrastructure;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable(TableConstants.CatalogTable.Variant, TableConstants.Schema.Catalog);
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("variant_id");

        builder.Property(v => v.ProductId).HasColumnName("product_id").IsRequired();

        builder.Property(v => v.Sku).HasColumnName("sku").HasMaxLength(100).IsRequired();
        builder.HasIndex(v => v.Sku).IsUnique();

        // Money — Price (required)
        builder.Property(v => v.Price)
            .HasConversion(m => m.Amount, a => new Money(a, DomainConstants.Currencies.Default))
            .HasColumnName("price")
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        // Money? — CompareAtPrice (optional, display only)
        builder.Property(v => v.CompareAtPrice)
            .HasConversion(
                m => m == null ? (decimal?)null : m.Amount,
                a => a.HasValue ? new Money(a.Value, DomainConstants.Currencies.Default) : null)
            .HasColumnName("compare_at_price")
            .HasColumnType("numeric(10,2)");

        // ── Sale Price Fields ──────────────────────────────────────────
        builder.Property(v => v.SalePrice)
            .HasConversion(
                m => m == null ? (decimal?)null : m.Amount,
                a => a.HasValue ? new Money(a.Value, DomainConstants.Currencies.Default) : null)
            .HasColumnName("sale_price")
            .HasColumnType("numeric(10,2)");

        builder.Property(v => v.SaleStart).HasColumnName("sale_start");
        builder.Property(v => v.SaleEnd).HasColumnName("sale_end");

        // Partial index for fast active-sale queries and the expiry background job
        builder.HasIndex(v => new { v.SaleEnd, v.SalePrice })
            .HasDatabaseName("idx_variants_active_sale")
            .HasFilter("sale_price IS NOT NULL");
        // ──────────────────────────────────────────────────────────────

        builder.Property(v => v.StockQuantity).HasColumnName("stock_quantity").IsRequired();
        builder.Property(v => v.Status).HasColumnName("status").IsRequired();
        builder.Property(v => v.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}
