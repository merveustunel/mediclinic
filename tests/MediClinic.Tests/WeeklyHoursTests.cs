using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using MediClinic.Domain;
using MediClinic.Infrastructure.Identity;
using MediClinic.Web.Controllers;
using MediClinic.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediClinic.Tests;

public sealed class WeeklyHoursTests
{
    private static WeeklyHoursModel Week(Guid branch) => new() { BranchId = branch,
        Days = Enumerable.Range(0, 7).Select(d => new WorkingDayModel { Day = (DayOfWeek)d, Closed = false, Opens = new(9, 0), Closes = new(17, 0) }).ToList() };
    private static ClinicManagementController Controller(MediClinic.Infrastructure.Persistence.MediClinicDbContext db, string user) => new(db) {
        ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user)], "test")) } } };

    [Fact]
    public async Task WeeklySaveChangesPatientSlotsAndClosedDayOptions()
    {
        await using var data = await BookingTests.Database.Create();
        await using var db = data.Context();
        db.ClinicAdministrators.Add(new() { UserId = data.UnlinkedStaffId, ClinicId = data.Fixture.Clinic.Id }); await db.SaveChangesAsync();
        var controller = Controller(db, data.UnlinkedStaffId);
        var week = Week(data.Request().BranchId);
        Assert.IsType<OkObjectResult>(await controller.WeeklyHours(week));
        Assert.Equal(7, await db.WorkingHours.CountAsync());
        Assert.Contains(data.Request().LocalStart, await data.Service(db).Slots(data.PatientId, data.Fixture.Doctor.Id, data.Fixture.Service.Id, new(2030, 1, 2)));
        week.Days.Single(d => d.Day == DayOfWeek.Wednesday).Closed = true;
        Assert.IsType<OkObjectResult>(await controller.WeeklyHours(week));
        await using var fresh = data.Context();
        Assert.Empty(await data.Service(fresh).Slots(data.PatientId, data.Fixture.Doctor.Id, data.Fixture.Service.Id, new(2030, 1, 2)));
        Assert.Contains(DayOfWeek.Wednesday, (await data.Service(fresh).Options(data.PatientId)).Single(o => o.ServiceId == data.Fixture.Service.Id).ClosedDays!);
        await Assert.ThrowsAsync<MediClinic.Application.BookingException>(() => data.Service(fresh).Book(data.PatientId, data.Request()));
        using var site = new BookingMvcTests.Site(data);
        using var patient = await site.Login(data.PatientId);
        var request = data.Request();
        var page = await patient.GetStringAsync($"/Appointments/Book?ClinicId={request.ClinicId}&BranchId={request.BranchId}&DepartmentId={request.DepartmentId}&DoctorId={request.DoctorId}&ServiceId={request.ServiceId}&Date=2030-01-02&Stage=2");
        Assert.DoesNotContain("2030-01-02T12:00:00", page);
    }

    [Fact]
    public async Task InvalidWeeksAndActiveAppointmentsLeaveAllDaysUnchanged()
    {
        await using var data = await BookingTests.Database.Create(); await using var db = data.Context();
        db.ClinicAdministrators.Add(new() { UserId = data.UnlinkedStaffId, ClinicId = data.Fixture.Clinic.Id }); await db.SaveChangesAsync();
        var controller = Controller(db, data.UnlinkedStaffId); var week = Week(data.Request().BranchId);
        week.Days[6].Closes = new(8, 0);
        Assert.IsType<BadRequestObjectResult>(await controller.WeeklyHours(week)); Assert.Empty(await db.WorkingHours.ToListAsync());
        week = Week(data.Request().BranchId); week.Days[6].Day = week.Days[0].Day;
        Assert.IsType<BadRequestObjectResult>(await controller.WeeklyHours(week));
        await data.Service(db).Book(data.PatientId, data.Request());
        week = Week(data.Request().BranchId); week.Days[3].Closed = true;
        Assert.IsType<ConflictObjectResult>(await controller.WeeklyHours(week)); Assert.Empty(await db.WorkingHours.ToListAsync());
    }

    [Fact]
    public async Task DatabaseFailureRollsBackEarlierDayWrites()
    {
        await using var data = await BookingTests.Database.Create();
        await using (var db = data.Context())
        {
            db.ClinicAdministrators.Add(new() { UserId = data.UnlinkedStaffId, ClinicId = data.Fixture.Clinic.Id }); await db.SaveChangesAsync();
            Assert.IsType<OkObjectResult>(await Controller(db, data.UnlinkedStaffId).WeeklyHours(Week(data.Request().BranchId)));
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_week BEFORE UPDATE ON WorkingHours WHEN NEW.Day = 6 BEGIN SELECT RAISE(ABORT, 'test_failure'); END;");
            var week = Week(data.Request().BranchId); foreach (var day in week.Days) day.Opens = new(10, 0);
            var result = Assert.IsType<ObjectResult>(await Controller(db, data.UnlinkedStaffId).WeeklyHours(week)); Assert.Equal(500, result.StatusCode);
        }
        await using var verify = data.Context();
        Assert.Equal(7, await verify.WorkingHours.CountAsync());
        Assert.All(await verify.WorkingHours.ToListAsync(), h => Assert.Equal(new TimeOnly(9, 0), h.Opens));
    }

    [Fact]
    public async Task HttpEndpointRequiresAdminScopeAndAntiforgery()
    {
        await using var data = await BookingTests.Database.Create(); using var site = new BookingMvcTests.Site(data);
        var values = new Dictionary<string, string> { ["BranchId"] = data.Request().BranchId.ToString() };
        using var anonymous = site.Client(); using var patient = await site.Login(data.PatientId);
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.PostAsync("/ClinicManagement/WeeklyHours", new FormUrlEncodedContent(values))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await patient.PostAsync("/ClinicManagement/WeeklyHours", new FormUrlEncodedContent(values))).StatusCode);
        using (var scope = site.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(); await roles.CreateAsync(new IdentityRole(AuthRoles.ClinicAdmin));
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); await users.AddToRoleAsync((await users.FindByIdAsync(data.UnlinkedStaffId))!, AuthRoles.ClinicAdmin);
        }
        using var admin = await site.Login(data.UnlinkedStaffId);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/ClinicManagement/WeeklyHours", new FormUrlEncodedContent(values))).StatusCode);
        await using (var db = data.Context()) { db.ClinicAdministrators.Add(new() { UserId = data.UnlinkedStaffId, ClinicId = data.Fixture.Clinic.Id }); await db.SaveChangesAsync(); }
        var html = await admin.GetStringAsync("/ClinicManagement");
        values["__RequestVerificationToken"] = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(values["__RequestVerificationToken"]);
        for (var d = 0; d < 7; d++) { values[$"Days[{d}].Day"] = d.ToString(); values[$"Days[{d}].Closed"] = "true"; }
        values["BranchId"] = data.Other.Department.BranchId.ToString();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync("/ClinicManagement/WeeklyHours", new FormUrlEncodedContent(values))).StatusCode);
        values["BranchId"] = data.Request().BranchId.ToString();
        var saved = await admin.PostAsync("/ClinicManagement/WeeklyHours", new FormUrlEncodedContent(values));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode); Assert.Equal("application/json", saved.Content.Headers.ContentType!.MediaType);
    }
}
