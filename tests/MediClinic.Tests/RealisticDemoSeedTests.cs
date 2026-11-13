using MediClinic.Application;
using MediClinic.Domain;
using MediClinic.Infrastructure;
using MediClinic.Infrastructure.Identity;
using MediClinic.Infrastructure.Persistence;
using MediClinic.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MediClinic.Tests;

public sealed class RealisticDemoSeedTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), $"hospital-{Guid.NewGuid()}.db");
        private readonly string secret = Guid.NewGuid().ToString("N") + "Aa1!";
        public WebApplication App(string environment = "Development", bool enabled = true, string? password = null, DateTimeOffset? now = null)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Logging:LogLevel:Default"] = "Warning",
                ["Demo:Enabled"] = enabled.ToString(), ["Demo:PatientPassword"] = password ?? secret, ["Demo:StaffPassword"] = secret });
            builder.Services.AddInfrastructure($"Data Source={path};Foreign Keys=True");
            builder.Services.AddSingleton<TimeProvider>(new Clock(now ?? Scheduling.ToUtc(new DateTime(2026, 11, 13, 14, 0, 0))));
            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o => { o.Password.RequiredLength = 12; o.User.RequireUniqueEmail = true; })
                .AddEntityFrameworkStores<MediClinicDbContext>();
            return builder.Build();
        }
        public ValueTask DisposeAsync()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
            return ValueTask.CompletedTask;
        }
    }
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private static async Task Migrate(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MediClinicDbContext>().Database.MigrateAsync();
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Testing", true)]
    [InlineData("Development", false)]
    public async Task SafetyGateRejectsBeforeDatabaseAccess(string environment, bool enabled)
    {
        await using var fixture = new Fixture();
        await using var app = fixture.App(environment, enabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() => RealisticDemoSeed.RunAsync(app));
    }

    [Fact]
    public async Task MissingMigrationsAndInvalidSecretsAreRejected()
    {
        await using var fixture = new Fixture();
        await using var app = fixture.App();
        await Assert.ThrowsAsync<InvalidOperationException>(() => RealisticDemoSeed.RunAsync(app));
        await Migrate(app);
        foreach (var password in new[] { "", "short" })
        {
            await using var invalid = fixture.App(password: password);
            await Assert.ThrowsAsync<InvalidOperationException>(() => RealisticDemoSeed.RunAsync(invalid));
            using var scope = invalid.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediClinicDbContext>();
            Assert.Equal(0, await db.Clinics.CountAsync());
            Assert.Equal(0, await db.Users.CountAsync());
        }
    }

    [Theory]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public async Task FreshSeedIsConsistentAndRepeatableAcrossDates(int day)
    {
        await using var fixture = new Fixture();
        var now = Scheduling.ToUtc(new DateTime(2026, 11, day, 14, 0, 0));
        await using var app = fixture.App(now: now);
        await Migrate(app);
        await RealisticDemoSeed.RunAsync(app);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediClinicDbContext>();
        Assert.Equal(1, await db.Clinics.CountAsync());
        Assert.Equal(2, await db.Branches.CountAsync());
        Assert.Equal(8, await db.Departments.CountAsync());
        Assert.Equal(16, await db.Doctors.CountAsync());
        Assert.Equal(24, await db.MedicalServices.CountAsync());
        Assert.Equal(30, await db.Patients.CountAsync());
        Assert.Equal(60, await db.Appointments.CountAsync());
        Assert.Equal(33, await db.Users.CountAsync());
        var hashes = await db.Users.ToDictionaryAsync(x => x.Id, x => x.PasswordHash);
        var original = await db.Appointments.ToDictionaryAsync(x => x.Id, x => (x.StartsAt, x.Status, x.Price));
        var count = await db.StaffAvailabilities.CountAsync();
        await RealisticDemoSeed.RunAsync(app);
        Assert.Equal(count, await db.StaffAvailabilities.CountAsync());
        // Rerunning on a later date extends availability, never recreates appointment history.
        await using var later = fixture.App(now: now.AddDays(8));
        await RealisticDemoSeed.RunAsync(later);
        db.ChangeTracker.Clear();
        Assert.Equal(60, await db.Appointments.CountAsync());
        Assert.Equal(30, await db.Patients.CountAsync());
        Assert.Equal(16, await db.Doctors.CountAsync());
        Assert.Equal(24, await db.MedicalServices.CountAsync());
        foreach (var user in await db.Users.ToListAsync())
        {
            Assert.Equal(hashes[user.Id], user.PasswordHash);
            Assert.True(RealisticDemoSeed.IsDemoUser(user.Id));
            Assert.True(DemoSeed.IsDemoUser(user.Id));
            Assert.EndsWith(".test", user.Email);
        }
        var doctors = await db.Doctors.Include(d => d.Services).Include(d => d.Department).ThenInclude(d => d.Branch).ToListAsync();
        foreach (var doctor in doctors)
        {
            Assert.Equal(doctor.Department.Name, doctor.Specialty);
            Assert.Equal(3, doctor.Services.Count);
            Assert.All(doctor.Services, s => Assert.Equal(doctor.DepartmentId, s.DepartmentId));
        }
        var intervals = await db.StaffAvailabilities.Include(a => a.Staff).ToListAsync();
        var hours = await db.WorkingHours.ToListAsync();
        foreach (var interval in intervals)
        {
            Assert.True(AppointmentBooking.BranchOpen(hours.Where(h => h.BranchId == interval.Staff.Department.BranchId).ToList(), interval.StartsAt, interval.EndsAt));
            Assert.DoesNotContain(intervals, x => x.Id != interval.Id && x.StaffId == interval.StaffId && x.StartsAt < interval.EndsAt && interval.StartsAt < x.EndsAt);
        }
        var appointments = await db.Appointments.Include(a => a.Patient).Include(a => a.Doctor).Include(a => a.Service).ThenInclude(s => s.Department).ThenInclude(d => d.Branch).ToListAsync();
        Assert.Equal(5, appointments.Select(a => a.Status).Distinct().Count());
        Assert.Equal(12, appointments.Count(a => a.Status == AppointmentStatus.Completed));
        Assert.Equal(6, appointments.Count(a => a.Status == AppointmentStatus.Rejected));
        Assert.Equal(6, appointments.Count(a => a.Status == AppointmentStatus.Cancelled));
        Assert.Equal(18, appointments.Count(a => a.Status == AppointmentStatus.Pending));
        Assert.Equal(18, appointments.Count(a => a.Status == AppointmentStatus.Confirmed));
        foreach (var a in appointments)
        {
            Assert.Equal(original[a.Id], (a.StartsAt, a.Status, a.Price));
            Assert.True(a.Doctor.Offers(a.Service));
            Assert.Equal(a.Service.Duration, a.EndsAt - a.StartsAt);
            Assert.Equal(a.Service.Price, a.Price);
            Assert.Contains(intervals, x => x.StaffId == a.DoctorId && x.Covers(a.StartsAt, a.EndsAt));
            Assert.DoesNotContain(appointments, x => x.Id != a.Id && (x.DoctorId == a.DoctorId || x.PatientId == a.PatientId) && x.StartsAt < a.EndsAt && a.StartsAt < x.EndsAt);
            if (a.Status == AppointmentStatus.Completed) Assert.True(a.EndsAt <= now);
            if (a.Status is AppointmentStatus.Cancelled or AppointmentStatus.Rejected) Assert.False(string.IsNullOrWhiteSpace(a.DecisionReason));
        }
    }

    [Fact]
    public async Task LegacyAccountsAndAppointmentsSurviveAndScopesRemainIsolated()
    {
        await using var fixture = new Fixture();
        await using var app = fixture.App();
        await Migrate(app);
        await DemoSeed.RunAsync(app);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediClinicDbContext>();
        var hashes = await db.Users.ToDictionaryAsync(x => x.Id, x => x.PasswordHash);
        var doctor = await db.Doctors.Include(x => x.Department).ThenInclude(x => x.Services).FirstAsync();
        var patient = await db.Patients.SingleAsync();
        var existing = new Appointment(Guid.NewGuid(), patient, doctor, doctor.Department.Services.Single(), Scheduling.ToUtc(new DateTime(2026, 11, 9, 9, 0, 0)));
        existing.Confirm(); db.Add(existing); await db.SaveChangesAsync();
        await RealisticDemoSeed.RunAsync(app);
        await RealisticDemoSeed.RunAsync(app);
        db.ChangeTracker.Clear();
        Assert.Equal(61, await db.Appointments.CountAsync());
        Assert.Equal(19, await db.Doctors.CountAsync());
        Assert.Equal(10, await db.Departments.CountAsync());
        Assert.Equal(26, await db.MedicalServices.CountAsync());
        Assert.Equal(31, await db.Patients.CountAsync());
        foreach (var hash in hashes) Assert.Equal(hash.Value, (await db.Users.FindAsync(hash.Key))!.PasswordHash);
        Assert.Equal(AppointmentStatus.Confirmed, (await db.Appointments.FindAsync(existing.Id))!.Status);
        var booking = scope.ServiceProvider.GetRequiredService<IAppointmentBooking>();
        var staff0 = RealisticDemoSeed.Id("user/staff/0").ToString();
        var staff1 = RealisticDemoSeed.Id("user/staff/1").ToString();
        var scoped = await booking.StaffAppointments(staff0, null, null);
        Assert.NotEmpty(scoped);
        foreach (var a in scoped) Assert.Null(await booking.StaffAppointment(staff1, a.Appointment.Id));
        Assert.Equal(61, (await booking.AdminAppointments(RealisticDemoSeed.Id("user/admin").ToString(), null, null, null)).Count);
        var foreign = new Clinic(Guid.NewGuid(), "Başka klinik"); db.Add(foreign);
        var foreignDepartment = foreign.AddBranch(Guid.NewGuid(), "Başka şube", "Sentetik adres").AddDepartment(Guid.NewGuid(), "Dahiliye");
        var foreignService = foreignDepartment.AddService(Guid.NewGuid(), "Muayene", TimeSpan.FromMinutes(30));
        var foreignDoctor = new Doctor(Guid.NewGuid(), "Sentetik", "Hekim", foreignDepartment, "Dahiliye"); db.Add(foreignDoctor);
        var retainedPatient = (await db.Patients.FindAsync(patient.Id))!;
        var foreignAppointment = new Appointment(Guid.NewGuid(), retainedPatient, foreignDoctor, foreignService, existing.StartsAt); db.Add(foreignAppointment);
        await db.SaveChangesAsync();
        Assert.Null(await booking.AdminAppointment(RealisticDemoSeed.Id("user/admin").ToString(), foreignAppointment.Id));
        Assert.Null(await booking.Find(RealisticDemoSeed.Id("user/patient/0").ToString(), existing.Id, false));
    }

    [Fact]
    public async Task IncompatibleExistingScheduleIsPreservedAndSeedRollsBack()
    {
        await using var fixture = new Fixture();
        await using var app = fixture.App();
        await Migrate(app);
        await DemoSeed.RunAsync(app);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediClinicDbContext>();
        var doctor = await db.Doctors.FirstAsync();
        var start = Scheduling.ToUtc(new DateTime(2026, 11, 14, 9, 0, 0)); // Saturday
        var interval = new StaffAvailability(Guid.NewGuid(), doctor, start, start.AddHours(2));
        db.Add(interval); await db.SaveChangesAsync();
        var count = await db.StaffAvailabilities.CountAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => RealisticDemoSeed.RunAsync(app));
        db.ChangeTracker.Clear();
        Assert.Equal(count, await db.StaffAvailabilities.CountAsync());
        Assert.True(await db.StaffAvailabilities.AnyAsync(a => a.Id == interval.Id));
        Assert.Equal(0, await db.WorkingHours.CountAsync());
        Assert.Equal(3, await db.Users.CountAsync());
        Assert.Equal(3, await db.Doctors.CountAsync());
    }

    [Fact]
    public async Task AccountCollisionRollsBackAndPreservesExistingUser()
    {
        await using var fixture = new Fixture();
        await using var app = fixture.App();
        await Migrate(app);
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var candidate = ApplicationUser.Administrator("hospital.patient.01@mediclinic.test");
        Assert.True((await users.CreateAsync(candidate)).Succeeded);
        await Assert.ThrowsAsync<InvalidOperationException>(() => RealisticDemoSeed.RunAsync(app));
        var db = scope.ServiceProvider.GetRequiredService<MediClinicDbContext>();
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(0, await db.Clinics.CountAsync());
        Assert.Equal(candidate.Id, (await users.FindByEmailAsync(candidate.Email!))!.Id);
    }
}
