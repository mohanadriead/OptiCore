using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OptiCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductsAndBrands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                    table.CheckConstraint("CK_Brands_Name", "length(btrim(\"Name\")) > 0 AND length(btrim(\"NormalizedName\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductNumber = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    Barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: true),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Color = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Size = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GenderCategory = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    LensType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RegularSalePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PromoPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    VatRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Products_Barcode", "\"Barcode\" IS NULL OR (length(\"Barcode\") > 0 AND \"Barcode\" = btrim(\"Barcode\"))");
                    table.CheckConstraint("CK_Products_Category", "\"Category\" IN ('Frames','Sunglasses','Lenses','ContactLenses','Accessories','CleaningProducts','Cases','Other')");
                    table.CheckConstraint("CK_Products_GenderCategory", "\"GenderCategory\" IS NULL OR (\"Category\" IN ('Frames','Sunglasses') AND \"GenderCategory\" IN ('Men','Women','Unisex','Kids'))");
                    table.CheckConstraint("CK_Products_LensType", "\"LensType\" IS NULL OR \"Category\" = 'Lenses'");
                    table.CheckConstraint("CK_Products_Prices", "\"RegularSalePrice\" >= 0 AND (\"PromoPrice\" IS NULL OR \"PromoPrice\" >= 0)");
                    table.CheckConstraint("CK_Products_VatRate", "\"VatRate\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_Products_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductAttributes",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAttributes", x => new { x.ProductId, x.Key });
                    table.CheckConstraint("CK_ProductAttributes_Text", "length(btrim(\"Key\")) > 0 AND length(btrim(\"Value\")) > 0 AND \"Key\" = btrim(\"Key\") AND \"Value\" = btrim(\"Value\")");
                    table.ForeignKey(
                        name: "FK_ProductAttributes_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Brands_IsActive",
                table: "Brands",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Brands_NormalizedName",
                table: "Brands",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                unique: true,
                filter: "\"Barcode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_BrandId",
                table: "Products",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Category_IsActive",
                table: "Products",
                columns: new[] { "Category", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive_ProductNumber",
                table: "Products",
                columns: new[] { "IsActive", "ProductNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductNumber",
                table: "Products",
                column: "ProductNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductAttributes");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Brands");
        }
    }
}
