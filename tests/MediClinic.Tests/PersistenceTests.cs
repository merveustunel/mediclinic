using MediClinic.Domain;
using MediClinic.Infrastructure;
using MediClinic.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediClinic.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task MigrationAndRoundTripRestoreEncapsulatedGraphAndInheritance()
    {
        await using var database = await Database.Create();
        var fixture = database.Fixture;
        await using var db = database.Context();
        var clinic = await db.Clinics.Include(x => x.Branches).ThenInclude(x => x.Departments)
            .ThenInclude(x => x.Services).SingleAsync();
        var department = Assert.Single(Assert.Single(clinic.Branches).Departments);
        var service = Assert.Single(department.Services);
        Assert.Equal(fixture.Clinic.Id, clinic.Id);
        Assert.Equal(fixture.Clinic.Name, clinic.Name);
        Assert.Equal(fixture.Department.Branch.Address, department.Branch.Address);
        Assert.Same(clinic, department.Branch.Clinic);
        Assert.Same(department, service.Department);
        Assert.Equal(fixture.Service.Duration, service.Duration);
        Assert.Throws<NotSupportedException>(() => ((IList<Branch>)clinic.Branches).Clear());

        var people = await db.Set<Person>().ToListAsync();
        Assert.Equal(3, people.Count);
        var doctor = Assert.Single(people.OfType<Doctor>());
        var patient = Assert.Single(people.OfType<Patient>());
        Assert.Contains(people, x => x.GetType() == typeof(Staff) && x.Role == "Nurse");
        Assert.Equal(fixture.Patient.DateOfBirth, patient.DateOfBirth);
        Assert.Equal(fixture.Patient.FullName, patient.FullName);
        Assert.Equal(fixture.Doctor.Specialty, doctor.Specialty);
        Assert.Equal("Doctor", doctor.Role);
        Assert.True(doctor.CanProvide(service));
        var appointment = await db.Appointments.Include(x => x.Patient).Include(x => x.Doctor)
            .Include(x => x.Service).SingleAsync();
        Assert.Same(patient, appointment.Patient);
        Assert.Same(doctor, appointment.Doctor);
        Assert.Equal(department.BranchId, appointment.BranchId);
        Assert.Equal(fixture.Start, appointment.StartsAt);
        Assert.Equal(fixture.Start.Add(fixture.Service.Duration), appointment.EndsAt);
        Assert.Equal(AppointmentStatus.Pending, appointment.Status);
        var availability = await db.StaffAvailabilities.Include(x => x.Staff).SingleAsync();
        Assert.Same(doctor, availability.Staff);
        Assert.True(availability.Covers(appointment.StartsAt, appointment.EndsAt));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Theory]
    [InlineData("Confirm", AppointmentStatus.Confirmed, null)]
    [InlineData("Reject", AppointmentStatus.Rejected, "Unavailable")]
    [InlineData("Cancel", AppointmentStatus.Cancelled, "Changed plans")]
    [InlineData("Complete", AppointmentStatus.Completed, null)]
    public async Task StatusChangesPersistAcrossContexts(string operation, AppointmentStatus expected, string? reason)
    {
        await using var database = await Database.Create();
        await using (var db = database.Context())
        {
            var appointment = await db.Appointments.SingleAsync();
            switch (operation)
            {
                case "Confirm": appointment.Confirm(); break;
                case "Reject": appointment.Reject(reason!); break;
                case "Cancel": appointment.Confirm(); appointment.Cancel(reason!); break;
                case "Complete": appointment.Confirm(); appointment.Complete(appointment.EndsAt); break;
            }
            await db.SaveChangesAsync();
        }
        await using var verification = database.Context();
        var restored = await verification.Appointments.SingleAsync();
        Assert.Equal(expected, restored.Status);
        Assert.Equal(reason, restored.DecisionReason);
        Assert.Throws<InvalidOperationException>(() => restored.Confirm());
    }

    [Fact]
    public async Task UtcTicksSupportSqlFilteringOrderingAndPreservePrecision()
    {
        await using var database = await Database.Create();
        var instant = database.Fixture.Start.AddHours(1).ToOffset(TimeSpan.FromHours(3)).AddTicks(17);
        await using (var db = database.Context())
        {
            db.Attach(database.Fixture.Patient);
            db.Attach(database.Fixture.Doctor);
            db.Add(new Appointment(Guid.NewGuid(), database.Fixture.Patient,
                database.Fixture.Doctor, database.Fixture.Service, instant));
            await db.SaveChangesAsync();
        }
        await using var query = database.Context();
        var result = await query.Appointments.Where(x => x.StartsAt > database.Fixture.Start)
            .OrderBy(x => x.StartsAt).SingleAsync();
        Assert.Equal(instant.UtcTicks, result.StartsAt.UtcTicks);
        Assert.Equal(TimeSpan.Zero, result.StartsAt.Offset);
    }

    [Theory]
    [InlineData("UPDATE Clinics SET Name = NULL")]
    [InlineData("UPDATE Clinics SET Name = '   '")]
    [InlineData("UPDATE MedicalServices SET Duration = 0")]
    [InlineData("UPDATE StaffAvailabilities SET EndsAt = StartsAt")]
    [InlineData("UPDATE Appointments SET EndsAt = StartsAt")]
    [InlineData("UPDATE Appointments SET Status = 'Unknown'")]
    [InlineData("UPDATE Appointments SET Status = 'Cancelled', DecisionReason = NULL")]
    [InlineData("UPDATE Appointments SET DoctorId = '00000000-0000-0000-0000-000000000001'")]
    [InlineData("UPDATE People SET Specialty = NULL WHERE PersonType = 'Doctor'")]
    [InlineData("DELETE FROM Clinics")]
    [InlineData("DELETE FROM Branches")]
    [InlineData("DELETE FROM Departments")]
    [InlineData("DELETE FROM MedicalServices")]
    [InlineData("DELETE FROM People WHERE PersonType = 'Patient'")]
    [InlineData("DELETE FROM People WHERE PersonType = 'Doctor'")]
    public async Task DatabaseRejectsInvalidRowsAndReferencedDeletes(string sql)
    {
        await using var database = await Database.Create();
        await using var db = database.Context();
        var exception = await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync(sql));
        Assert.Equal(19, exception.SqliteErrorCode);
    }

    [Fact]
    public async Task DuplicateIdentifiersAreRejectedByDatabase()
    {
        await using var database = await Database.Create();
        await using var db = database.Context();
        db.Add(new Clinic(database.Fixture.Clinic.Id, "Duplicate"));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(19, Assert.IsType<SqliteException>(exception.InnerException).SqliteErrorCode);
    }

    [Fact]
    public void RegistrationUsesScopedContextsWithoutCreatingDatabase()
    {
        var services = new ServiceCollection();
        Assert.Same(services, services.AddInfrastructure("Data Source=:memory:;Foreign Keys=True"));
        using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var context = first.ServiceProvider.GetRequiredService<MediClinicDbContext>();
        Assert.Same(context, first.ServiceProvider.GetRequiredService<MediClinicDbContext>());
        Assert.NotSame(context, second.ServiceProvider.GetRequiredService<MediClinicDbContext>());
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", context.Database.ProviderName);
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    private sealed class Database(SqliteConnection connection) : IAsyncDisposable
    {
        internal DomainFixture Fixture { get; } = new();
        internal MediClinicDbContext Context() => new(new DbContextOptionsBuilder<MediClinicDbContext>()
            .UseSqlite(connection).Options);

        internal static async Task<Database> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var database = new Database(connection);
            await using var db = database.Context();
            await db.Database.MigrateAsync();
            var fixture = database.Fixture;
            db.Add(fixture.Appointment());
            db.Add(new Staff(Guid.NewGuid(), "Sample", "Nurse", fixture.Department, "Nurse"));
            db.Add(new StaffAvailability(Guid.NewGuid(), fixture.Doctor, fixture.Start.AddHours(-1), fixture.Start.AddHours(1)));
            await db.SaveChangesAsync();
            return database;
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
