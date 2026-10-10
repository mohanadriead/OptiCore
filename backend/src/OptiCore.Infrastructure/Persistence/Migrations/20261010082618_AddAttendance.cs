using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptiCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckInAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AutomaticCheckoutDueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CheckOutAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CheckoutProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WasCheckoutAutomatic = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.CheckConstraint("CK_AttendanceRecords_Boundary", "\"AutomaticCheckoutDueAtUtc\" > \"CheckInAtUtc\"");
                    table.CheckConstraint("CK_AttendanceRecords_Checkout", "(\"CheckOutAtUtc\" IS NULL AND \"CheckoutProcessedAtUtc\" IS NULL AND NOT \"WasCheckoutAutomatic\") OR (\"CheckOutAtUtc\" IS NOT NULL AND \"CheckoutProcessedAtUtc\" IS NOT NULL AND \"CheckOutAtUtc\" >= \"CheckInAtUtc\" AND \"CheckoutProcessedAtUtc\" >= \"CheckOutAtUtc\" AND ((\"WasCheckoutAutomatic\" AND \"UpdatedByEmployeeId\" IS NULL AND \"CheckOutAtUtc\" = \"AutomaticCheckoutDueAtUtc\") OR (NOT \"WasCheckoutAutomatic\" AND \"UpdatedByEmployeeId\" IS NOT NULL AND \"CheckOutAtUtc\" < \"AutomaticCheckoutDueAtUtc\")))");
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_AutomaticCheckoutDueAtUtc",
                table: "AttendanceRecords",
                column: "AutomaticCheckoutDueAtUtc",
                filter: "\"CheckOutAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_EmployeeId_Open",
                table: "AttendanceRecords",
                column: "EmployeeId",
                unique: true,
                filter: "\"CheckOutAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceRecords");
        }
    }
}
