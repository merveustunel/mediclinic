using System.Net;
using MediClinic.Application;
using MediClinic.Domain;
using MediClinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediClinic.Tests;

public sealed class StaffDashboardTests
{
    [Fact]
    public async Task MetricsUseIstanbulStartDateAllStatusesAndExclusiveMidnight()
    {
        await using var data = await BookingTests.Database.Create();
        await using var db = data.Context();
        var f = data.Fixture;
        db.AttachRange(f.Patient, f.Doctor, f.Service);
        var start = Scheduling.ToUtc(new DateTime(2030, 1, 2));
        data.Time.Now = start.AddMinutes(1); // Still January 1 in UTC.
        var times = new[] { start.AddMinutes(-30), start, start.AddHours(1), start.AddHours(2), start.AddHours(3), start.AddDays(1) };
        for (var i = 0; i < times.Length; i++)
        {
            var item = new Appointment(Guid.NewGuid(), f.Patient, f.Doctor, f.Service, times[i]);
            if (i == 2) item.Confirm();
            if (i == 3) { item.Confirm(); item.Complete(item.EndsAt); }
            if (i == 4) item.Cancel("Private reason");
            db.Add(item);
        }
        await db.SaveChangesAsync();
        var result = await data.Service(db).Dashboard(data.StaffId);
        Assert.Equal(4, result.TodayCount);
        Assert.Equal(3, result.PendingCount);
        Assert.Equal(1, result.ConfirmedCount);
        Assert.Equal(1, result.CompletedCount);
        Assert.Equal(new[] { start.AddDays(1), start.AddMinutes(-30), start }, result.Pending.Select(x => x.StartsAt));
        Assert.Equal(times.Skip(1).Take(4), result.Today.Select(x => x.StartsAt));
    }

    [Fact]
    public async Task PendingOrderingIsStableAndCompactButMetricCountsAllRequests()
    {
        await using var data = await BookingTests.Database.Create();
        await using var db = data.Context();
        var f = data.Fixture;
        db.AttachRange(f.Patient, f.Doctor, f.Service);
        data.Time.Now = f.Start;
        var ids = new List<Guid>();
        for (var i = 8; i >= -1; i--)
        {
            var item = new Appointment(Guid.NewGuid(), f.Patient, f.Doctor, f.Service, f.Start.AddHours(i));
            db.Add(item); ids.Add(item.Id);
        }
        // Terminal appointments may share a start; they must not enter the pending list.
        var cancelled = new Appointment(Guid.NewGuid(), f.Patient, f.Doctor, f.Service, f.Start);
        cancelled.Cancel("Private"); db.Add(cancelled);
        await db.SaveChangesAsync();
        var result = await data.Service(db).Dashboard(data.StaffId);
        Assert.Equal(10, result.PendingCount);
        Assert.Equal(6, result.Pending.Count);
        Assert.Equal(Enumerable.Range(0, 6).Select(i => f.Start.AddHours(i)), result.Pending.Select(x => x.StartsAt));
        Assert.Equal(result.Pending, (await data.Service(db).Dashboard(data.StaffId)).Pending);
    }

    [Fact]
    public async Task ServerQueriesIsolateOtherClinicsBranchesAndDepartments()
    {
        await using var data = await BookingTests.Database.Create();
        await using var db = data.Context();
        var f = data.Fixture;
        db.AttachRange(f.Patient, f.Doctor, f.Service);
        var branchDepartment = f.Clinic.AddBranch(Guid.NewGuid(), "Other branch", "Address").AddDepartment(Guid.NewGuid(), "Other branch department");
        var sameBranchDepartment = f.Department.Branch.AddDepartment(Guid.NewGuid(), "Other department");
        foreach (var department in new[] { branchDepartment, sameBranchDepartment })
        {
            var doctor = new Doctor(Guid.NewGuid(), "Hidden", "Doctor", department, "Specialty");
            var service = department.AddService(Guid.NewGuid(), "Hidden service", TimeSpan.FromMinutes(30));
            db.Add(new Appointment(Guid.NewGuid(), f.Patient, doctor, service, f.Start));
        }
        db.AttachRange(data.Other.Patient, data.Other.Doctor, data.Other.Service);
        db.Add(data.Other.Appointment()); db.Add(f.Appointment()); await db.SaveChangesAsync();
        data.Time.Now = f.Start;
        var result = await data.Service(db).Dashboard(data.StaffId);
        Assert.Equal(1, result.TodayCount); Assert.Equal(1, result.PendingCount);
        Assert.All(result.Today.Concat(result.Pending), x => Assert.Equal(f.Doctor.FullName, x.Doctor));
        await Assert.ThrowsAsync<BookingException>(() => data.Service(db).Dashboard(data.PatientId));
        await Assert.ThrowsAsync<BookingException>(() => data.Service(db).Dashboard(data.UnlinkedStaffId));
    }

