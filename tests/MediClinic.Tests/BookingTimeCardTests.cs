using System.Net;
using System.Text.RegularExpressions;
using MediClinic.Application;
using MediClinic.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace MediClinic.Tests;

public sealed class BookingTimeCardTests
{
    [Fact]
    public void TodayAddsOnlyElapsedMarkersAndAuthoritativeFutureSlots()
    {
        var now = new DateTimeOffset(2030, 1, 2, 12, 0, 0, TimeSpan.FromHours(3));
        var model = new BookingModel { Date = new(2030, 1, 2), Slots = [new(2030, 1, 2, 12, 15, 0)] };
        model.PrepareTimeCards(now);
        Assert.Contains(model.TimeCards, x => x.LocalStart.Hour == 11 && x.IsPast);
        Assert.Contains(model.TimeCards, x => x.LocalStart == Scheduling.Local(now) && x.IsPast);
        Assert.Single(model.TimeCards, x => !x.IsPast);
        Assert.DoesNotContain(model.TimeCards, x => x.LocalStart.Hour == 13);
        Assert.Equal(model.TimeCards.Count, model.TimeCards.Select(x => x.LocalStart).Distinct().Count());
    }

    [Fact]
    public void IstanbulMidnightUsesTheInstantRatherThanUtcDateOrOffsetClockTime()
    {
        var now = new DateTimeOffset(2030, 1, 1, 21, 0, 0, TimeSpan.Zero);
        var model = new BookingModel { Date = new(2030, 1, 2) };
        model.PrepareTimeCards(now.ToOffset(TimeSpan.FromHours(-5)));
        var midnight = Assert.Single(model.TimeCards);
        Assert.Equal(new DateTime(2030, 1, 2, 0, 0, 0), midnight.LocalStart);
        Assert.True(midnight.IsPast);
        Assert.Equal(now, Scheduling.ToUtc(midnight.LocalStart));
        model.PrepareTimeCards(now.AddTicks(-1));
        Assert.Empty(model.TimeCards); // Tomorrow in Istanbul, despite the same UTC date.
    }

    [Fact]
    public void FutureDateContainsOnlyGenuinelyAvailableInteractiveSlots()
    {
        var local = new DateTime(2030, 1, 2, 12, 0, 0);
        var model = new BookingModel { Date = DateOnly.FromDateTime(local), Slots = [local] };
        model.PrepareTimeCards(new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero));
        var card = Assert.Single(model.TimeCards);
        Assert.False(card.IsPast);
        Assert.Equal(local, card.LocalStart);
    }

    private static string Page(BookingTests.Database data, string date = "2030-01-02")
    {
        var request = data.Request();
        return $"/Appointments/Book?Stage=2&Date={date}&ClinicId={request.ClinicId}&BranchId={request.BranchId}&DepartmentId={request.DepartmentId}&DoctorId={request.DoctorId}&ServiceId={request.ServiceId}";
    }

    [Fact]
    public async Task TodayRendersDisabledPastAndExactNowCardsButKeepsFutureSlotsInteractive()
    {
        await using var data = await BookingTests.Database.Create();
        data.Time.Now = data.Fixture.Start.AddMinutes(15);
        using var site = new BookingMvcTests.Site(data); using var client = await site.Login(data.PatientId);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(Page(data)));
        foreach (var time in new[] { "12:00", "12:15" })
        {
            var card = Regex.Match(html, $"<label class=\"booking-slot booking-slot-past\">.*?value=\"2030-01-02T{time}:00\".*?</label>").Value;
            Assert.NotEmpty(card);
            Assert.Contains("disabled=\"disabled\"", card);
            Assert.Contains("Geçmiş saat", card);
            Assert.DoesNotContain("checked=", card);
        }
        var future = Regex.Match(html, "<label class=\"booking-slot\\s*\"><input[^>]*value=\"2030-01-02T12:30:00\"[^>]*>").Value;
        Assert.NotEmpty(future); Assert.DoesNotContain("disabled", future);
        await using var db = data.Context();
        var service = data.Service(db);
        var slots = await service.Slots(data.PatientId, data.Fixture.Doctor.Id, data.Fixture.Service.Id, new(2030, 1, 2));
        Assert.DoesNotContain(Scheduling.Local(data.Time.Now), slots);
        Assert.Contains(Scheduling.Local(data.Time.Now.AddMinutes(15)), slots);
        await Assert.ThrowsAsync<BookingException>(() => service.Book(data.PatientId, data.Request(data.Time.Now)));
        await Assert.ThrowsAsync<BookingException>(() => service.Book(data.PatientId, data.Request(data.Time.Now.AddMinutes(-15))));
        Assert.Empty(await db.Appointments.ToListAsync());
        await service.Book(data.OtherPatientId, data.Request(data.Time.Now.AddMinutes(15)));
        var occupied = await client.GetStringAsync(Page(data));
        Assert.DoesNotContain("value=\"2030-01-02T12:30:00\"", occupied);
        Assert.DoesNotContain("value=\"2030-01-02T12:45:00\"", occupied);
        Assert.Contains("value=\"2030-01-02T13:00:00\"", occupied);
    }

    [Fact]
    public async Task PastDateHasClearTurkishValidationAndNoSelectableGrid()
    {
        await using var data = await BookingTests.Database.Create();
        data.Time.Now = data.Fixture.Start;
        using var site = new BookingMvcTests.Site(data); using var client = await site.Login(data.PatientId);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(Page(data, "2030-01-01")));
        Assert.Contains("Geçmiş bir tarihe randevu alınamaz", html);
        Assert.DoesNotContain("type=\"radio\"", html);
        await using var db = data.Context();
        await Assert.ThrowsAsync<BookingException>(() => data.Service(db).Slots(data.PatientId,
            data.Fixture.Doctor.Id, data.Fixture.Service.Id, new(2030, 1, 1)));
        await Assert.ThrowsAsync<BookingException>(() => data.Service(db).Book(data.PatientId, data.Request(data.Fixture.Start.AddDays(-1))));
        Assert.Empty(await db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task DisplayedSlotBecomingExactNowIsRejectedOnPostIndependentlyOfDisabledMarkup()
    {
        await using var data = await BookingTests.Database.Create();
        data.Time.Now = data.Fixture.Start.AddMinutes(-1);
        using var site = new BookingMvcTests.Site(data); using var client = await site.Login(data.PatientId);
        var html = await client.GetStringAsync(Page(data).Replace("Stage=2", "Stage=3") + "&LocalStart=2030-01-02T12:00:00");
        Assert.Contains("Randevu talebini", WebUtility.HtmlDecode(html));
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        var request = data.Request();
        data.Time.Now = data.Fixture.Start;
        var response = await client.PostAsync("/Appointments/Book", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ClinicId"] = request.ClinicId.ToString(), ["BranchId"] = request.BranchId.ToString(),
            ["DepartmentId"] = request.DepartmentId.ToString(), ["DoctorId"] = request.DoctorId.ToString(),
            ["ServiceId"] = request.ServiceId.ToString(), ["Date"] = "2030-01-02", ["LocalStart"] = "2030-01-02T12:00:00",
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token)
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Seçtiğiniz saat artık uygun değil", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        await using var db = data.Context(); Assert.Empty(await db.Appointments.ToListAsync());
    }
}
