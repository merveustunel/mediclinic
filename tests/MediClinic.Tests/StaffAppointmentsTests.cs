using System.Net;
using System.Text.RegularExpressions;
using MediClinic.Application;
using MediClinic.Domain;
using MediClinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace MediClinic.Tests;

public sealed class StaffAppointmentsTests
{
    [Fact]
    public async Task FiltersUseAllStatusesAndExclusiveIstanbulDayWithinDepartmentScope()
    {
        await using var data = await BookingTests.Database.Create();
        await using var db = data.Context();
        var f = data.Fixture;
        db.AttachRange(f.Patient, f.Doctor, f.Service);
        var start = Scheduling.ToUtc(new DateTime(2030, 1, 2));
        foreach (var status in Enum.GetValues<AppointmentStatus>())
        {
            var item = new Appointment(Guid.NewGuid(), f.Patient, f.Doctor, f.Service, start.AddHours((int)status));
            if (status == AppointmentStatus.Confirmed) item.Confirm();
            if (status == AppointmentStatus.Completed) { item.Confirm(); item.Complete(item.EndsAt); }
            if (status == AppointmentStatus.Rejected) item.Reject("Reason");
            if (status == AppointmentStatus.Cancelled) item.Cancel("Reason");
            db.Add(item);
        }
        db.Add(new Appointment(Guid.NewGuid(), f.Patient, f.Doctor, f.Service, start.AddDays(1)));
        var departments = new[] { f.Department.Branch.AddDepartment(Guid.NewGuid(), "Other department"), f.Clinic.AddBranch(Guid.NewGuid(), "Other branch", "Address").AddDepartment(Guid.NewGuid(), "Hidden") };
        foreach (var department in departments)
        {
            var doctor = new Doctor(Guid.NewGuid(), "Hidden", "Doctor", department, "Specialty");
            var service = department.AddService(Guid.NewGuid(), "Hidden", TimeSpan.FromMinutes(30));
            db.Add(new Appointment(Guid.NewGuid(), f.Patient, doctor, service, start));
        }
        db.AttachRange(data.Other.Patient, data.Other.Doctor, data.Other.Service);
        var hidden = data.Other.Appointment(); db.Add(hidden);
        await db.SaveChangesAsync();
        var booking = data.Service(db);
        Assert.Equal(6, (await booking.StaffAppointments(data.StaffId, null, null)).Count);
        foreach (var status in Enum.GetValues<AppointmentStatus>())
        {
            var row = Assert.Single(await booking.StaffAppointments(data.StaffId, status, new DateOnly(2030, 1, 2)));
            Assert.Equal(status, row.Appointment.Status); Assert.Equal(f.Patient.FullName, row.Patient);
        }
        Assert.Null(await booking.StaffAppointment(data.StaffId, hidden.Id));
        await Assert.ThrowsAsync<BookingException>(() => booking.StaffAppointments(data.PatientId, null, null));
    }

