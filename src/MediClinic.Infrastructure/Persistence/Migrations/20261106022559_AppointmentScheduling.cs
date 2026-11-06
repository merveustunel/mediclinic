using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediClinic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Refuse an inconsistent legacy database instead of silently deleting clinical history.
            migrationBuilder.Sql("""
                CREATE TABLE __AppointmentOverlapCheck (Count INTEGER CHECK (Count = 0));
                INSERT INTO __AppointmentOverlapCheck
                SELECT count(*) FROM Appointments a JOIN Appointments b
                  ON a.Id < b.Id AND a.DoctorId = b.DoctorId
                  AND a.StartsAt < b.EndsAt AND b.StartsAt < a.EndsAt
                WHERE a.Status IN ('Pending','Confirmed','Completed')
                  AND b.Status IN ('Pending','Confirmed','Completed');
                DROP TABLE __AppointmentOverlapCheck;
                """);
            foreach (var operation in new[] { "INSERT", "UPDATE" })
                migrationBuilder.Sql($"""
                    CREATE TRIGGER TR_Appointments_NoOverlap_{operation}
                    BEFORE {operation} ON Appointments
                    WHEN NEW.Status IN ('Pending','Confirmed','Completed')
                    BEGIN
                      SELECT RAISE(ABORT, 'appointment_overlap')
                      WHERE EXISTS (
                        SELECT 1 FROM Appointments a
                        WHERE a.Id <> NEW.Id AND a.DoctorId = NEW.DoctorId
                          AND a.Status IN ('Pending','Confirmed','Completed')
                          AND a.StartsAt < NEW.EndsAt AND NEW.StartsAt < a.EndsAt
                      );
                    END;
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER TR_Appointments_NoOverlap_INSERT;");
            migrationBuilder.Sql("DROP TRIGGER TR_Appointments_NoOverlap_UPDATE;");
        }
    }
}
