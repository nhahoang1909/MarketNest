using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // auto-generated migration — array args are not called repeatedly

namespace MarketNest.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddVariantSalePrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "variants",
                schema: "catalog",
                columns: table => new
                {
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    compare_at_price = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    sale_price = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    sale_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sale_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    stock_quantity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_variants", x => x.variant_id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_variants_active_sale",
                schema: "catalog",
                table: "variants",
                columns: new[] { "sale_end", "sale_price" },
                filter: "sale_price IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_variants_sku",
                schema: "catalog",
                table: "variants",
                column: "sku",
                unique: true);

            // Invariant S5: sale price/start/end must all be null or all non-null; price must be positive
            migrationBuilder.Sql(@"
                ALTER TABLE catalog.variants ADD CONSTRAINT chk_sale_price_positive
                    CHECK (sale_price IS NULL OR sale_price > 0);

                ALTER TABLE catalog.variants ADD CONSTRAINT chk_sale_dates_consistent
                    CHECK (
                        (sale_price IS NULL AND sale_start IS NULL AND sale_end IS NULL)
                        OR
                        (sale_price IS NOT NULL AND sale_start IS NOT NULL AND sale_end IS NOT NULL AND sale_start < sale_end)
                    );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE catalog.variants DROP CONSTRAINT IF EXISTS chk_sale_price_positive;
                ALTER TABLE catalog.variants DROP CONSTRAINT IF EXISTS chk_sale_dates_consistent;
            ");

            migrationBuilder.DropTable(
                name: "variants",
                schema: "catalog");
        }
    }
}
