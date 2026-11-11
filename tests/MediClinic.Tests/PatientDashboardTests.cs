using System.Net;
using MediClinic.Application;
using MediClinic.Domain;
using MediClinic.Web.Models;

namespace MediClinic.Tests;

public sealed class PatientDashboardTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 2, 9, 0, 0, TimeSpan.Zero);
    private static AppointmentSummary Item(AppointmentStatus status, int minutes) =>
        new(Guid.NewGuid(), "Hizmet", "Doktor", "Şube", Now.AddMinutes(minutes), Now.AddMinutes(minutes + 30), status, null);

    [Fact]
    public void CountsIncludeHistoryButUpcomingExcludesPastBoundaryAndTerminalStatuses()
    {
        var pending = Item(AppointmentStatus.Pending, 30);
        var model = PatientDashboardModel.Create(new[] {
            Item(AppointmentStatus.Confirmed, 60), pending,
            Item(AppointmentStatus.Pending, -60), Item(AppointmentStatus.Pending, 0),
            Item(AppointmentStatus.Confirmed, -30), Item(AppointmentStatus.Confirmed, 0),
            Item(AppointmentStatus.Completed, -120), Item(AppointmentStatus.Cancelled, 10),
            Item(AppointmentStatus.Rejected, 20), Item(AppointmentStatus.Completed, 5)
        }, Now);
        Assert.Equal(10, model.TotalAppointments);
        Assert.Equal(3, model.PendingAppointments);
        Assert.Equal(1, model.ConfirmedUpcomingAppointments);
        Assert.Equal(2, model.CompletedAppointments);
        Assert.Equal(pending, model.NextAppointment);
    }

    [Fact]
    public void NextSelectionUsesInstantsAndStableIdTieBreak()
    {
        var first = Item(AppointmentStatus.Confirmed, 1) with { Id = Guid.Empty,
            StartsAt = Now.AddMinutes(1).ToOffset(TimeSpan.FromHours(3)) };
        var second = Item(AppointmentStatus.Pending, 1);
        Assert.Equal(first, PatientDashboardModel.Create(new[] { second, first }, Now).NextAppointment);
        Assert.Equal(first, PatientDashboardModel.Create(new[] { first, second }, Now).NextAppointment);
    }

    [Fact]
    public void EmptyAndHistoryOnlyHaveNoNextAppointment()
    {
        Assert.Equal(new PatientDashboardModel(0, 0, 0, 0, null), PatientDashboardModel.Create([], Now));
        Assert.Null(PatientDashboardModel.Create([Item(AppointmentStatus.Confirmed, 0),
            Item(AppointmentStatus.Cancelled, 20)], Now).NextAppointment);
    }

    [Fact]
    public async Task RealDashboardRendersOwnedDataLocalTimeAndDistinctEmptyStates()
    {
        await using var data = await BookingTests.Database.Create();
        Guid id;
        await using (var db = data.Context())
            id = await data.Service(db).Book(data.PatientId, data.Request());
        using var site = new BookingMvcTests.Site(data);
        using var owner = await site.Login(data.PatientId);
        var html = WebUtility.HtmlDecode(await owner.GetStringAsync("/Dashboard/Patient"));
        Assert.Contains($"/Appointments/Details/{id}", html);
        Assert.Contains(data.Fixture.Doctor.FullName, html);
        Assert.Contains(data.Fixture.Service.Name, html);
        Assert.Contains(data.Fixture.Department.Branch.Name, html);
        Assert.Contains("02 Ocak 2030, 12:00", html);
        Assert.Contains("Beklemede", html);
        Assert.Contains("<dt>Toplam randevu</dt><dd>1</dd>", html);
        Assert.Contains("<dt>Bekleyen randevu</dt><dd>1</dd>", html);
        Assert.Contains("<dt>Onaylı yaklaşan randevu</dt><dd>0</dd>", html);
        using var other = await site.Login(data.OtherPatientId);
        var empty = WebUtility.HtmlDecode(await other.GetStringAsync($"/Dashboard/Patient?patientId={data.PatientId}"));
        Assert.Contains("Henüz randevunuz bulunmuyor", empty);
        Assert.Contains("<dt>Toplam randevu</dt><dd>0</dd>", empty);
        Assert.DoesNotContain(id.ToString(), empty);
        Assert.DoesNotContain(data.Fixture.Doctor.FullName, empty);
        await using (var db = data.Context())
            await data.Service(db).Act(data.PatientId, id, "Cancel", "Özel iptal nedeni");
        var history = WebUtility.HtmlDecode(await owner.GetStringAsync("/Dashboard/Patient"));
        Assert.Contains("Yaklaşan randevunuz bulunmuyor", history);
        Assert.DoesNotContain("Özel iptal nedeni", history);
    }

    [Fact]
    public async Task DashboardRejectsAnonymousStaffAndUnlinkedPatients()
    {
        await using var data = await BookingTests.Database.Create();
        using var site = new BookingMvcTests.Site(data);
        using var anonymous = site.Client();
        Assert.Contains("/Account/Login", (await anonymous.GetAsync("/Dashboard/Patient")).Headers.Location!.OriginalString);
        using var staff = await site.Login(data.StaffId);
        Assert.Contains("/Account/AccessDenied", (await staff.GetAsync("/Dashboard/Patient")).Headers.Location!.OriginalString);
        using var patient = await site.Login(data.PatientId);
        await using (var db = data.Context())
        {
            var user = await db.Users.FindAsync(data.PatientId);
            db.Entry(user!).Property(x => x.PatientId).CurrentValue = null;
            await db.SaveChangesAsync();
        }
        Assert.Contains("/Account/AccessDenied", (await patient.GetAsync("/Dashboard/Patient")).Headers.Location!.OriginalString);
    }
}
