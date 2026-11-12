using System.Net;
using System.Text.RegularExpressions;
using MediClinic.Application;
using MediClinic.Domain;
using MediClinic.Infrastructure.Identity;
using MediClinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediClinic.Tests;

public sealed class BookingMvcTests
{
    internal sealed class Site(BookingTests.Database data) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:MediClinic", data.ConnectionString)
            .ConfigureServices(services => services.AddSingleton<TimeProvider>(data.Time));
        public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        public async Task<HttpClient> Login(string id)
        {
            using var scope = Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(id))!;
            var password = "Test-" + Guid.NewGuid().ToString("N") + "aA1!";
            Assert.True((await users.AddPasswordAsync(user, password)).Succeeded);
            var client = Client();
            var result = await Post(client, "/Account/Login", "/Account/Login", new() { ["Email"] = user.Email!, ["Password"] = password });
            Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
            return client;
        }
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client, string page, string url, Dictionary<string, string> values)
    {
        var html = await client.GetStringAsync(page);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        values["__RequestVerificationToken"] = WebUtility.HtmlDecode(token);
        return await client.PostAsync(url, new FormUrlEncodedContent(values));
    }
    private static Dictionary<string, string> Form(BookingTests.Database data)
    {
        var request = data.Request();
        return new() { ["ClinicId"] = request.ClinicId.ToString(), ["BranchId"] = request.BranchId.ToString(),
            ["DepartmentId"] = request.DepartmentId.ToString(), ["DoctorId"] = request.DoctorId.ToString(), ["ServiceId"] = request.ServiceId.ToString(),
            ["Date"] = "2030-01-02", ["LocalStart"] = request.LocalStart.ToString("yyyy-MM-ddTHH:mm:ss") };
    }

    [Fact]
    public async Task PatientCanBrowseBookViewAndCancelThroughMvc()
    {
        await using var data = await BookingTests.Database.Create(); using var site = new Site(data); using var client = await site.Login(data.PatientId);
        var request = data.Request();
        var page = await client.GetStringAsync($"/Appointments/Book?ClinicId={request.ClinicId}&BranchId={request.BranchId}&DepartmentId={request.DepartmentId}&DoctorId={request.DoctorId}&ServiceId={request.ServiceId}&Date=2030-01-02&Stage=2");
        Assert.Contains(data.Fixture.Clinic.Name, page);
        Assert.Contains("Europe/Istanbul", page);
        Assert.Contains("2030-01-02T12:00:00", page);
        var form = Form(data);
        var response = await Post(client, "/Appointments/Book", "/Appointments/Book", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var detailsUrl = response.Headers.Location!.OriginalString;
        var details = await client.GetStringAsync(detailsUrl);
        Assert.Contains("Pending", details); Assert.Contains(data.Fixture.Service.Name, details);
        await using var db = data.Context(); var appointment = await db.Appointments.SingleAsync();
        Assert.Equal(data.Fixture.Patient.Id, db.Entry(appointment).Property<Guid>("PatientKey").CurrentValue);
        var cancel = await Post(client, detailsUrl, "/Appointments/Cancel", new() { ["Id"] = appointment.Id.ToString(), ["Reason"] = "Changed plans" });
        Assert.Equal(HttpStatusCode.Redirect, cancel.StatusCode);
        var cancelled = WebUtility.HtmlDecode(await client.GetStringAsync(detailsUrl));
        Assert.Contains("Cancelled", cancelled);
        Assert.Contains("Randevu iptal edildi.", cancelled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(2001)]
    public async Task CancellationRequiresReasonAndShowsTurkishFeedback(int length)
    {
        await using var data = await BookingTests.Database.Create();
        Guid id;
        await using (var db = data.Context()) id = await data.Service(db).Book(data.PatientId, data.Request());
        using var site = new Site(data); using var client = await site.Login(data.PatientId);
        var url = $"/Appointments/Details/{id}";
        var reason = new string(length > 2000 ? 'x' : ' ', length);
        var response = await Post(client, url, "/Appointments/Cancel", new() { ["Id"] = id.ToString(), ["Reason"] = reason });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("İptal gerekçesi zorunludur ve en fazla 2000 karakter olabilir.", WebUtility.HtmlDecode(await client.GetStringAsync(url)));
        await using var verify = data.Context();
        Assert.Equal(AppointmentStatus.Pending, (await verify.Appointments.SingleAsync()).Status);
    }

    [Fact]
    public async Task RepeatedCancellationShowsTurkishFailureAndPreservesOriginalReason()
    {
        await using var data = await BookingTests.Database.Create();
        Guid id;
        await using (var db = data.Context())
        {
            var service = data.Service(db);
            id = await service.Book(data.PatientId, data.Request());
            await service.Act(data.PatientId, id, "Cancel", "İlk gerekçe");
        }
        using var site = new Site(data); using var client = await site.Login(data.PatientId);
        var url = $"/Appointments/Details/{id}";
        await Post(client, url, "/Appointments/Cancel", new() { ["Id"] = id.ToString(), ["Reason"] = "İkinci gerekçe" });
        Assert.Contains("Randevu iptal edilemedi. Güncel randevu durumunu kontrol edin.", WebUtility.HtmlDecode(await client.GetStringAsync(url)));
        await using var verify = data.Context();
        var item = await verify.Appointments.SingleAsync();
        Assert.Equal(AppointmentStatus.Cancelled, item.Status);
        Assert.Equal("İlk gerekçe", item.DecisionReason);
    }

    [Fact]
    public async Task MvcEnforcesPatientIsolationStaffScopeAndAntiforgery()
    {
        await using var data = await BookingTests.Database.Create();
        Guid id;
        await using (var db = data.Context()) id = await data.Service(db).Book(data.PatientId, data.Request());
        using var site = new Site(data);
        using var anonymous = site.Client();
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/Appointments")).StatusCode);
        using var other = await site.Login(data.OtherPatientId);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/Appointments/Details/{id}")).StatusCode);
        Assert.DoesNotContain(id.ToString(), await other.GetStringAsync("/Appointments"));
        Assert.Equal(HttpStatusCode.Redirect, (await other.GetAsync("/StaffAppointments")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsync("/Appointments/Cancel", new FormUrlEncodedContent(new Dictionary<string, string> { ["Id"] = id.ToString(), ["Reason"] = "No" }))).StatusCode);
        await Post(other, "/Appointments/Book", "/Appointments/Cancel", new() { ["Id"] = id.ToString(), ["Reason"] = "No" });
        using var staff = await site.Login(data.OtherStaffId);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/StaffAppointments/Details/{id}")).StatusCode);
        Assert.DoesNotContain(id.ToString(), await staff.GetStringAsync("/StaffAppointments"));
        await Post(staff, "/Account/Login", "/StaffAppointments/Review", new() { ["Id"] = id.ToString(), ["Action"] = "Confirm" });
        await using var verify = data.Context(); Assert.Equal(AppointmentStatus.Pending, (await verify.Appointments.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("PatientId")][InlineData("Role")][InlineData("Status")]
    public async Task BookingRejectsOwnershipAndPrivilegeInjection(string field)
    {
        await using var data = await BookingTests.Database.Create(); using var site = new Site(data); using var client = await site.Login(data.PatientId);
        var form = Form(data); form[field] = data.OtherPatientId;
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/Appointments/Book", "/Appointments/Book", form)).StatusCode);
        await using var verify = data.Context(); Assert.Empty(await verify.Appointments.ToListAsync());
    }

    [Fact]
    public async Task CascadingSelectionsAndReviewWorkWithoutJavascript()
    {
        await using var data = await BookingTests.Database.Create();
        using var site = new Site(data); using var client = await site.Login(data.PatientId);
        var values = Form(data); values.Remove("LocalStart"); values["Stage"] = "1";
        var path = "/Appointments/Book";
        var initial = WebUtility.HtmlDecode(await client.GetStringAsync(path));
        Assert.DoesNotContain("<script>", initial);
        Assert.Contains("name=\"ClinicId\"", initial);
        Assert.Contains("data-booking-selection", initial);
        Assert.Contains("/js/booking.js", initial);
        Assert.Matches("<button[^>]*data-booking-update[^>]*>Seçenekleri güncelle</button>", initial);
        Assert.DoesNotMatch("<button[^>]*hidden[^>]*data-booking-update", initial);
        Assert.Matches("name=\"BranchId\"[^>]*disabled", initial);
        var accumulated = new Dictionary<string, string> { ["Stage"] = "1" };
        foreach (var key in new[] { "ClinicId", "BranchId", "DepartmentId", "DoctorId", "ServiceId" })
        {
            accumulated[key] = values[key];
            var html = WebUtility.HtmlDecode(await client.GetStringAsync(path + "?" + await new FormUrlEncodedContent(accumulated).ReadAsStringAsync()));
            Assert.Contains($"value=\"{values[key]}\" selected", html);
            Assert.DoesNotContain($"value=\"{data.Other.Doctor.Id}\"", html);
        }
        values["Stage"] = "2";
        var slots = WebUtility.HtmlDecode(await client.GetStringAsync(path + "?" + await new FormUrlEncodedContent(values).ReadAsStringAsync()));
        Assert.Contains("type=\"radio\"", slots); Assert.Contains("02 Ocak 2030", slots);
        Assert.DoesNotContain("<select", slots);
        values["Stage"] = "3"; values["LocalStart"] = "2030-01-02T12:00:00";
        var review = WebUtility.HtmlDecode(await client.GetStringAsync(path + "?" + await new FormUrlEncodedContent(values).ReadAsStringAsync()));
        foreach (var label in new[] { "Klinik", "Şube", "Bölüm", "Doktor", "Hizmet", "Tarih", "Saat", "Süre", "Beklemede", "12:00", "30 dakika", "Randevu talebini gönder" })
            Assert.Contains(label, review);
        await using var db = data.Context(); Assert.Empty(await db.Appointments.ToListAsync());
        var result = await Post(client, path, path, values);
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.Equal(AppointmentStatus.Pending, (await db.Appointments.SingleAsync()).Status);
    }

    [Fact]
    public async Task StageOneGetPreservesValidAncestorsAndRejectsInvalidDescendants()
    {
        await using var data = await BookingTests.Database.Create();
        using var site = new Site(data); using var client = await site.Login(data.PatientId);
        var values = Form(data); values["Stage"] = "1"; values.Remove("LocalStart");
        values["DepartmentId"] = data.Other.Department.Id.ToString();
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Appointments/Book?" + await new FormUrlEncodedContent(values).ReadAsStringAsync()));
        Assert.Contains($"value=\"{values["ClinicId"]}\" selected", html);
        Assert.Contains($"value=\"{values["BranchId"]}\" selected", html);
        Assert.DoesNotContain($"value=\"{values["DepartmentId"]}\" selected", html);
        Assert.Matches("name=\"DoctorId\"[^>]*disabled", html);
        Assert.Matches("name=\"ServiceId\"[^>]*disabled", html);
        Assert.Contains("seçim geçersiz", html);
        await using var db = data.Context(); Assert.Empty(await db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task StaleSlotAndTamperedHierarchyCannotCreateAppointment()
    {
        await using var data = await BookingTests.Database.Create();
        using var site = new Site(data); using var client = await site.Login(data.PatientId);
        var values = Form(data); values["Stage"] = "3";
        var url = "/Appointments/Book?" + await new FormUrlEncodedContent(values).ReadAsStringAsync();
        Assert.Contains("Randevu talebini", WebUtility.HtmlDecode(await client.GetStringAsync(url)));
        await using (var db = data.Context()) await data.Service(db).Book(data.OtherPatientId, data.Request());
        var stale = await Post(client, "/Appointments/Book", "/Appointments/Book", values);
        Assert.Contains("artık uygun değil", WebUtility.HtmlDecode(await stale.Content.ReadAsStringAsync()));
        values["ClinicId"] = data.Other.Clinic.Id.ToString();
        var tampered = await Post(client, "/Appointments/Book", "/Appointments/Book", values);
        Assert.Contains("seçim geçersiz", WebUtility.HtmlDecode(await tampered.Content.ReadAsStringAsync()));
        await using var verify = data.Context(); Assert.Single(await verify.Appointments.ToListAsync());
    }

    [Fact]
    public async Task EmptyAvailabilityMissingSlotAndMalformedDateShowTurkishFeedback()
    {
        await using var data = await BookingTests.Database.Create();
        using var site = new Site(data); using var client = await site.Login(data.PatientId);
        var values = Form(data); values["Stage"] = "2"; values["Date"] = "2030-01-03";
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Appointments/Book?" + await new FormUrlEncodedContent(values).ReadAsStringAsync()));
        Assert.Contains("Bu tarihte uygun saat bulunmuyor", html);
        values["Date"] = "2030-01-02"; values.Remove("LocalStart"); values["Stage"] = "3";
        html = WebUtility.HtmlDecode(await client.GetStringAsync("/Appointments/Book?" + await new FormUrlEncodedContent(values).ReadAsStringAsync()));
        Assert.Contains("birini seçin", html);
        values["Date"] = "invalid";
        var invalid = await Post(client, "/Appointments/Book", "/Appointments/Book", values);
        Assert.Contains("tarih bilgisini kontrol edin", WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync()));
        await using var db = data.Context(); Assert.Empty(await db.Appointments.ToListAsync());
        await db.Database.ExecuteSqlRawAsync("DELETE FROM MedicalServices");
        html = WebUtility.HtmlDecode(await client.GetStringAsync("/Appointments/Book"));
        Assert.Contains("randevu alınabilecek hizmet bulunmuyor", html);
    }

    [Fact]
    public async Task StaffCanConfirmAndRejectWithReasonButCannotCompleteEarly()
    {
        await using var data = await BookingTests.Database.Create();
        Guid first, second;
        await using (var db = data.Context())
        {
            first = await data.Service(db).Book(data.PatientId, data.Request());
            second = await data.Service(db).Book(data.PatientId, data.Request(data.Fixture.Start.AddMinutes(30)));
        }
        using var site = new Site(data); using var client = await site.Login(data.StaffId);
        var firstUrl = $"/StaffAppointments/Details/{first}"; var secondUrl = $"/StaffAppointments/Details/{second}";
        await Post(client, firstUrl, "/StaffAppointments/Review", new() { ["Id"] = first.ToString(), ["Action"] = "Confirm" });
        await Post(client, firstUrl, "/StaffAppointments/Review", new() { ["Id"] = first.ToString(), ["Action"] = "Complete" });
        Assert.Contains("Confirmed", await client.GetStringAsync(firstUrl));
        await Post(client, secondUrl, "/StaffAppointments/Review", new() { ["Id"] = second.ToString(), ["Action"] = "Reject", ["Reason"] = " " });
        Assert.Contains("Pending", await client.GetStringAsync(secondUrl));
        await Post(client, secondUrl, "/StaffAppointments/Review", new() { ["Id"] = second.ToString(), ["Action"] = "Reject", ["Reason"] = "Unavailable" });
        Assert.Contains("Rejected", await client.GetStringAsync(secondUrl));
        Assert.Contains("Unavailable", await client.GetStringAsync(secondUrl));
    }
}
