using System.Net;
using System.Text.RegularExpressions;
using MediClinic.Application;
using MediClinic.Domain;
using MediClinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MediClinic.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MediClinic.Tests;

public sealed class EmployeeManagementTests
{
    private sealed class BeforeSave(Func<Task> action) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { await action(); return result; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BookingRollsBackIfScheduleOrAssignmentChangesAfterPrechecks(bool assignment)
    {
        await using var data = await BookingTests.Database.Create();
        var interceptor = new BeforeSave(async () =>
        {
            await using var changed = data.Context();
            if (assignment) await changed.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM DoctorServices WHERE DoctorId = {data.Fixture.Doctor.Id}");
            else await changed.StaffAvailabilities.ExecuteDeleteAsync();
        });
        await using var booking = data.Context(interceptor);
        await Assert.ThrowsAsync<BookingException>(() => data.Service(booking).Book(data.PatientId, data.Request()));
        await using var verify = data.Context(); Assert.Empty(await verify.Appointments.ToListAsync());
    }
    [Fact]
    public async Task MigrationBackfillsLegacyCompatibilityAndPreservesHistoryAndAccounts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new MediClinicDbContext(new DbContextOptionsBuilder<MediClinicDbContext>().UseSqlite(connection).Options);
        await db.GetService<IMigrator>().MigrateAsync("20261112154350_ClinicManagement");
        var fixture = new DomainFixture(); var appointment = fixture.Appointment();
        await fixture.SeedLegacy(db, appointment);
        var account = ApplicationUser.Administrator("preserved@example.test"); db.Users.Add(account); await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        var doctor = await db.Doctors.Include(d => d.Services).Include(d => d.Department).SingleAsync();
        Assert.Equal(fixture.Service.Id, Assert.Single(doctor.Services).Id);
        Assert.True(doctor.Offers(doctor.Services.Single()));
        Assert.Equal(appointment.Id, (await db.Appointments.SingleAsync()).Id);
        Assert.Equal(appointment.EndsAt, (await db.Appointments.SingleAsync()).EndsAt);
        Assert.Equal(account.Id, (await db.Users.SingleAsync()).Id);
        Assert.Empty(await db.ClinicAdministrators.ToListAsync());
        var newService = doctor.Department.AddService(Guid.NewGuid(), "New service", TimeSpan.FromMinutes(20));
        db.Add(newService); await db.SaveChangesAsync();
        Assert.False(doctor.Offers(newService));
    }

