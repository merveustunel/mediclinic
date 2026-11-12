using System.Net;
using System.Security.Claims;
using MediClinic.Domain;
using MediClinic.Infrastructure.Identity;
using MediClinic.Infrastructure.Persistence;
using MediClinic.Web.Controllers;
using MediClinic.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;

namespace MediClinic.Tests;

public sealed class ClinicManagementTests
{
    [Fact]
    public async Task MvcRequiresRoleScopeAndAntiforgeryAndRendersCatalog()
    {
        await using var data = await BookingTests.Database.Create();
        using var site = new BookingMvcTests.Site(data);
        using var anonymous = site.Client();
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/ClinicManagement")).StatusCode);
        using var patient = await site.Login(data.PatientId);
        Assert.Equal(HttpStatusCode.Redirect, (await patient.GetAsync("/ClinicManagement")).StatusCode);
        using (var scope = site.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            Assert.True((await roles.CreateAsync(new IdentityRole(AuthRoles.ClinicAdmin))).Succeeded);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(data.UnlinkedStaffId))!, AuthRoles.ClinicAdmin)).Succeeded);
        }
        using var admin = await site.Login(data.UnlinkedStaffId);
        Assert.Contains("Yetkili", await admin.GetStringAsync("/ClinicManagement"));
        using (var db = data.Context())
        {
            db.ClinicAdministrators.Add(new() { UserId = data.UnlinkedStaffId, ClinicId = data.Fixture.Clinic.Id });
            await db.SaveChangesAsync();
        }
        var html = WebUtility.HtmlDecode(await admin.GetStringAsync("/ClinicManagement"));
        Assert.Contains(data.Fixture.Service.Name, html);
        Assert.Contains("Çalışma", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(data.Other.Clinic.Id.ToString(), html);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/ClinicManagement/Edit?kind=Clinic&id={data.Other.Clinic.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/ClinicManagement/Edit", new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "Forged" }))).StatusCode);
        var form = await admin.GetStringAsync($"/ClinicManagement/Edit?kind=Service&id={data.Fixture.Service.Id}");
        var token = WebUtility.HtmlDecode(Regex.Match(form, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(token);
        var create = new Dictionary<string, string> { ["Kind"] = "Clinic", ["Name"] = "MVC clinic", ["__RequestVerificationToken"] = token };
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync("/ClinicManagement/Edit", new FormUrlEncodedContent(create))).StatusCode);
        var values = new Dictionary<string, string> { ["Kind"] = "Service", ["Id"] = data.Fixture.Service.Id.ToString(), ["Name"] = "Updated service", ["DurationMinutes"] = "0", ["Price"] = "-1", ["__RequestVerificationToken"] = token };
        var invalid = await admin.PostAsync("/ClinicManagement/Edit", new FormUrlEncodedContent(values));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode); Assert.Contains("validation-summary-errors", await invalid.Content.ReadAsStringAsync());
        values["DurationMinutes"] = "45"; values["Price"] = "12.50";
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync("/ClinicManagement/Edit", new FormUrlEncodedContent(values))).StatusCode);
        using var verify = data.Context(); Assert.Equal(12.50m, (await verify.MedicalServices.SingleAsync(s => s.Id == data.Fixture.Service.Id)).Price);
    }
    [Theory]
    [InlineData(0, 10)]
    [InlineData(1441, 10)]
    [InlineData(30, -1)]
    public void InvalidServiceValuesAreRejected(int minutes, int price)
    {
        var service = new DomainFixture().Service;
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Update("Service", minutes, price));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Update("Service", 30, 1.001m));
    }

    [Fact]
    public void HoursValidateDayRangeAndClosedDays()
    {
        var branch = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new WorkingHours(Guid.NewGuid(), branch, (DayOfWeek)7, true, null, null));
        Assert.Throws<ArgumentException>(() => new WorkingHours(Guid.NewGuid(), branch, DayOfWeek.Monday, false, null, null));
        Assert.Throws<ArgumentException>(() => new WorkingHours(Guid.NewGuid(), branch, DayOfWeek.Monday, false, new(17, 0), new(9, 0)));
        Assert.Throws<ArgumentException>(() => new WorkingHours(Guid.NewGuid(), branch, DayOfWeek.Monday, false, new(9, 0), new(9, 0)));
        var hours = new WorkingHours(Guid.NewGuid(), branch, DayOfWeek.Monday, false, new(9, 0), new(17, 0));
        hours.Update(true, new(9, 0), new(17, 0));
        Assert.Null(hours.Opens); Assert.Null(hours.Closes); Assert.True(hours.Closed);
    }

    [Fact]
    public async Task ScopedCatalogCrudAndIsolationPreserveAppointments()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new MediClinicDbContext(new DbContextOptionsBuilder<MediClinicDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        var fixture = new DomainFixture(); var other = new Clinic(Guid.NewGuid(), "Other clinic");
        var user = ApplicationUser.Administrator("manager@example.test");
        db.Users.Add(user); db.Clinics.AddRange(fixture.Clinic, other); db.Appointments.Add(fixture.Appointment());
        db.ClinicAdministrators.Add(new() { UserId = user.Id, ClinicId = fixture.Clinic.Id }); await db.SaveChangesAsync();
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)], "test")) };
        var controller = new ClinicManagementController(db) { ControllerContext = new ControllerContext { HttpContext = context }, TempData = new TempDataDictionary(context, new MemoryTempData()) };
        Assert.IsType<NotFoundResult>(await controller.Edit("Clinic", other.Id));
        Assert.IsType<NotFoundResult>(await controller.Edit(new CatalogEditModel { Kind = "Branch", ParentId = other.Id, Name = "Forbidden", Address = "Address" }));
        Assert.IsType<NotFoundResult>(await controller.Index(other.Id, null, null));
        Assert.IsType<NotFoundResult>(await controller.Hours(Guid.NewGuid(), DayOfWeek.Monday, true, null, null));
        Assert.IsType<RedirectToActionResult>(await controller.Edit(new CatalogEditModel { Name = "New clinic" }));
        var created = await db.Clinics.SingleAsync(c => c.Name == "New clinic");
        Assert.True(await db.ClinicAdministrators.AnyAsync(a => a.ClinicId == created.Id && a.UserId == user.Id));
        Assert.IsType<RedirectToActionResult>(await controller.Edit(new CatalogEditModel { Id = created.Id, Name = "Renamed clinic" }));
        Assert.Equal("Renamed clinic", created.Name);
        Assert.IsType<RedirectToActionResult>(await controller.Edit(new CatalogEditModel { Kind = "Branch", ParentId = created.Id, Name = "Branch", Address = "Address" }));
        var branch = await db.Branches.SingleAsync(b => b.Name == "Branch");
        Assert.IsType<RedirectToActionResult>(await controller.Edit(new CatalogEditModel { Kind = "Branch", Id = branch.Id, Name = "Renamed branch", Address = "New address", ParentId = other.Id }));
        Assert.Equal(created.Id, branch.ClinicId); Assert.Equal("New address", branch.Address);
        Assert.IsType<RedirectToActionResult>(await controller.Edit(new CatalogEditModel { Kind = "Department", ParentId = branch.Id, Name = "Department" }));
        var department = await db.Departments.SingleAsync(d => d.Name == "Department");
        Assert.IsType<RedirectToActionResult>(await controller.Edit(new CatalogEditModel { Kind = "Department", Id = department.Id, Name = "Renamed department" }));
        Assert.IsType<RedirectToActionResult>(await controller.Edit(new CatalogEditModel { Kind = "Service", ParentId = department.Id, Name = "Service", DurationMinutes = 45, Price = 1234.56m }));
        var service = await db.MedicalServices.SingleAsync(s => s.Name == "Service");
        Assert.IsType<RedirectToActionResult>(await controller.Edit(new CatalogEditModel { Kind = "Service", Id = service.Id, Name = "Renamed service", DurationMinutes = 60, Price = 2000.01m }));
        Assert.Equal(2000.01m, service.Price); Assert.Equal(TimeSpan.FromHours(1), service.Duration);
        Assert.IsType<ViewResult>(await controller.Edit(new CatalogEditModel { Kind = "Service", ParentId = department.Id, Name = " renamed SERVICE " }));
        controller.ModelState.Clear();
        Assert.IsType<RedirectToActionResult>(await controller.Hours(branch.Id, DayOfWeek.Monday, false, new(9, 0), new(17, 0)));
        Assert.IsType<RedirectToActionResult>(await controller.Hours(branch.Id, DayOfWeek.Monday, true, null, null));
        Assert.True((await db.WorkingHours.SingleAsync()).Closed);
        Assert.Single(await db.Appointments.ToListAsync()); Assert.Equal(0m, fixture.Service.Price);
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "unlinked")], "test"));
        Assert.IsType<ForbidResult>(await controller.Edit(new CatalogEditModel { Name = "Unauthorized clinic" }));
        Assert.Empty(((ClinicManagementModel)((ViewResult)await controller.Index(null, null, null)).Model!).Clinics);
    }

    [Fact]
    public async Task AdditiveMigrationPreservesExistingGraphAndAddsZeroPriceWithoutGrants()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new MediClinicDbContext(new DbContextOptionsBuilder<MediClinicDbContext>().UseSqlite(connection).Options);
        await db.GetService<IMigrator>().MigrateAsync("20261106022559_AppointmentScheduling");
        // Raw legacy insertion avoids referencing the not-yet-existing Price column.
        var f = new DomainFixture();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Clinics (Id, Name) VALUES ({f.Clinic.Id}, {f.Clinic.Name})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Branches (Id, ClinicId, Name, Address) VALUES ({f.Department.BranchId}, {f.Clinic.Id}, {"Branch"}, {"Address"})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Departments (Id, BranchId, Name) VALUES ({f.Department.Id}, {f.Department.BranchId}, {"Department"})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO MedicalServices (Id, DepartmentId, Name, Duration) VALUES ({f.Service.Id}, {f.Department.Id}, {"Service"}, {f.Service.Duration.Ticks})");
        await db.Database.MigrateAsync();
        Assert.Equal(0m, (await db.MedicalServices.SingleAsync()).Price);
        Assert.Single(await db.Clinics.ToListAsync()); Assert.Empty(await db.ClinicAdministrators.ToListAsync());
    }

    private sealed class MemoryTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
