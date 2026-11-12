using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediClinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DoctorServices",
                columns: table => new
                {
                    DoctorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServiceId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorServices", x => new { x.DoctorId, x.ServiceId });
                    table.ForeignKey(
                        name: "FK_DoctorServices_MedicalServices_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "MedicalServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorServices_People_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorServices_ServiceId",
                table: "DoctorServices",
                column: "ServiceId");

            // Preserve every previously bookable department-compatible pair. No
            // appointment, person, account or availability row is rewritten.
            migrationBuilder.Sql("""
                INSERT INTO DoctorServices (DoctorId, ServiceId)
                SELECT p.Id, s.Id FROM People p JOIN MedicalServices s ON s.DepartmentId = p.DepartmentId
                WHERE p.PersonType = 'Doctor';
                """);
            foreach (var operation in new[] { "INSERT", "UPDATE" })
                migrationBuilder.Sql($"""
                    CREATE TRIGGER DoctorServices_Compatible_{operation}
                    BEFORE {operation} ON DoctorServices
                    WHEN NOT EXISTS (
                        SELECT 1 FROM People p JOIN MedicalServices s ON s.DepartmentId = p.DepartmentId
                        WHERE p.Id = NEW.DoctorId AND p.PersonType = 'Doctor' AND s.Id = NEW.ServiceId)
                    BEGIN SELECT RAISE(ABORT, 'doctor_service_incompatible'); END;
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS DoctorServices_Compatible_INSERT;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS DoctorServices_Compatible_UPDATE;");
            migrationBuilder.DropTable(
                name: "DoctorServices");
        }
    }
}
