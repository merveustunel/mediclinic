using System.Net;
using MediClinic.Domain;
using Microsoft.EntityFrameworkCore;

namespace MediClinic.Tests;

public sealed class PatientAppointmentsTests
{
    [Theory]
    [InlineData(null)][InlineData("")][InlineData("Pending")][InlineData("Confirmed")]
    [InlineData("Completed")][InlineData("Cancelled")][InlineData("Rejected")]
    public async Task FiltersRenderOnlyOwnedMatchesAndPreserveDescendingOrder(string? filter)
    {
        await using var data = await BookingTests.Database.Create();
        var ids = new Dictionary<AppointmentStatus, Guid>();
        Guid otherId;
        await using (var db = data.Context())
        {
            var service = data.Service(db);
            var index = 0;
            foreach (var status in Enum.GetValues<AppointmentStatus>())
            {
                var id = await service.Book(data.PatientId, data.Request(data.Fixture.Start.AddMinutes(30 * index++)));
                ids[status] = id;
                var appointment = await db.Appointments.SingleAsync(x => x.Id == id);
                switch (status)
                {
                    case AppointmentStatus.Confirmed: appointment.Confirm(); break;
                    case AppointmentStatus.Completed: appointment.Confirm(); appointment.Complete(appointment.EndsAt); break;
                    case AppointmentStatus.Cancelled: appointment.Cancel("Private reason"); break;
                    case AppointmentStatus.Rejected: appointment.Reject("Private reason"); break;
                }
                await db.SaveChangesAsync();
            }
            otherId = await service.Book(data.OtherPatientId, data.Request(data.Fixture.Start.AddMinutes(150)));
        }
        using var site = new BookingMvcTests.Site(data);
        using var owner = await site.Login(data.PatientId);
        var html = WebUtility.HtmlDecode(await owner.GetStringAsync("/Appointments?status=" + filter + "&patientId=" + data.OtherPatientId));
        foreach (var (status, id) in ids)
        {
            var link = $"/Appointments/Details/{id}";
            if (string.IsNullOrEmpty(filter) || filter == status.ToString()) Assert.Contains(link, html);
            else Assert.DoesNotContain(link, html);
        }
        Assert.DoesNotContain(otherId.ToString(), html);
        Assert.DoesNotContain("Private reason", html);
        Assert.Contains("Yeni randevu oluştur", html);
        Assert.Contains("Detayları görüntüle", html);
        Assert.Contains(data.Fixture.Doctor.FullName, html);
        Assert.Contains(data.Fixture.Service.Name, html);
        Assert.Contains(data.Fixture.Department.Branch.Name, html);
        Assert.Contains("<time datetime=", html);
        if (string.IsNullOrEmpty(filter) || filter == "Pending") Assert.Contains("02 Ocak 2030, 12:00", html);
        if (string.IsNullOrEmpty(filter))
        {
            var positions = Enum.GetValues<AppointmentStatus>().Reverse().Select(x => html.IndexOf(ids[x].ToString(), StringComparison.Ordinal)).ToArray();
            Assert.Equal(positions.Order(), positions);
        }
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/Appointments/Details/{ids[AppointmentStatus.Pending]}")).StatusCode);
        using var other = await site.Login(data.OtherPatientId);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/Appointments/Details/{ids[AppointmentStatus.Pending]}")).StatusCode);
        var otherHtml = await other.GetStringAsync("/Appointments?status=" + filter);
        foreach (var id in ids.Values) Assert.DoesNotContain(id.ToString(), otherHtml);
    }

    [Theory]
    [InlineData("0")][InlineData("99")][InlineData("pending")][InlineData("Unknown")]
    [InlineData("Pending,Confirmed")][InlineData("%3Cscript%3E")]
    public async Task InvalidFiltersReturnBadRequest(string filter)
    {
        await using var data = await BookingTests.Database.Create();
        using var site = new BookingMvcTests.Site(data);
        using var owner = await site.Login(data.PatientId);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync("/Appointments?status=" + filter)).StatusCode);
    }

    [Fact]
    public async Task EmptyStatesAndAccessPoliciesRemainDistinct()
    {
        await using var data = await BookingTests.Database.Create();
        using var site = new BookingMvcTests.Site(data);
        using var owner = await site.Login(data.PatientId);
        Assert.Contains("Henüz randevunuz bulunmuyor", WebUtility.HtmlDecode(await owner.GetStringAsync("/Appointments?status=Confirmed")));
        await using (var db = data.Context()) await data.Service(db).Book(data.PatientId, data.Request());
        var filtered = WebUtility.HtmlDecode(await owner.GetStringAsync("/Appointments?status=Confirmed"));
        Assert.Contains("Bu durumda randevunuz bulunmuyor", filtered);
        Assert.Contains("Tüm randevuları görüntüle", filtered);
        Assert.DoesNotContain("Henüz randevunuz bulunmuyor", filtered);
        using var anonymous = site.Client();
        Assert.Contains("/Account/Login", (await anonymous.GetAsync("/Appointments?status=Pending")).Headers.Location!.OriginalString);
        using var staff = await site.Login(data.StaffId);
        Assert.Contains("/Account/AccessDenied", (await staff.GetAsync("/Appointments")).Headers.Location!.OriginalString);
        await using (var db = data.Context())
        {
            var user = await db.Users.FindAsync(data.PatientId);
            db.Entry(user!).Property(x => x.PatientId).CurrentValue = null;
            await db.SaveChangesAsync();
        }
        Assert.Contains("/Account/AccessDenied", (await owner.GetAsync("/Appointments")).Headers.Location!.OriginalString);
    }
}
