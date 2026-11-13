using System.Net;
using System.Text.RegularExpressions;
using MediClinic.Application;
using MediClinic.Domain;
using MediClinic.Infrastructure;
using MediClinic.Infrastructure.Identity;
using MediClinic.Infrastructure.Persistence;
using MediClinic.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MediClinic.Tests;

public sealed class DemoVerificationTests
{
    private sealed class Site : WebApplicationFactory<Program>
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), $"mediclinic-demo-{Guid.NewGuid()}.db");
        public string EnvironmentName { get; set; } = "Development";
        public string Password { get; } = Guid.NewGuid().ToString("N") + "aA1!";
        private string Connection => $"Data Source={path};Foreign Keys=True";
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment(EnvironmentName)
            .UseSetting("ConnectionStrings:MediClinic", Connection);
        public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        public async Task Seed(string environment = "Development", string? password = null, bool migrate = true)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:PatientPassword"] = password ?? Password, ["Demo:StaffPassword"] = password ?? Password
            });
            builder.Services.AddInfrastructure(Connection);
            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => options.Password.RequiredLength = 12)
                .AddEntityFrameworkStores<MediClinicDbContext>();
            await using var app = builder.Build();
            if (migrate)
            {
                using var scope = app.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<MediClinicDbContext>().Database.MigrateAsync();
            }
            await DemoSeed.RunAsync(app);
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
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
    private static Task<HttpResponseMessage> Login(HttpClient client, string email, string password) =>
        Post(client, "/Account/Login", "/Account/Login", new() { ["Email"] = email, ["Password"] = password });

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public async Task SeederRejectsOtherEnvironmentsBeforeDatabaseAccess(string environment)
    {
        using var site = new Site();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => site.Seed(environment, migrate: false));
        Assert.Contains("requires Development", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public async Task MissingOrInvalidSecretsDoNotLeavePartialData(string password)
    {
        using var site = new Site();
        await Assert.ThrowsAsync<InvalidOperationException>(() => site.Seed(password: password));
        using var scope = site.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediClinicDbContext>();
        Assert.Empty(await db.Clinics.ToListAsync());
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.Roles.ToListAsync());
    }

    [Fact]
    public async Task UnmigratedDatabaseIsRefused()
    {
        using var site = new Site();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => site.Seed(migrate: false));
        Assert.Contains("migrations", error.Message);
    }

    [Fact]
    public async Task SeedIsIdempotentPreservesRecordsAndMapsRoles()
    {
        using var site = new Site();
        await site.Seed();
        using var scope = site.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediClinicDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var originalHashes = await db.Users.ToDictionaryAsync(x => x.Id, x => x.PasswordHash);
        var existing = new Patient(Guid.NewGuid(), "Existing", "Patient", new DateOnly(1980, 1, 1));
        db.Add(existing);
        await db.SaveChangesAsync();
        var availability = await db.StaffAvailabilities.CountAsync();
        await site.Seed();
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.Clinics.CountAsync());
        Assert.Equal(2, await db.Branches.CountAsync());
        Assert.Equal(2, await db.Departments.CountAsync());
        Assert.Equal(3, await db.Doctors.CountAsync());
        Assert.Equal(2, await db.MedicalServices.CountAsync());
        Assert.Equal(2, await db.Patients.CountAsync());
        Assert.True(await db.Patients.AnyAsync(x => x.Id == existing.Id));
        Assert.Equal(30, availability);
        Assert.Equal(availability, await db.StaffAvailabilities.CountAsync());
        foreach (var user in await db.Users.Include(x => x.Patient).Include(x => x.Staff).ThenInclude(x => x!.Department).ToListAsync())
        {
            Assert.Equal(originalHashes[user.Id], user.PasswordHash);
            Assert.Equal(new[] { user.PatientId != null ? AuthRoles.Patient : AuthRoles.Staff }, await users.GetRolesAsync(user));
            Assert.True(user.Patient != null ^ user.Staff != null);
            if (user.Staff != null) Assert.NotNull(user.Staff.Department);
        }
        foreach (var interval in await db.StaffAvailabilities.ToListAsync())
        {
            Assert.DoesNotContain(Scheduling.Local(interval.StartsAt).DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday });
            Assert.Equal(8, (interval.EndsAt - interval.StartsAt).TotalHours);
        }
    }

    [Fact]
    public async Task EmailCollisionRollsBackWithoutTakingOverExistingAccount()
    {
        using var site = new Site();
        await site.Seed();
        using var scope = site.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediClinicDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync("demo.patient@example.test"))!;
        Assert.True((await users.RemoveFromRoleAsync(user, AuthRoles.Patient)).Succeeded);
        var hash = user.PasswordHash;
        await Assert.ThrowsAsync<InvalidOperationException>(() => site.Seed());
        db.ChangeTracker.Clear();
        Assert.Equal(hash, (await users.FindByIdAsync(user.Id))!.PasswordHash);
        Assert.Empty(await users.GetRolesAsync((await users.FindByIdAsync(user.Id))!));
        Assert.Equal(3, await db.Users.CountAsync());
    }

    [Fact]
    public async Task DemoCredentialsCannotLoginOutsideDevelopment()
    {
        using var site = new Site { EnvironmentName = "Production" };
        await site.Seed();
        using var client = site.Client();
        var response = await Login(client, "demo.patient@example.test", site.Password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("E-posta veya parola hatalı.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Appointments")).StatusCode);
    }

    [Fact]
    public async Task SeededAccountsCompleteBookingReviewCancellationAndRejectionThroughMvc()
    {
        using var site = new Site();
        await site.Seed();
        using var patient = site.Client(); using var staff = site.Client(); using var otherStaff = site.Client(); using var anonymous = site.Client();
        Assert.Equal(HttpStatusCode.Redirect, (await Login(patient, "demo.patient@example.test", site.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Login(staff, "demo.staff.central@example.test", site.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Login(otherStaff, "demo.staff.riverside@example.test", site.Password)).StatusCode);
        using var scope = site.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediClinicDbContext>();
        var booking = scope.ServiceProvider.GetRequiredService<IAppointmentBooking>();
        var patientId = DemoSeed.Id("user/patient").ToString();
        var option = (await booking.Options(patientId)).First(x => x.Branch == "Merkez");
        var start = await db.StaffAvailabilities.MinAsync(x => x.StartsAt);
        var date = DateOnly.FromDateTime(Scheduling.Local(start));
        var slot = (await booking.Slots(patientId, option.DoctorId, option.ServiceId, date)).First();
        var selection = $"/Appointments/Book?ClinicId={option.ClinicId}&BranchId={option.BranchId}&DepartmentId={option.DepartmentId}&DoctorId={option.DoctorId}&ServiceId={option.ServiceId}&Date={date:yyyy-MM-dd}";
        Assert.Contains(slot.ToString("yyyy-MM-ddTHH:mm:ss"), await patient.GetStringAsync(selection));
        Dictionary<string, string> Form() => new()
        {
            ["ClinicId"] = option.ClinicId.ToString(), ["BranchId"] = option.BranchId.ToString(),
            ["DepartmentId"] = option.DepartmentId.ToString(), ["DoctorId"] = option.DoctorId.ToString(),
            ["Date"] = date.ToString("yyyy-MM-dd"), ["ServiceId"] = option.ServiceId.ToString(), ["LocalStart"] = slot.ToString("yyyy-MM-ddTHH:mm:ss")
        };
        var created = await Post(patient, selection, "/Appointments/Book", Form());
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var url = created.Headers.Location!.OriginalString;
        Assert.Contains("Pending", await patient.GetStringAsync(url));
        var id = (await db.Appointments.AsNoTracking().SingleAsync()).Id;
        Assert.Equal(HttpStatusCode.OK, (await Post(patient, selection, "/Appointments/Book", Form())).StatusCode);
        Assert.Equal(1, await db.Appointments.CountAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await patient.GetAsync("/StaffAppointments")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherStaff.GetAsync($"/StaffAppointments/Details/{id}")).StatusCode);
        var review = $"/StaffAppointments/Details/{id}";
        await Post(staff, review, "/StaffAppointments/Review", new() { ["Id"] = id.ToString(), ["Action"] = "Confirm" });
        Assert.Contains("Confirmed", await patient.GetStringAsync(url));
        await site.Seed(); // Never changes existing appointments, even when rerun after booking.
        Assert.Contains("Confirmed", await patient.GetStringAsync(url));
        await Post(patient, url, "/Appointments/Cancel", new() { ["Id"] = id.ToString(), ["Reason"] = "Plans changed" });
        Assert.Contains("Cancelled", await patient.GetStringAsync(url));
        created = await Post(patient, selection, "/Appointments/Book", Form());
        url = created.Headers.Location!.OriginalString;
        var second = await db.Appointments.AsNoTracking().SingleAsync(x => x.Id != id);
        await Post(staff, $"/StaffAppointments/Details/{second.Id}", "/StaffAppointments/Review",
            new() { ["Id"] = second.Id.ToString(), ["Action"] = "Reject", ["Reason"] = "Demo rejection" });
        var rejected = await patient.GetStringAsync(url);
        Assert.Contains("Rejected", rejected); Assert.Contains("Demo rejection", rejected);
    }
}