    [Fact]
    public async Task MvcRendersNamesStatusesLocalTimeLinksAndEmptyStatesWithoutPrivateReasons()
    {
        await using var data = await BookingTests.Database.Create();
        Guid id;
        await using (var db = data.Context()) id = await data.Service(db).Book(data.PatientId, data.Request());
        data.Time.Now = data.Fixture.Start;
        using var site = new BookingMvcTests.Site(data);
        using var staff = await site.Login(data.StaffId);
        var html = WebUtility.HtmlDecode(await staff.GetStringAsync("/Dashboard/Staff"));
        Assert.Contains(data.Fixture.Patient.FullName, html);
        Assert.Contains("02 Ocak 2030, 12:00", html);
        Assert.Contains("Beklemede", html);
        Assert.Contains($"/StaffAppointments/Details/{id}", html);
        Assert.Contains("<dt>Bugünkü randevular</dt><dd>1</dd>", html);
        using var other = await site.Login(data.OtherStaffId);
        var empty = WebUtility.HtmlDecode(await other.GetStringAsync($"/Dashboard/Staff?userId={data.StaffId}&branchId={data.Fixture.Department.Branch.Id}"));
        Assert.Contains("Bekleyen randevu talebi bulunmuyor.", empty);
        Assert.Contains("Bugün için randevu bulunmuyor.", empty);
        Assert.DoesNotContain(id.ToString(), empty);
        await using (var db = data.Context()) await data.Service(db).Act(data.PatientId, id, "Cancel", "Private reason");
        html = await staff.GetStringAsync("/Dashboard/Staff");
        Assert.DoesNotContain("Private reason", html);
        using var patient = await site.Login(data.PatientId);
        Assert.Contains("/Account/AccessDenied", (await patient.GetAsync("/Dashboard/Staff")).Headers.Location!.OriginalString);
        using var anonymous = site.Client();
        Assert.Contains("/Account/Login", (await anonymous.GetAsync("/Dashboard/Staff")).Headers.Location!.OriginalString);
        using var unlinked = await site.Login(data.UnlinkedStaffId);
        Assert.Contains("/Account/AccessDenied", (await unlinked.GetAsync("/Dashboard/Staff")).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task ClinicAdminReadAccessRequiresLinkedStaffAndPreservesDepartmentScope()
    {
        await using var data = await BookingTests.Database.Create();
        Guid id;
        await using (var db = data.Context())
        {
            id = await data.Service(db).Book(data.PatientId, data.Request());
            var role = new IdentityRole(AuthRoles.ClinicAdmin); db.Roles.Add(role);
            var staffRole = await db.UserRoles.SingleAsync(x => x.UserId == data.StaffId); db.UserRoles.Remove(staffRole);
            db.UserRoles.Add(new() { UserId = data.StaffId, RoleId = role.Id });
            db.UserRoles.Add(new() { UserId = data.UnlinkedStaffId, RoleId = role.Id });
            await db.SaveChangesAsync();
        }
        using var site = new BookingMvcTests.Site(data);
        using var admin = await site.Login(data.StaffId);
        foreach (var path in new[] { "/Dashboard/Staff", "/Dashboard/ClinicAdmin" })
            Assert.Contains(id.ToString(), await admin.GetStringAsync(path));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/StaffAppointments/Details/{id}")).StatusCode);
        using var unlinked = await site.Login(data.UnlinkedStaffId);
        Assert.Contains("/Account/AccessDenied", (await unlinked.GetAsync("/Dashboard/ClinicAdmin")).Headers.Location!.OriginalString);
        using var patient = await site.Login(data.PatientId);
        Assert.Contains("/Account/AccessDenied", (await patient.GetAsync("/Dashboard/ClinicAdmin")).Headers.Location!.OriginalString);
    }
}
