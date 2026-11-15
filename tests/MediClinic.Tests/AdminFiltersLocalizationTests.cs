using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MediClinic.Application;
using MediClinic.Domain;
using MediClinic.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace MediClinic.Tests;

public sealed class AdminFiltersLocalizationTests
{
    [Fact]
    public async Task NestedNavigationRetainsParentInBothCultures()
    {
        await using var data = await BookingTests.Database.Create();
        using var site = new BookingMvcTests.Site(data);
        using var admin = await EmployeeManagementTests.Admin(site, data);
        Guid appointmentId;
        await using (var db = data.Context()) appointmentId = await data.Service(db).Book(data.PatientId, data.Request());
        var pages = new[]
        {
            ($"/AdminAppointments/Details/{appointmentId}", "/AdminAppointments"),
            ($"/EmployeeManagement/Availability?doctorId={data.Fixture.Doctor.Id}", "/EmployeeManagement"),
            ($"/EmployeeManagement/Edit?id={data.Fixture.Doctor.Id}", "/EmployeeManagement"),
            ($"/ClinicManagement/Edit?kind=Department&id={data.Fixture.Department.Id}", "/ClinicManagement"),
            ($"/ClinicManagement/Edit?kind=Service&id={data.Fixture.Service.Id}", "/ClinicManagement")
        };
        foreach (var culture in new[] { "tr-TR", "en-GB" })
        {
            admin.DefaultRequestHeaders.Remove("Cookie");
            admin.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Culture=c%3D{culture}%7Cuic%3D{culture}");
            foreach (var (page, parent) in pages)
            {
                var html = await admin.GetStringAsync(page);
                var navigation = Regex.Match(html, "<details class=\"navigation\".*?</details>", RegexOptions.Singleline).Value;
                var active = Regex.Matches(navigation, "<a[^>]*aria-current=\"page\"[^>]*>");
                Assert.Single(active);
                Assert.Contains("href=\"" + parent + "\"", active[0].Value);
            }
        }
    }

    [Fact]
    public async Task DepartmentsFromAnotherAuthorizedBranchAreRejected()
    {
        await using var data = await BookingTests.Database.Create();
        using var site = new BookingMvcTests.Site(data);
        using var admin = await EmployeeManagementTests.Admin(site, data);
        Guid departmentId;
        await using (var db = data.Context())
        {
            var clinic = await db.Clinics.SingleAsync(c => c.Id == data.Fixture.Clinic.Id);
            var department = clinic.AddBranch(Guid.NewGuid(), "Riverside", "Test address").AddDepartment(Guid.NewGuid(), "Dermatoloji");
            db.Add(department); await db.SaveChangesAsync(); departmentId = department.Id;
        }
        foreach (var page in new[] { "AdminAppointments", "EmployeeManagement" })
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/{page}?branchId={data.Fixture.Department.BranchId}&departmentId={departmentId}")).StatusCode);
    }

    [Fact]
    public async Task BackfillIsRepeatableAndPreservesManualTranslationsAndOriginalNames()
    {
        await using var data = await BookingTests.Database.Create();
        await using var db = data.Context();
        var department = await db.Departments.SingleAsync(d => d.Id == data.Fixture.Department.Id);
        department.Rename("Dermatoloji"); department.Translate(null, "Manual English name");
        await db.SaveChangesAsync();
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MediClinic.sln"))) directory = directory.Parent;
        var sql = await File.ReadAllTextAsync(Path.Combine(directory!.FullName, "scripts", "backfill-medical-names.sql"));
        await db.Database.ExecuteSqlRawAsync(sql); await db.Database.ExecuteSqlRawAsync(sql);
        db.ChangeTracker.Clear();
        department = await db.Departments.SingleAsync(d => d.Id == data.Fixture.Department.Id);
        Assert.Equal("Dermatoloji", department.Name);
        Assert.Equal("Dermatoloji", department.NameTr);
        Assert.Equal("Manual English name", department.NameEn);
    }

