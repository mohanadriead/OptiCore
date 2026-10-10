using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptiCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProductVatRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_VatRate",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "Products",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_VatRate",
                table: "Products",
                sql: "\"VatRate\" BETWEEN 0 AND 100");
        }
    }
}
