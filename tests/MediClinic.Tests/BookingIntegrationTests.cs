using MediClinic.Application;
using MediClinic.Domain;
using MediClinic.Infrastructure.Identity;
using MediClinic.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;

namespace MediClinic.Tests;

public sealed class BookingIntegrationTests
{
    [Fact]
    public async Task PriceSnapshotSurvivesCatalogChangesAndDetailsAreIsolated()
    {
        await using var data = await BookingTests.Database.Create();
        Guid id;
        await using (var db = data.Context())
        {
            var catalog = await db.MedicalServices.SingleAsync(s => s.Id == data.Fixture.Service.Id);
            catalog.Update(catalog.Name, 30, 123.45m); await db.SaveChangesAsync();
            id = await data.Service(db).Book(data.PatientId, data.Request());
            catalog.Update(catalog.Name, 45, 999m); await db.SaveChangesAsync();
        }
        await using (var db = data.Context())
        {
            var item = (await data.Service(db).Find(data.PatientId, id, false))!;
            Assert.Equal(123.45m, item.Price); Assert.Equal(TimeSpan.FromMinutes(30), item.EndsAt-item.StartsAt);
            Assert.Equal(data.Fixture.Clinic.Name, item.Clinic); Assert.Equal(data.Fixture.Department.Name, item.Department);
            Assert.Equal(item, (await data.Service(db).StaffAppointment(data.StaffId, id))!.Appointment);
            Assert.Null(await data.Service(db).StaffAppointment(data.OtherStaffId, id));
        }
        using var site = new BookingMvcTests.Site(data);
        using var patient = await site.Login(data.PatientId); using var staff = await site.Login(data.StaffId);
        foreach (var pair in new[] { (patient, $"/Appointments/Details/{id}"), (staff, $"/StaffAppointments/Details/{id}") })
        {
            var html = await pair.Item1.GetStringAsync(pair.Item2);
            Assert.Contains("123,45 TL", html); Assert.Contains(data.Fixture.Clinic.Name, html);
            Assert.Contains(data.Fixture.Department.Name, html); Assert.DoesNotContain("999", html);
        }
    }

    [Theory]
    [InlineData(true, 9, 17, false)]
    [InlineData(false, 13, 17, false)]
    [InlineData(false, 9, 12, false)]
    [InlineData(false, 9, 12.25, false)]
    [InlineData(false, 12, 12.5, true)]
    public async Task SlotsAndBookingUseIdenticalBranchBoundaries(bool closed, double opens, double closes, bool allowed)
    {
        await using var data = await BookingTests.Database.Create(); await using var db = data.Context();
        db.WorkingHours.Add(new(Guid.NewGuid(), data.Request().BranchId, DayOfWeek.Wednesday, closed,
            TimeOnly.MinValue.AddHours(opens), TimeOnly.MinValue.AddHours(closes))); await db.SaveChangesAsync();
        var service = data.Service(db);
        var slots = await service.Slots(data.PatientId, data.Fixture.Doctor.Id, data.Fixture.Service.Id, new(2030,1,2));
        Assert.Equal(allowed, slots.Contains(data.Request().LocalStart));
        if (allowed) await service.Book(data.PatientId, data.Request());
        else await Assert.ThrowsAsync<BookingException>(() => service.Book(data.PatientId, data.Request()));
    }

    // Commit a competing administration change after booking prechecks but before its INSERT.
    private sealed class ChangeBeforeInsert(Func<Task> change) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken token = default)
        { await change(); return result; }
    }

    [Theory]
    [InlineData("hours")][InlineData("availability")][InlineData("assignment")][InlineData("price")][InlineData("duration")]
    public async Task CommittedAdministrativeChangeAfterPrechecksRollsBackBooking(string kind)
    {
        await using var data = await BookingTests.Database.Create();
        async Task Change()
        {
            await using var other = data.Context();
            if (kind == "hours") other.WorkingHours.Add(new(Guid.NewGuid(), data.Request().BranchId, DayOfWeek.Wednesday, true, null, null));
            else if (kind == "availability") other.StaffAvailabilities.Remove(await other.StaffAvailabilities.SingleAsync());
            else if (kind == "assignment")
            {
                var doctor = await other.Doctors.Include(d => d.Services).SingleAsync(d => d.Id == data.Fixture.Doctor.Id);
                doctor.AssignServices([]);
            }
            else
            {
                var service = await other.MedicalServices.SingleAsync(s => s.Id == data.Fixture.Service.Id);
                service.Update(service.Name, kind == "duration" ? 60 : 30, kind == "price" ? 100m : 0m);
            }
            await other.SaveChangesAsync();
        }
        await using (var db = data.Context(new ChangeBeforeInsert(Change)))
            await Assert.ThrowsAsync<BookingException>(() => data.Service(db).Book(data.PatientId, data.Request()));
        await using var verify = data.Context(); Assert.Empty(await verify.Appointments.ToListAsync());
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task HoursEditsPreservePendingAndConfirmedBookings(bool confirmed)
    {
        await using var data = await BookingTests.Database.Create(); await using var db = data.Context();
        await data.Service(db).Book(data.PatientId, data.Request());
        if (confirmed) { (await db.Appointments.SingleAsync()).Confirm(); await db.SaveChangesAsync(); }
        db.ClinicAdministrators.Add(new() { UserId = data.UnlinkedStaffId, ClinicId = data.Fixture.Clinic.Id }); await db.SaveChangesAsync();
        var http = new DefaultHttpContext { User = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, data.UnlinkedStaffId)], "test")) };
        var controller = new ClinicManagementController(db) { ControllerContext = new() { HttpContext = http }, TempData = new TempDataDictionary(http, new MemoryTempData()) };
        await controller.Hours(data.Request().BranchId, DayOfWeek.Wednesday, true, null, null);
        Assert.Empty(await db.WorkingHours.ToListAsync()); Assert.NotNull(controller.TempData["ManagementError"]);
        await controller.Hours(data.Request().BranchId, DayOfWeek.Wednesday, false, new(12,0), new(12,30));
        Assert.Single(await db.WorkingHours.ToListAsync());
    }
    [Fact]
    public async Task M12MigrationPreservesM11RowsWithoutInferringCatalogPrice()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new MediClinic.Infrastructure.Persistence.MediClinicDbContext(
            new DbContextOptionsBuilder<MediClinic.Infrastructure.Persistence.MediClinicDbContext>().UseSqlite(connection).Options);
        var migrator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(db);
        await migrator.MigrateAsync("20261112161952_StaffManagement");
        var fixture = new DomainFixture(); var appointment = fixture.Appointment();
        await fixture.SeedLegacy(db, appointment);
        await db.Database.ExecuteSqlRawAsync("UPDATE MedicalServices SET Price = '123.45'");
        await migrator.MigrateAsync();
        var restored = await db.Appointments.SingleAsync();
        Assert.Null(restored.Price); Assert.Equal(appointment.Id, restored.Id);
        Assert.Equal(appointment.StartsAt, restored.StartsAt); Assert.Equal(appointment.EndsAt, restored.EndsAt);
        Assert.Equal(2, await db.Set<Person>().CountAsync());
        Assert.Equal(123.45m, (await db.MedicalServices.SingleAsync()).Price);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    private sealed class MemoryTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