    [Theory]
    [InlineData("tr-TR", "Dermatoloji", "Dermatoloji Muayenesi")]
    [InlineData("en-GB", "Dermatology", "Dermatology Consultation")]
    public async Task BookingOptionsRespectCultureCookie(string culture, string departmentName, string serviceName)
    {
        await using var data = await BookingTests.Database.Create();
        await using (var db = data.Context())
        {
            (await db.Departments.SingleAsync(d => d.Id == data.Fixture.Department.Id)).Rename("Dermatoloji");
            (await db.MedicalServices.SingleAsync(s => s.Id == data.Fixture.Service.Id)).Update("Dermatoloji Muayenesi", 30, 1234.5m);
            await db.SaveChangesAsync();
        }
        using var site = new BookingMvcTests.Site(data);
        using var patient = await site.Login(data.PatientId);
        patient.DefaultRequestHeaders.Add("Cookie", $".AspNetCore.Culture=c%3D{culture}%7Cuic%3D{culture}");
        var request = data.Request();
        var page = $"/Appointments/Book?ClinicId={request.ClinicId}&BranchId={request.BranchId}&DepartmentId={request.DepartmentId}&DoctorId={request.DoctorId}&Stage=1";
        var html = WebUtility.HtmlDecode(await patient.GetStringAsync(page));
        Assert.Contains(departmentName, html); Assert.Contains(serviceName, html);
        Assert.Contains(culture == "tr-TR" ? "1.234,50 TL" : "1,234.50 TRY", html);
    }

    [Theory]
    [InlineData("İPEK ŞEN", "ipek şen", true)]
    [InlineData("IŞIK ÇAĞRI", "ışık çağrı", true)]
    [InlineData("ÖZGÜR ÜNAL", "özgür ünal", true)]
    [InlineData("Deniz Demo", "  DENİZ  ", true)]
    [InlineData("Deniz Demo", "başka", false)]
    public void TurkishNameSearch(string name, string search, bool expected) =>
        Assert.Equal(expected, AdminFilters.MatchesName(name, search));

    [Theory]
    [InlineData("Dermatoloji", "en-GB", "Dermatology")]
    [InlineData("Dermatology", "tr-TR", "Dermatoloji")]
    [InlineData("Dermatoloji Muayenesi", "en-GB", "Dermatology Consultation")]
    [InlineData("Çocuk Sağlığı ve Hastalıkları", "en-GB", "Pediatrics")]
    [InlineData("Özel hizmet", "en-GB", "Özel hizmet")]
    public void VocabularySelectsCultureAndPreservesUnknownNames(string value, string culture, string expected) =>
        Assert.Equal(expected, MedicalNames.Display(value, culture));

