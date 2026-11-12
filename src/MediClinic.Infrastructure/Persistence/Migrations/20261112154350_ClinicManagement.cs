using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediClinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClinicManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "MedicalServices",
                type: "TEXT",
                precision: 11,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ClinicAdministrators",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ClinicId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicAdministrators", x => new { x.UserId, x.ClinicId });
                    table.ForeignKey(
                        name: "FK_ClinicAdministrators_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicAdministrators_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkingHours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BranchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Day = table.Column<int>(type: "INTEGER", nullable: false),
                    Closed = table.Column<bool>(type: "INTEGER", nullable: false),
                    Opens = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    Closes = table.Column<TimeOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkingHours", x => x.Id);
                    table.CheckConstraint("CK_WorkingHours_Range", "Day BETWEEN 0 AND 6 AND ((Closed = 1 AND Opens IS NULL AND Closes IS NULL) OR (Closed = 0 AND Opens IS NOT NULL AND Closes IS NOT NULL AND Opens < Closes))");
                    table.ForeignKey(
                        name: "FK_WorkingHours_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicAdministrators_ClinicId",
                table: "ClinicAdministrators",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkingHours_BranchId_Day",
                table: "WorkingHours",
                columns: new[] { "BranchId", "Day" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicAdministrators");

            migrationBuilder.DropTable(
                name: "WorkingHours");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "MedicalServices");
        }
    }
}
