using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OptiCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AttendanceRecords_Checkout",
                table: "AttendanceRecords");

            migrationBuilder.CreateTable(
                name: "AttendanceCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousCheckInAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PreviousCheckOutAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NewCheckInAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NewCheckOutAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CorrectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrectedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceCorrections", x => x.Id);
                    table.CheckConstraint("CK_AttendanceCorrections_Reason", "length(btrim(\"Reason\")) > 0");
                    table.CheckConstraint("CK_AttendanceCorrections_Times", "(\"PreviousCheckOutAtUtc\" IS NULL OR \"PreviousCheckOutAtUtc\" >= \"PreviousCheckInAtUtc\") AND (\"NewCheckOutAtUtc\" IS NULL OR \"NewCheckOutAtUtc\" >= \"NewCheckInAtUtc\")");
                    table.ForeignKey(
                        name: "FK_AttendanceCorrections_AttendanceRecords_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalTable: "AttendanceRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceCorrections_Employees_CorrectedByEmployeeId",
                        column: x => x.CorrectedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AttendanceRecords_Checkout",
                table: "AttendanceRecords",
                sql: "(\"CheckOutAtUtc\" IS NULL AND \"CheckoutProcessedAtUtc\" IS NULL AND NOT \"WasCheckoutAutomatic\") OR (\"CheckOutAtUtc\" IS NOT NULL AND \"CheckoutProcessedAtUtc\" IS NOT NULL AND \"CheckOutAtUtc\" >= \"CheckInAtUtc\" AND \"CheckoutProcessedAtUtc\" >= \"CheckOutAtUtc\" AND ((\"WasCheckoutAutomatic\" AND \"CheckOutAtUtc\" = \"AutomaticCheckoutDueAtUtc\") OR (NOT \"WasCheckoutAutomatic\" AND \"UpdatedByEmployeeId\" IS NOT NULL AND \"CheckOutAtUtc\" <= \"AutomaticCheckoutDueAtUtc\")))");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCorrections_AttendanceRecordId_CorrectedAtUtc",
                table: "AttendanceCorrections",
                columns: new[] { "AttendanceRecordId", "CorrectedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCorrections_CorrectedByEmployeeId",
                table: "AttendanceCorrections",
                column: "CorrectedByEmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceCorrections");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AttendanceRecords_Checkout",
                table: "AttendanceRecords");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AttendanceRecords_Checkout",
                table: "AttendanceRecords",
                sql: "(\"CheckOutAtUtc\" IS NULL AND \"CheckoutProcessedAtUtc\" IS NULL AND NOT \"WasCheckoutAutomatic\") OR (\"CheckOutAtUtc\" IS NOT NULL AND \"CheckoutProcessedAtUtc\" IS NOT NULL AND \"CheckOutAtUtc\" >= \"CheckInAtUtc\" AND \"CheckoutProcessedAtUtc\" >= \"CheckOutAtUtc\" AND ((\"WasCheckoutAutomatic\" AND \"UpdatedByEmployeeId\" IS NULL AND \"CheckOutAtUtc\" = \"AutomaticCheckoutDueAtUtc\") OR (NOT \"WasCheckoutAutomatic\" AND \"UpdatedByEmployeeId\" IS NOT NULL AND \"CheckOutAtUtc\" < \"AutomaticCheckoutDueAtUtc\")))");
        }
    }
}
