using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptiCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeePermissions",
                columns: table => new
                {
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePermissions", x => new { x.EmployeeId, x.PermissionCode });
                    table.CheckConstraint("CK_EmployeePermissions_PermissionCode", "\"PermissionCode\" IN ('ReceiveStock', 'GiveDiscount', 'ViewDailySales', 'ViewProfit', 'ViewSupplierDetails', 'ViewReports', 'ExportData', 'ViewAuditLogs')");
                    table.ForeignKey(
                        name: "FK_EmployeePermissions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeePermissions");
        }
    }
}
