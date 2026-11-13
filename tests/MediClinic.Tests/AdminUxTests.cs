using System.Net;
using System.Text.RegularExpressions;
using MediClinic.Application;
using MediClinic.Domain;
using Microsoft.EntityFrameworkCore;

namespace MediClinic.Tests;

public sealed class AdminUxTests
{
    [Fact]
    public async Task AdminCanReviewAnotherDepartmentAndConcurrentDecisionIsRejected()
    {
        await using var data = await BookingTests.Database.Create(); using var site = new BookingMvcTests.Site(data);
        using var admin = await EmployeeManagementTests.Admin(site, data);
        Guid id;
        await using (var db = data.Context())
        {
            var branch = await db.Branches.SingleAsync(b => b.Id == data.Fixture.Department.BranchId);
            var department = branch.AddDepartment(Guid.NewGuid(), "İkinci bölüm");
            var service = department.AddService(Guid.NewGuid(), "Muayene", TimeSpan.FromMinutes(30));
            var doctor = new Doctor(Guid.NewGuid(), "Deniz", "Demo", department, "Uzman");
            var patient = await db.Patients.SingleAsync(p => p.Id == data.Fixture.Patient.Id);
            var item = new Appointment(Guid.NewGuid(), patient, doctor, service, data.Fixture.Start);
            db.Add(item); await db.SaveChangesAsync(); id = item.Id;
        }
        var page = $"/AdminAppointments/Details/{id}";
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(page)).StatusCode);
        await using var first = data.Context(); await using var second = data.Context();
        // Materialize both versions before the first decision is saved.
        await first.Appointments.SingleAsync(a => a.Id == id);
        await second.Appointments.SingleAsync(a => a.Id == id);
        await data.Service(first).AdminAct(data.UnlinkedStaffId, id, "Confirm", null);
        await Assert.ThrowsAsync<BookingException>(() => data.Service(second).AdminAct(data.UnlinkedStaffId, id, "Reject", "Uygun değil"));
        await using var verify = data.Context();
        Assert.Equal(AppointmentStatus.Confirmed, (await verify.Appointments.SingleAsync(a => a.Id == id)).Status);
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client, string page, string action, Dictionary<string, string> values)
    {
        var html = await client.GetStringAsync(page);
        values["__RequestVerificationToken"] = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        return await client.PostAsync(action, new FormUrlEncodedContent(values));
    }
    [Theory]
    [InlineData("Confirm", AppointmentStatus.Confirmed)]
    [InlineData("Reject", AppointmentStatus.Rejected)]
    public async Task AdminReviewsScopedAppointmentsAndCannotRepeatDecision(string action, AppointmentStatus expected)
    {
        await using var data = await BookingTests.Database.Create();
        Guid id, foreign;
        await using (var db = data.Context())
        {
            id = await data.Service(db).Book(data.PatientId, data.Request());
            var other = new Appointment(Guid.NewGuid(), data.Other.Patient, data.Other.Doctor, data.Other.Service, data.Other.Start);
            foreign = other.Id;
            // Use tracked entities to preserve the independent clinic fixture.
            var patient = await db.Patients.SingleAsync(p => p.Id == data.Other.Patient.Id);
            var doctor = await db.Doctors.Include(d => d.Department).SingleAsync(d => d.Id == data.Other.Doctor.Id);
            var service = await db.MedicalServices.Include(s => s.Department).SingleAsync(s => s.Id == data.Other.Service.Id);
            db.Add(new Appointment(foreign, patient, doctor, service, data.Other.Start)); await db.SaveChangesAsync();
        }
        using var site = new BookingMvcTests.Site(data);
        using var admin = await EmployeeManagementTests.Admin(site, data);
        var page = $"/AdminAppointments/Details/{id}";
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(page)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/AdminAppointments/Details/{foreign}")).StatusCode);
        var form = new Dictionary<string,string> { ["Id"] = id.ToString(), ["Action"] = action, ["Reason"] = "Uygun değil" };
        Assert.Equal(HttpStatusCode.Redirect, (await Post(admin, page, "/AdminAppointments/Review", form)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(admin, page, "/AdminAppointments/Review", form)).StatusCode);
        form["Id"] = foreign.ToString();
        Assert.Equal(HttpStatusCode.NotFound, (await Post(admin, page, "/AdminAppointments/Review", form)).StatusCode);
        await using var verify = data.Context();
        Assert.Equal(expected, (await verify.Appointments.SingleAsync(a => a.Id == id)).Status);
        Assert.Equal(AppointmentStatus.Pending, (await verify.Appointments.SingleAsync(a => a.Id == foreign)).Status);
    }
    [Fact]
    public async Task AdminFiltersSortsAndRetainsStaffIsolation()
    {
        await using var data = await BookingTests.Database.Create();
        Guid first, last;
        await using (var db = data.Context())
        {
            last = await data.Service(db).Book(data.PatientId, data.Request(data.Fixture.Start.AddHours(1)));
            first = await data.Service(db).Book(data.PatientId, data.Request());
        }
        using var site = new BookingMvcTests.Site(data); using var admin = await EmployeeManagementTests.Admin(site, data);
        var html = await admin.GetStringAsync("/AdminAppointments?from=2030-01-02&to=2030-01-02&status=Pending");
        Assert.True(html.IndexOf(first.ToString(), StringComparison.Ordinal) < html.IndexOf(last.ToString(), StringComparison.Ordinal));
        Assert.DoesNotContain(first.ToString(), await admin.GetStringAsync("/AdminAppointments?status=Confirmed"));
        Assert.DoesNotContain(first.ToString(), await admin.GetStringAsync("/AdminAppointments?from=2030-01-03"));
        foreach (var query in new[] { "from=2030-02-01&to=2030-01-01", "to=9999-12-31", "status=invalid", "from=invalid" })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/AdminAppointments?" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.GetAsync("/StaffAppointments")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/AdminAppointments/Review", new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
    }
    [Theory]
    [InlineData("09:00", "12:00", true)]
    [InlineData("12:00", "09:00", false)]
    [InlineData("09:00", "09:00", false)]
    public async Task WeeklyCreatesAtomicDailyIntervals(string opens, string closes, bool valid)
    {
        await using var data = await BookingTests.Database.Create(); using var site = new BookingMvcTests.Site(data);
        using var admin = await EmployeeManagementTests.Admin(site, data);
        var page = $"/EmployeeManagement/Weekly?doctorId={data.Fixture.Doctor.Id}";
        var form = new Dictionary<string,string> { ["DoctorId"] = data.Fixture.Doctor.Id.ToString(), ["From"] = "2030-01-07", ["To"] = "2030-01-14", ["Days"] = "Monday", ["Opens"] = opens, ["Closes"] = closes };
        await using var before = data.Context(); var count = await before.StaffAvailabilities.CountAsync();
        Assert.Equal(valid ? HttpStatusCode.Redirect : HttpStatusCode.OK, (await Post(admin, page, "/EmployeeManagement/Weekly", form)).StatusCode);
        await using var db = data.Context(); Assert.Equal(count + (valid ? 2 : 0), await db.StaffAvailabilities.CountAsync());
        Assert.Equal(HttpStatusCode.OK, (await Post(admin, page, "/EmployeeManagement/Weekly", form)).StatusCode);
        Assert.Equal(count + (valid ? 2 : 0), await db.StaffAvailabilities.CountAsync());
        form["DoctorId"] = data.Other.Doctor.Id.ToString();
        Assert.Equal(HttpStatusCode.NotFound, (await Post(admin, page, "/EmployeeManagement/Weekly", form)).StatusCode);
    }
    [Theory]
    [InlineData("PasswordTooShort", "Parola en az 12 karakter olmalıdır.")]
    [InlineData("PasswordRequiresDigit", "Parola en az bir rakam içermelidir.")]
    [InlineData("Unknown", "Hesap işlemi tamamlanamadı. Bilgilerinizi kontrol edip yeniden deneyiniz.")]
    public void IdentityMessagesAreTurkishAndSafe(string code, string expected)
        => Assert.Equal(expected, MediClinic.Web.Controllers.TurkishIdentityErrors.Message(code));
}
