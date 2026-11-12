using MediClinic.Application;
using MediClinic.Domain;
using MediClinic.Infrastructure;
using MediClinic.Infrastructure.Identity;
using MediClinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MediClinic.Tests;

public sealed class BookingTests
{
    internal sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2029, 12, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    internal sealed class Database : IAsyncDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), $"booking-{Guid.NewGuid()}.db");
        public DomainFixture Fixture { get; } = new();
        public DomainFixture Other { get; } = new();
        public Clock Time { get; } = new();
        public string PatientId { get; private set; } = "";
        public string OtherPatientId { get; private set; } = "";
        public string StaffId { get; private set; } = "";
        public string OtherStaffId { get; private set; } = "";
        public string UnlinkedStaffId { get; private set; } = "";
        public string ConnectionString => $"Data Source={path};Foreign Keys=True;Default Timeout=10;Pooling=False";
        public MediClinicDbContext Context(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<MediClinicDbContext>()
            .UseSqlite(ConnectionString).AddInterceptors(interceptors).Options);
        public AppointmentBooking Service(MediClinicDbContext db) => new(db, Time);
        public BookingRequest Request(DateTimeOffset? start = null) => new(Fixture.Clinic.Id, Fixture.Department.Branch.Id,
            Fixture.Department.Id, Fixture.Doctor.Id, Fixture.Service.Id, Scheduling.Local(start ?? Fixture.Start));
        public static async Task<Database> Create()
        {
            var database = new Database();
            await using var db = database.Context();
            await db.Database.MigrateAsync();
            db.AddRange(database.Fixture.Doctor, database.Other.Doctor, database.Fixture.Patient, database.Other.Patient);
            db.AddRange(database.Fixture.Service, database.Other.Service);
            db.Add(new StaffAvailability(Guid.NewGuid(), database.Fixture.Doctor, database.Fixture.Start, database.Fixture.Start.AddHours(3)));
            foreach (var role in new[] { AuthRoles.Patient, AuthRoles.Staff }) db.Roles.Add(new IdentityRole(role) { NormalizedName = role.ToUpperInvariant() });
            await db.SaveChangesAsync();
            async Task<string> Add(ApplicationUser user, string role)
            {
                user.NormalizedUserName = user.UserName!.ToUpperInvariant();
                user.NormalizedEmail = user.Email!.ToUpperInvariant();
                db.Users.Add(user);
                db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = (await db.Roles.SingleAsync(x => x.Name == role)).Id });
                await db.SaveChangesAsync();
                return user.Id;
            }
            database.PatientId = await Add(ApplicationUser.ForPatient("patient@test", database.Fixture.Patient), AuthRoles.Patient);
            database.OtherPatientId = await Add(ApplicationUser.ForPatient("other@test", database.Other.Patient), AuthRoles.Patient);
            database.StaffId = await Add(ApplicationUser.ForStaff("staff@test", database.Fixture.Doctor), AuthRoles.Staff);
            database.OtherStaffId = await Add(ApplicationUser.ForStaff("otherstaff@test", database.Other.Doctor), AuthRoles.Staff);
            database.UnlinkedStaffId = await Add(ApplicationUser.Administrator("unlinked@test"), AuthRoles.Staff);
            return database;
        }
        public ValueTask DisposeAsync() { File.Delete(path); return ValueTask.CompletedTask; }
    }

    [Fact]
    public async Task BookingPersistsPendingOwnerUtcAndDurationSnapshot()
    {
        await using var data = await Database.Create(); await using var db = data.Context();
        var service = data.Service(db);
        var id = await service.Book(data.PatientId, data.Request());
        var appointment = await service.Find(data.PatientId, id, false);
        Assert.NotNull(appointment); Assert.Equal(AppointmentStatus.Pending, appointment.Status);
        Assert.Equal(data.Fixture.Start, appointment.StartsAt); Assert.Equal(TimeSpan.Zero, appointment.StartsAt.Offset);
        Assert.Equal(data.Fixture.Service.Duration, appointment.EndsAt - appointment.StartsAt);
        await db.Database.ExecuteSqlRawAsync("UPDATE MedicalServices SET Duration = Duration * 2");
        db.ChangeTracker.Clear();
        Assert.Equal(data.Fixture.Service.Duration, (await db.Appointments.SingleAsync()).EndsAt - appointment.StartsAt);
    }

    [Theory]
    [InlineData("clinic")][InlineData("branch")][InlineData("department")][InlineData("doctor")][InlineData("service")]
    public async Task RejectsIncompatibleOrInvalidIdentifiers(string field)
    {
        await using var data = await Database.Create(); await using var db = data.Context();
        var request = data.Request();
        request = field switch
        {
            "clinic" => request with { ClinicId = data.Other.Clinic.Id },
            "branch" => request with { BranchId = Guid.NewGuid() },
            "department" => request with { DepartmentId = data.Other.Department.Id },
            "doctor" => request with { DoctorId = data.Other.Doctor.Id },
            _ => request with { ServiceId = data.Other.Service.Id }
        };
        await Assert.ThrowsAsync<BookingException>(() => data.Service(db).Book(data.PatientId, request));
        Assert.Empty(await db.Appointments.ToListAsync());
    }

    [Theory]
    [InlineData(-60)][InlineData(170)][InlineData(1440)][InlineData(-100000)][InlineData(1000000)]
    public async Task RejectsUnavailablePastAndDistantTimes(int minutes)
    {
        await using var data = await Database.Create(); await using var db = data.Context();
        await Assert.ThrowsAsync<BookingException>(() => data.Service(db).Book(data.PatientId, data.Request(data.Fixture.Start.AddMinutes(minutes))));
    }

    [Theory]
    [InlineData("Pending", false)][InlineData("Confirmed", false)][InlineData("Completed", false)]
    [InlineData("Rejected", true)][InlineData("Cancelled", true)]
    public async Task BlockingStatusAndReleasedSlots(string status, bool allowed)
    {
        await using var data = await Database.Create(); await using var db = data.Context();
        var service = data.Service(db); var id = await service.Book(data.PatientId, data.Request());
        var appointment = await db.Appointments.SingleAsync();
        switch (status)
        {
            case "Confirmed": appointment.Confirm(); break;
            case "Completed": appointment.Confirm(); appointment.Complete(appointment.EndsAt); break;
            case "Rejected": appointment.Reject("Unavailable"); break;
            case "Cancelled": appointment.Cancel("Changed plans"); break;
        }
        await db.SaveChangesAsync();
        if (allowed) Assert.NotEqual(id, await service.Book(data.OtherPatientId, data.Request()));
        else await Assert.ThrowsAsync<BookingException>(() => service.Book(data.OtherPatientId, data.Request(data.Fixture.Start.AddMinutes(15))));
    }

    [Fact]
    public async Task AdjacentIntervalsAreAllowedAndSlotsExcludeOverlaps()
    {
        await using var data = await Database.Create(); await using var db = data.Context(); var service = data.Service(db);
        await service.Book(data.PatientId, data.Request());
        var slots = await service.Slots(data.PatientId, data.Fixture.Doctor.Id, data.Fixture.Service.Id, DateOnly.FromDateTime(Scheduling.Local(data.Fixture.Start)));
        Assert.DoesNotContain(Scheduling.Local(data.Fixture.Start.AddMinutes(15)), slots);
        Assert.Contains(Scheduling.Local(data.Fixture.Start.AddMinutes(30)), slots);
        await service.Book(data.PatientId, data.Request(data.Fixture.Start.AddMinutes(30)));
        Assert.Equal(2, await db.Appointments.CountAsync());
    }

    [Fact]
    public async Task PatientIsolationDepartmentScopeAndUnlinkedStaffAreEnforced()
    {
        await using var data = await Database.Create(); await using var db = data.Context(); var service = data.Service(db);
        var id = await service.Book(data.PatientId, data.Request());
        Assert.Null(await service.Find(data.OtherPatientId, id, false)); Assert.Empty(await service.List(data.OtherPatientId, false));
        Assert.Null(await service.Find(data.OtherStaffId, id, true)); Assert.Empty(await service.List(data.OtherStaffId, true));
        await Assert.ThrowsAsync<BookingException>(() => service.Act(data.OtherPatientId, id, "Cancel", "No"));
        await Assert.ThrowsAsync<BookingException>(() => service.Act(data.OtherStaffId, id, "Confirm", null));
        await Assert.ThrowsAsync<BookingException>(() => service.Act(data.PatientId, id, "Confirm", null));
        await Assert.ThrowsAsync<BookingException>(() => service.List(data.UnlinkedStaffId, true));
        await Assert.ThrowsAsync<BookingException>(() => service.Book(data.StaffId, data.Request()));
        Assert.Single(await service.List(data.StaffId, true));
    }

    [Fact]
    public async Task ActionsRequireReasonsAndPreserveTemporalAndTerminalRules()
    {
        await using var data = await Database.Create(); await using var db = data.Context(); var service = data.Service(db);
        var id = await service.Book(data.PatientId, data.Request());
        await Assert.ThrowsAsync<BookingException>(() => service.Act(data.StaffId, id, "Reject", " "));
        await Assert.ThrowsAsync<BookingException>(() => service.Act(data.PatientId, id, "Cancel", null));
        await service.Act(data.StaffId, id, "Confirm", null);
        await Assert.ThrowsAsync<BookingException>(() => service.Act(data.StaffId, id, "Complete", null));
        data.Time.Now = data.Fixture.Start.AddMinutes(30);
        await service.Act(data.StaffId, id, "Complete", null);
        await Assert.ThrowsAsync<BookingException>(() => service.Act(data.PatientId, id, "Cancel", "No"));
        Assert.Equal(AppointmentStatus.Completed, (await service.Find(data.PatientId, id, false))!.Status);
    }

    // Both requests pass their prechecks before either INSERT. Separate contexts/connections,
    // real file-backed SQLite, and a barrier at SaveChanges prove the database boundary.
    private sealed class SaveBarrier(Barrier barrier) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Concurrent save did not arrive.");
            return ValueTask.FromResult(result);
        }
    }
    [Fact]
    public async Task SimultaneousBookingsCannotBothSucceedAfterBothPrechecksPass()
    {
        await using var data = await Database.Create(); using var barrier = new Barrier(2);
        async Task<bool> Attempt(string patient)
        {
            await using var db = data.Context(new SaveBarrier(barrier));
            try { await data.Service(db).Book(patient, data.Request()); return true; }
            catch (BookingException) { return false; }
        }
        var results = await Task.WhenAll(Task.Run(() => Attempt(data.PatientId)), Task.Run(() => Attempt(data.OtherPatientId)));
        Assert.Equal(1, results.Count(x => x));
        await using var verify = data.Context(); Assert.Equal(1, await verify.Appointments.CountAsync());
    }

    [Fact]
    public async Task ConcurrentStaffDecisionsCannotOverwriteEachOther()
    {
        await using var data = await Database.Create();
        await using (var db = data.Context()) await data.Service(db).Book(data.PatientId, data.Request());
        await using var first = data.Context(); await using var second = data.Context();
        var one = await first.Appointments.SingleAsync(); var two = await second.Appointments.SingleAsync();
        one.Confirm(); two.Reject("No availability"); await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task DatabaseTriggerAlsoRejectsOverlappingUpdateAndDirectInsert()
    {
        await using var data = await Database.Create(); await using var db = data.Context(); var service = data.Service(db);
        await service.Book(data.PatientId, data.Request());
        var next = await service.Book(data.PatientId, data.Request(data.Fixture.Start.AddMinutes(30)));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Appointments SET StartsAt = {data.Fixture.Start.UtcTicks} WHERE Id = {next}"));
        db.Add(new Appointment(Guid.NewGuid(), (await db.Patients.SingleAsync(x => x.Id == data.Fixture.Patient.Id)),
            await db.Doctors.SingleAsync(x => x.Id == data.Fixture.Doctor.Id), await db.MedicalServices.SingleAsync(x => x.Id == data.Fixture.Service.Id), data.Fixture.Start));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("appointment_overlap", error.InnerException!.Message);
    }

    [Fact]
    public async Task SchedulingMigrationPreservesExistingAppointments()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new MediClinicDbContext(new DbContextOptionsBuilder<MediClinicDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync("20261105223217_IdentityAuthentication");
        var fixture = new DomainFixture(); var appointment = fixture.Appointment(); await fixture.SeedLegacy(db, appointment);
        await migrator.MigrateAsync(); db.ChangeTracker.Clear();
        var restored = await db.Appointments.SingleAsync(); Assert.Equal(appointment.Id, restored.Id); Assert.Equal(appointment.EndsAt, restored.EndsAt); Assert.Null(restored.Price);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task UnsupportedDurationCannotProduceSlotsOrOverflowBooking()
    {
        await using var data = await Database.Create(); await using var db = data.Context();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE MedicalServices SET Duration = {long.MaxValue}");
        var service = data.Service(db);
        Assert.Empty(await service.Slots(data.PatientId, data.Fixture.Doctor.Id, data.Fixture.Service.Id, DateOnly.FromDateTime(Scheduling.Local(data.Fixture.Start))));
        await Assert.ThrowsAsync<BookingException>(() => service.Book(data.PatientId, data.Request()));
    }

    [Fact]
    public async Task SlotDiscoveryChecksConflictsBeyondLocalMidnight()
    {
        await using var data = await Database.Create(); await using var db = data.Context();
        var night = data.Fixture.Start.AddHours(11); // 23:00 in Europe/Istanbul.
        var doctor = await db.Doctors.Include(x => x.Department).SingleAsync(x => x.Id == data.Fixture.Doctor.Id);
        db.Add(new StaffAvailability(Guid.NewGuid(), doctor, night, night.AddHours(2))); await db.SaveChangesAsync();
        var service = data.Service(db);
        await service.Book(data.PatientId, data.Request(night.AddHours(1)));
        var slots = await service.Slots(data.PatientId, doctor.Id, data.Fixture.Service.Id, DateOnly.FromDateTime(Scheduling.Local(night)));
        Assert.Contains(Scheduling.Local(night.AddMinutes(30)), slots);
        Assert.DoesNotContain(Scheduling.Local(night.AddMinutes(45)), slots);
    }

    [Fact]
    public async Task LegacyOverlapStopsMigrationWithoutChangingClinicalHistory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new MediClinicDbContext(new DbContextOptionsBuilder<MediClinicDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync("20261105223217_IdentityAuthentication");
        var fixture = new DomainFixture(); await fixture.SeedLegacy(db, fixture.Appointment(), fixture.Appointment());
        await Assert.ThrowsAsync<SqliteException>(() => migrator.MigrateAsync());
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Appointments").SingleAsync());
        Assert.Equal(4, (await db.Database.GetPendingMigrationsAsync()).Count());
    }
}