    [Fact]
    public async Task DepartmentReassignmentChecksScopeAndPreservesAppointmentRelationships()
    {
        await using var data = await BookingTests.Database.Create();
        Guid departmentId, serviceId;
        await using (var db = data.Context())
        {
            var branch = await db.Branches.SingleAsync(b => b.Id == data.Fixture.Department.BranchId);
            var department = branch.AddDepartment(Guid.NewGuid(), "İkinci bölüm");
            var service = department.AddService(Guid.NewGuid(), "Başka bölüm hizmeti", TimeSpan.FromMinutes(30));
            db.Add(department); db.Add(service); await db.SaveChangesAsync(); departmentId = department.Id; serviceId = service.Id;
        }
        using var site = new BookingMvcTests.Site(data); using var admin = await Admin(site, data);
        var form = Employee(data); form["IsDoctor"] = "true"; form["Specialty"] = "Uzmanlık"; form["ServiceIds"] = serviceId.ToString();
        Assert.Equal(HttpStatusCode.OK, (await Post(admin, "/EmployeeManagement/Edit", "Edit", form)).StatusCode);
        form.Remove("ServiceIds");
        Assert.Equal(HttpStatusCode.Redirect, (await Post(admin, "/EmployeeManagement/Edit", "Edit", form)).StatusCode);
        Guid createdId;
        await using (var db = data.Context()) createdId = (await db.Doctors.SingleAsync(d => d.FirstName == "Yeni")).Id;
        form["Id"] = createdId.ToString(); form["DepartmentId"] = departmentId.ToString(); form["ServiceIds"] = serviceId.ToString();
        Assert.Equal(HttpStatusCode.Redirect, (await Post(admin, $"/EmployeeManagement/Edit/{createdId}", "Edit", form)).StatusCode);
        await using (var db = data.Context())
        {
            Assert.Equal(departmentId, (await db.Doctors.Include(d => d.Department).SingleAsync(d => d.Id == createdId)).DepartmentId);
            await data.Service(db).Book(data.PatientId, data.Request());
            await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO DoctorServices (DoctorId, ServiceId) VALUES ({data.Fixture.Doctor.Id}, {serviceId})"));
        }
        form["Id"] = data.Fixture.Doctor.Id.ToString();
        Assert.Equal(HttpStatusCode.OK, (await Post(admin, $"/EmployeeManagement/Edit/{data.Fixture.Doctor.Id}", "Edit", form)).StatusCode);
        await using var verify = data.Context();
        Assert.Equal(data.Fixture.Department.Id, (await verify.Doctors.Include(d => d.Department).SingleAsync(d => d.Id == data.Fixture.Doctor.Id)).DepartmentId);
        Assert.Equal(data.Fixture.Doctor.Id, (await verify.Appointments.Include(a => a.Doctor).SingleAsync()).DoctorId);
    }
    private static async Task<HttpClient> Admin(BookingMvcTests.Site site, BookingTests.Database data, bool grant = true)
    {
        using var scope = site.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole(AuthRoles.ClinicAdmin))).Succeeded);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(data.UnlinkedStaffId))!, AuthRoles.ClinicAdmin)).Succeeded);
        if (grant)
        {
            await using var db = data.Context();
            db.ClinicAdministrators.Add(new() { UserId = data.UnlinkedStaffId, ClinicId = data.Fixture.Clinic.Id });
            await db.SaveChangesAsync();
        }
        return await site.Login(data.UnlinkedStaffId);
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client, string page, string action, Dictionary<string, string> values)
    {
        var html = await client.GetStringAsync(page);
        values["__RequestVerificationToken"] = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(values["__RequestVerificationToken"]);
        return await client.PostAsync("/EmployeeManagement/" + action, new FormUrlEncodedContent(values));
    }
    private static Dictionary<string, string> Employee(BookingTests.Database data) => new()
    {
        ["FirstName"] = "Yeni", ["LastName"] = "Çalışan", ["JobTitle"] = "Hemşire", ["Specialty"] = "", ["DepartmentId"] = data.Fixture.Department.Id.ToString()
    };
    private static Dictionary<string, string> Interval(BookingTests.Database data, DateTimeOffset start, DateTimeOffset end, Guid? id = null) => new()
    {
        ["DoctorId"] = data.Fixture.Doctor.Id.ToString(), ["StartsAt"] = Scheduling.Local(start).ToString("yyyy-MM-ddTHH:mm"),
        ["EndsAt"] = Scheduling.Local(end).ToString("yyyy-MM-ddTHH:mm"), ["Id"] = id?.ToString() ?? ""
    };

    [Fact]
    public async Task StaffCreationEditingAndProvisioningAreAtomicAndStaffOnly()
    {
        await using var data = await BookingTests.Database.Create(); using var site = new BookingMvcTests.Site(data); using var admin = await Admin(site, data);
        var form = Employee(data); form["Email"] = "new.employee@example.test"; form["InitialPassword"] = "Course-Employee-2030!";
        Assert.Equal(HttpStatusCode.Redirect, (await Post(admin, "/EmployeeManagement/Edit", "Edit", form)).StatusCode);
        Guid id;
        using (var scope = site.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var account = (await users.FindByEmailAsync(form["Email"]))!;
            Assert.NotNull(account); Assert.Null(account.PatientId); Assert.NotNull(account.StaffId);
            Assert.Equal(new[] { AuthRoles.Staff }, await users.GetRolesAsync(account));
            Assert.True(await users.CheckPasswordAsync(account, form["InitialPassword"])); id = account.StaffId!.Value;
        }
        form.Remove("Email"); form.Remove("InitialPassword"); form["Id"] = id.ToString(); form["FirstName"] = "Düzenlenen";
        Assert.Equal(HttpStatusCode.Redirect, (await Post(admin, $"/EmployeeManagement/Edit/{id}", "Edit", form)).StatusCode);
        await using var db = data.Context();
        Assert.Equal("Düzenlenen", (await db.Staff.SingleAsync(s => s.Id == id)).FirstName);
        form.Remove("Id");
        var duplicate = await Post(admin, "/EmployeeManagement/Edit", "Edit", form);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode); Assert.Contains("validation-summary-errors", await duplicate.Content.ReadAsStringAsync());
        form["FirstName"] = "ParolaHatası"; form["Email"] = "invalid.employee@example.test"; form["InitialPassword"] = "weak-secret";
        var invalid = await Post(admin, "/EmployeeManagement/Edit", "Edit", form);
        Assert.DoesNotContain("weak-secret", await invalid.Content.ReadAsStringAsync());
        Assert.False(await db.Staff.AnyAsync(s => s.FirstName == "ParolaHatası"));
        Assert.False(await db.Users.AnyAsync(u => u.Email == form["Email"]));
        using var staff = await site.Login(data.StaffId);
        Assert.Equal(HttpStatusCode.Redirect, (await staff.GetAsync("/EmployeeManagement")).StatusCode);
    }

    [Fact]
    public async Task RoleScopeAndAntiforgeryProtectEveryOperation()
    {
        await using var data = await BookingTests.Database.Create(); using var site = new BookingMvcTests.Site(data);
        using var anonymous = site.Client();
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/EmployeeManagement")).StatusCode);
        using var patient = await site.Login(data.PatientId);
        Assert.Equal(HttpStatusCode.Redirect, (await patient.GetAsync("/EmployeeManagement")).StatusCode);
        using var admin = await Admin(site, data);
        var html = await admin.GetStringAsync("/EmployeeManagement");
        Assert.Contains(data.Fixture.Doctor.Id.ToString(), html); Assert.DoesNotContain(data.Other.Doctor.Id.ToString(), html);
        foreach (var action in new[] { "Edit", "Availability", "Interval" })
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/EmployeeManagement/{action}?id={data.Other.Doctor.Id}&doctorId={data.Other.Doctor.Id}")).StatusCode);
        foreach (var action in new[] { "Edit", "Interval", "RemoveInterval" })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/EmployeeManagement/" + action, new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        var form = Employee(data); form["DepartmentId"] = data.Other.Department.Id.ToString();
        Assert.Equal(HttpStatusCode.NotFound, (await Post(admin, "/EmployeeManagement/Edit", "Edit", form)).StatusCode);
        form = Employee(data); form["Id"] = data.Other.Doctor.Id.ToString();
        Assert.Equal(HttpStatusCode.NotFound, (await Post(admin, "/EmployeeManagement/Edit", "Edit", form)).StatusCode);
        var interval = Interval(data, data.Fixture.Start.AddDays(1), data.Fixture.Start.AddDays(1).AddHours(1));
        interval["DoctorId"] = data.Other.Doctor.Id.ToString();
        Assert.Equal(HttpStatusCode.NotFound, (await Post(admin, "/EmployeeManagement/Edit", "Interval", interval)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(admin, "/EmployeeManagement/Edit", "RemoveInterval", interval)).StatusCode);
    }

    [Fact]
    public async Task UnlinkedAdminHasNoEmployeeAuthority()
    {
        await using var data = await BookingTests.Database.Create(); using var site = new BookingMvcTests.Site(data); using var admin = await Admin(site, data, false);
        Assert.DoesNotContain(data.Fixture.Doctor.Id.ToString(), await admin.GetStringAsync("/EmployeeManagement"));
        var denied = await admin.GetAsync("/EmployeeManagement/Edit");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("AccessDenied", denied.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/EmployeeManagement/Availability?doctorId={data.Fixture.Doctor.Id}")).StatusCode);
    }

    [Fact]
    public async Task DoctorServicesRestrictAllBookingPathsWithoutChangingAppointments()
    {
        await using var data = await BookingTests.Database.Create(); using var site = new BookingMvcTests.Site(data); using var admin = await Admin(site, data);
        Guid appointmentId;
        Guid secondId;
        await using (var db = data.Context())
        {
            appointmentId = await data.Service(db).Book(data.PatientId, data.Request());
            var department = await db.Departments.SingleAsync(d => d.Id == data.Fixture.Department.Id);
            var second = department.AddService(Guid.NewGuid(), "İkinci hizmet", TimeSpan.FromMinutes(15)); db.Add(second); await db.SaveChangesAsync(); secondId = second.Id;
            Assert.DoesNotContain(await data.Service(db).Options(data.PatientId), o => o.ServiceId == secondId);
            await Assert.ThrowsAsync<BookingException>(() => data.Service(db).Slots(data.PatientId, data.Fixture.Doctor.Id, secondId, new(2030, 1, 2)));
            await Assert.ThrowsAsync<BookingException>(() => data.Service(db).Book(data.PatientId, data.Request() with { ServiceId = secondId }));
        }
        var form = Employee(data); form["Id"] = data.Fixture.Doctor.Id.ToString(); form["IsDoctor"] = "true"; form["Specialty"] = "Yeni uzmanlık";
        form["ServiceIds"] = data.Other.Service.Id.ToString();
        Assert.Equal(HttpStatusCode.OK, (await Post(admin, $"/EmployeeManagement/Edit/{data.Fixture.Doctor.Id}", "Edit", form)).StatusCode);
        form["ServiceIds"] = secondId.ToString();
        Assert.Equal(HttpStatusCode.Redirect, (await Post(admin, $"/EmployeeManagement/Edit/{data.Fixture.Doctor.Id}", "Edit", form)).StatusCode);
        await using var verify = data.Context();
        var doctor = await verify.Doctors.Include(d => d.Services).SingleAsync(d => d.Id == data.Fixture.Doctor.Id);
        Assert.Equal("Yeni uzmanlık", doctor.Specialty); Assert.Equal(secondId, Assert.Single(doctor.Services).Id);
        Assert.Equal(appointmentId, (await verify.Appointments.SingleAsync()).Id);
        var options = await data.Service(verify).Options(data.PatientId);
        Assert.Contains(options, o => o.ServiceId == secondId); Assert.DoesNotContain(options, o => o.DoctorId == doctor.Id && o.ServiceId == data.Fixture.Service.Id);
        await Assert.ThrowsAsync<BookingException>(() => data.Service(verify).Book(data.PatientId, data.Request(data.Fixture.Start.AddHours(1))));
        form.Remove("ServiceIds"); form["ServiceIds[0]"] = secondId.ToString(); form["ServiceIds[1]"] = data.Fixture.Service.Id.ToString();
        form["JobTitle"] = "";
        Assert.Equal(HttpStatusCode.Redirect, (await Post(admin, $"/EmployeeManagement/Edit/{doctor.Id}", "Edit", form)).StatusCode);
        await using var multiple = data.Context();
        Assert.Equal(2, (await data.Service(multiple).Options(data.PatientId)).Count(o => o.DoctorId == doctor.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AvailabilityValidatesFutureOverlapOwnershipAndAppointmentCoverage(bool confirmed)
    {
        await using var data = await BookingTests.Database.Create();
        data.Time.Now = data.Fixture.Start.AddMinutes(-5);
        using var site = new BookingMvcTests.Site(data); using var admin = await Admin(site, data);
        var page = $"/EmployeeManagement/Interval?doctorId={data.Fixture.Doctor.Id}";
        var start = data.Fixture.Start.AddDays(1);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(admin, page, "Interval", Interval(data, start, start.AddHours(2)))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(admin, page, "Interval", Interval(data, start.AddHours(1), start.AddHours(3)))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(admin, page, "Interval", Interval(data, start, start))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(admin, page, "Interval", Interval(data, data.Time.Now.AddDays(-2), data.Time.Now.AddDays(-1)))).StatusCode);
        Guid intervalId;
        await using (var db = data.Context())
        {
            await data.Service(db).Book(data.PatientId, data.Request());
            if (confirmed) { (await db.Appointments.SingleAsync()).Confirm(); await db.SaveChangesAsync(); }
            intervalId = (await db.StaffAvailabilities.SingleAsync(a => a.StartsAt == data.Fixture.Start)).Id;
        }
        Assert.Equal(HttpStatusCode.OK, (await Post(admin, page, "Interval", Interval(data, data.Fixture.Start.AddHours(1), data.Fixture.Start.AddHours(3), intervalId))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(admin, page, "Interval", Interval(data, data.Fixture.Start, data.Fixture.Start.AddHours(1), intervalId))).StatusCode);
        var remove = new Dictionary<string, string> { ["DoctorId"] = data.Fixture.Doctor.Id.ToString(), ["Id"] = intervalId.ToString() };
        await Post(admin, page, "RemoveInterval", remove);
        await using (var db = data.Context()) Assert.True(await db.StaffAvailabilities.AnyAsync(a => a.Id == intervalId));
        var html = WebUtility.HtmlDecode(await admin.GetStringAsync($"/EmployeeManagement/Availability?doctorId={data.Fixture.Doctor.Id}&date=2030-01-02"));
        Assert.Contains("12:00", html); Assert.DoesNotContain("03.01.2030", html);
        Guid safeId;
        await using (var db = data.Context()) safeId = (await db.StaffAvailabilities.SingleAsync(a => a.StartsAt == start)).Id;
        remove["Id"] = safeId.ToString(); await Post(admin, page, "RemoveInterval", remove);
        await using (var db = data.Context()) Assert.False(await db.StaffAvailabilities.AnyAsync(a => a.Id == safeId));
        data.Time.Now = data.Fixture.Start.AddMinutes(1);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(page + "&id=" + intervalId)).StatusCode);
    }
}