    [Fact]
    public async Task RenderedFiltersDetailsValidationAndCompletionRespectBusinessRules()
    {
        await using var data = await BookingTests.Database.Create();
        Guid id;
        await using (var db = data.Context()) id = await data.Service(db).Book(data.PatientId, data.Request());
        data.Time.Now = data.Fixture.Start.AddMinutes(1);
        using var site = new BookingMvcTests.Site(data); using var staff = await site.Login(data.StaffId);
        var path = $"/StaffAppointments/Details/{id}";
        var html = WebUtility.HtmlDecode(await staff.GetStringAsync(path));
        Assert.Contains(data.Fixture.Patient.FullName, html); Assert.Contains("02 Ocak 2030, 12:00", html);
        Assert.Contains("required maxlength=\"2000\"", html);
        Assert.Contains(id.ToString(), await staff.GetStringAsync("/StaffAppointments?status=Pending&date=2030-01-02"));
        Assert.Contains("Randevu bulunamadı", WebUtility.HtmlDecode(await staff.GetStringAsync("/StaffAppointments?status=Rejected")));
        foreach (var query in new[] { "status=2", "status=Pending,Confirmed", "date=bad", "date=9999-12-31" })
            Assert.Equal(HttpStatusCode.BadRequest, (await staff.GetAsync("/StaffAppointments?" + query)).StatusCode);
        var missing = await Post(staff, path, id, "Reject", " ");
        Assert.Contains("Ret gerekçesi zorunludur", WebUtility.HtmlDecode(await missing.Content.ReadAsStringAsync()));
        var longReason = await Post(staff, path, id, "Reject", new string('a', 2001));
        Assert.Contains("en fazla 2000", WebUtility.HtmlDecode(await longReason.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsync("/StaffAppointments/Review", new FormUrlEncodedContent(new Dictionary<string,string> { ["Id"] = id.ToString(), ["Action"] = "Confirm" }))).StatusCode);
        await Post(staff, path, id, "Confirm");
        html = WebUtility.HtmlDecode(await staff.GetStringAsync(path)); Assert.Contains("Randevu onaylandı.", html);
        Assert.DoesNotContain("value=\"Complete\"", html);
        var early = await Post(staff, path, id, "Complete"); Assert.Contains("İşlem gerçekleştirilemedi", WebUtility.HtmlDecode(await early.Content.ReadAsStringAsync()));
        data.Time.Now = data.Fixture.Start.AddMinutes(30);
        Assert.Contains("value=\"Complete\"", await staff.GetStringAsync(path));
        await Post(staff, path, id, "Complete");
        Assert.DoesNotContain("/StaffAppointments/Review", await staff.GetStringAsync(path));
        var invalid = await Post(staff, "/Account/Login", id, "Reject", "No");
        Assert.Contains("İşlem gerçekleştirilemedi", WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync()));
        await using var verify = data.Context(); Assert.Equal(AppointmentStatus.Completed, (await verify.Appointments.SingleAsync()).Status);
        var output = Environment.GetEnvironmentVariable("MEDICLINIC_UI_CAPTURE");
        if (output != null)
        {
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, "staff-list.html"), await staff.GetStringAsync("/StaffAppointments"));
            // Capture actionable form using an isolated pending appointment.
            var second = await data.Service(verify).Book(data.PatientId, data.Request(data.Fixture.Start.AddHours(1)));
            await File.WriteAllTextAsync(Path.Combine(output, "staff-details.html"), await staff.GetStringAsync($"/StaffAppointments/Details/{second}"));
        }
    }

    [Fact]
    public async Task AdminRemainsReadOnlyAndPatientAndOtherDepartmentCannotReview()
    {
        await using var data = await BookingTests.Database.Create(); Guid id;
        await using (var db = data.Context())
        {
            id = await data.Service(db).Book(data.PatientId, data.Request());
            var role = new IdentityRole(AuthRoles.ClinicAdmin); db.Add(role);
            db.UserRoles.Remove(await db.UserRoles.SingleAsync(x => x.UserId == data.StaffId));
            db.UserRoles.Add(new() { UserId = data.StaffId, RoleId = role.Id }); await db.SaveChangesAsync();
        }
        using var site = new BookingMvcTests.Site(data); using var admin = await site.Login(data.StaffId);
        var path = $"/StaffAppointments/Details/{id}";
        Assert.DoesNotContain("/StaffAppointments/Review", await admin.GetStringAsync(path));
        Assert.Contains(id.ToString(), await admin.GetStringAsync("/StaffAppointments"));
        using var patient = await site.Login(data.PatientId);
        Assert.Contains("AccessDenied", (await patient.GetAsync(path)).Headers.Location!.OriginalString);
        foreach (var client in new[] { admin, patient })
            Assert.Contains("AccessDenied", (await Post(client, "/Account/Login", id, "Confirm")).Headers.Location!.OriginalString);
        using var other = await site.Login(data.OtherStaffId);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(other, "/Account/Login", id, "Confirm")).StatusCode);
        Assert.Contains("Randevu bulunamadı", WebUtility.HtmlDecode(await other.GetStringAsync("/StaffAppointments")));
        await using var verify = data.Context(); Assert.Equal(AppointmentStatus.Pending, (await verify.Appointments.SingleAsync()).Status);
        await Assert.ThrowsAsync<BookingException>(() => data.Service(verify).Act(data.StaffId, id, "Confirm", null));
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string page, Guid id, string action, string? reason = null)
    {
        var html = await client.GetStringAsync(page);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        return await client.PostAsync("/StaffAppointments/Review", new FormUrlEncodedContent(new Dictionary<string,string>
        { ["Id"] = id.ToString(), ["Action"] = action, ["Reason"] = reason ?? "", ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token) }));
    }
}