    [Fact]
    public async Task StoredTranslationsSurviveReloadAndBookingApproval()
    {
        await using var data = await BookingTests.Database.Create();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            await using (var db = data.Context())
            {
                var department = await db.Departments.SingleAsync(d => d.Id == data.Fixture.Department.Id);
                var service = await db.MedicalServices.SingleAsync(s => s.Id == data.Fixture.Service.Id);
                department.Translate("Özel bölüm", "Custom department");
                service.Translate("Özel muayene", "Custom consultation");
                await db.SaveChangesAsync();
            }
            await using var verify = data.Context();
            var booking = data.Service(verify);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-GB");
            var option = (await booking.Options(data.PatientId)).Single(x => x.ServiceId == data.Fixture.Service.Id);
            Assert.Equal("Custom department", option.Department);
            Assert.Equal("Custom consultation", option.Service);
            var id = await booking.Book(data.PatientId, data.Request());
            await booking.Act(data.StaffId, id, "Confirm", null);
            var appointment = await booking.Find(data.PatientId, id, false);
            Assert.Equal(AppointmentStatus.Confirmed, appointment!.Status);
            Assert.Equal("Custom consultation", appointment.Service);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal("Özel muayene", (await booking.Find(data.PatientId, id, false))!.Service);
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Fact]
    public async Task CombinedAppointmentFiltersRemainScopedAndKeepState()
    {
        await using var data = await BookingTests.Database.Create();
        using var site = new BookingMvcTests.Site(data);
        using var admin = await EmployeeManagementTests.Admin(site, data);
        await using (var db = data.Context()) await data.Service(db).Book(data.PatientId, data.Request());
        var query = $"branchId={data.Fixture.Department.BranchId}&departmentId={data.Fixture.Department.Id}&status=Pending&from=2030-01-02&to=2030-01-02";
        var html = WebUtility.HtmlDecode(await admin.GetStringAsync("/AdminAppointments?" + query));
        Assert.Contains(data.Fixture.Patient.FullName, html);
        Assert.Contains("branchId=" + data.Fixture.Department.BranchId, html);
        Assert.Contains("departmentId=" + data.Fixture.Department.Id, html);
        Assert.DoesNotContain(data.Fixture.Patient.FullName, WebUtility.HtmlDecode(await admin.GetStringAsync("/AdminAppointments?" + query.Replace("status=Pending", "status=Confirmed"))));
        Assert.DoesNotContain(data.Fixture.Patient.FullName, WebUtility.HtmlDecode(await admin.GetStringAsync("/AdminAppointments?" + query.Replace("from=2030-01-02&to=2030-01-02", "from=2030-01-03&to=2030-01-03"))));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/AdminAppointments?branchId={data.Other.Department.BranchId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/AdminAppointments?departmentId={data.Other.Department.Id}")).StatusCode);
    }

    [Fact]
    public async Task EmployeeCombinedFiltersAndUnauthorizedIdentifiers()
    {
        await using var data = await BookingTests.Database.Create();
        using var site = new BookingMvcTests.Site(data);
        using var admin = await EmployeeManagementTests.Admin(site, data);
        await using (var db = data.Context())
        {
            var doctor = await db.Doctors.SingleAsync(d => d.Id == data.Fixture.Doctor.Id);
            doctor.Rename("İpek", "Işık"); await db.SaveChangesAsync();
        }
        var query = $"branchId={data.Fixture.Department.BranchId}&departmentId={data.Fixture.Department.Id}&role=Doctor&search={Uri.EscapeDataString("ipek ışık")}";
        Assert.Contains("İpek Işık", WebUtility.HtmlDecode(await admin.GetStringAsync("/EmployeeManagement?" + query)));
        Assert.DoesNotContain("İpek Işık", WebUtility.HtmlDecode(await admin.GetStringAsync("/EmployeeManagement?" + query.Replace("role=Doctor", "role=Staff"))));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/EmployeeManagement?branchId={data.Other.Department.BranchId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/EmployeeManagement?departmentId={data.Other.Department.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/EmployeeManagement?role=Administrator")).StatusCode);
    }

    [Theory]
    [InlineData("/Dashboard/ClinicAdmin", "/Dashboard/ClinicAdmin")]
    [InlineData("/AdminAppointments", "/AdminAppointments")]
    [InlineData("/EmployeeManagement", "/EmployeeManagement")]
    [InlineData("/EmployeeManagement/Edit", "/EmployeeManagement")]
    [InlineData("/ClinicManagement", "/ClinicManagement")]
    [InlineData("/ClinicManagement/Edit?kind=Clinic", "/ClinicManagement")]
    public async Task ExactlyOneMainNavigationItemIsCurrent(string page, string parent)
    {
        await using var data = await BookingTests.Database.Create();
        using var site = new BookingMvcTests.Site(data);
        using var admin = await EmployeeManagementTests.Admin(site, data);
        var html = await admin.GetStringAsync(page);
        var navigation = Regex.Match(html, "<nav aria-label=\"Ana gezinme\">(.*?)</nav>", RegexOptions.Singleline).Value;
        var active = Regex.Matches(navigation, "<a[^>]*aria-current=\"page\"[^>]*>");
        Assert.Single(active);
        Assert.Contains("href=\"" + parent + "\"", active[0].Value);
    }
}
