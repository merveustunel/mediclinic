using System.Net;
using System.Text.RegularExpressions;
using MediClinic.Infrastructure.Identity;
using MediClinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace MediClinic.Tests;

public sealed class AuthenticationTests
{
    [Theory]
    [InlineData("Login", "")]
    [InlineData("Login", "   ")]
    [InlineData("Register", "")]
    [InlineData("Register", "   ")]
    public async Task MissingAuthCredentialsShowTurkishRequiredMessages(string action, string value)
    {
        using var site = new Site(); using var client = await site.Client();
        var form = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/" + action));
        Assert.Contains("data-val-required=\"E-posta alanı zorunludur.\"", form);
        Assert.Contains("data-val-required=\"Parola alanı zorunludur.\"", form);
        var data = Registration();
        data["Email"] = value; data["Password"] = value;
        var response = await Post(client, action, data);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches("<span[^>]*id=\"Email-error\"[^>]*>E-posta alanı zorunludur.</span>", html);
        Assert.Matches("<span[^>]*id=\"Password-error\"[^>]*>Parola alanı zorunludur.</span>", html);
        Assert.DoesNotContain("The Email field is required.", html);
        Assert.DoesNotContain("The Password field is required.", html);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Dashboard")).StatusCode);
        using var scope = site.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<MediClinicDbContext>().Users.ToListAsync());
    }

    [Theory]
    [InlineData("Login", "current-password")]
    [InlineData("Register", "new-password")]
    public async Task AuthFormsAssociateLabelsErrorsAndPasswordControls(string action, string autocomplete)
    {
        using var site = new Site(); using var client = await site.Client();
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/" + action));
        Assert.Contains("method=\"post\"", html);
        Assert.Contains("action=\"/Account/" + action + "\"", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.Contains("role=\"alert\"", html);
        Assert.Contains("autocomplete=\"" + autocomplete + "\"", html);
        Assert.Contains("data-password-toggle=\"Password\"", html);
        Assert.Contains("aria-controls=\"Password\"", html);
        Assert.Contains("aria-pressed=\"false\" hidden", html);
        foreach (var field in action == "Login" ? new[] { "Email", "Password" } : new[] { "Email", "Password", "FirstName", "LastName", "DateOfBirth" })
        {
            Assert.Contains("for=\"" + field + "\"", html);
            Assert.Contains("id=\"" + field + "-error\"", html);
            Assert.Contains("aria-describedby=\"" + field + "-error", html);
        }
    }

    [Theory]
    [InlineData("Login")]
    [InlineData("Register")]
    public async Task InvalidAuthFormsMarkFieldsAndNeverEchoPasswords(string action)
    {
        using var site = new Site(); using var client = await site.Client();
        var submittedPassword = "Invalid-" + Guid.NewGuid().ToString("N");
        var response = await Post(client, action, new() { ["Email"] = "invalid", ["Password"] = submittedPassword });
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("validation-summary-errors", html);
        Assert.Contains("field-validation-error", html);
        Assert.Matches("<input[^>]*aria-invalid=\"true\"[^>]*id=\"Email\"", html);
        Assert.DoesNotContain(submittedPassword, html);
        Assert.Contains("type=\"password\"", html);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Dashboard")).StatusCode);
    }

    [Fact]
    public async Task AccessDeniedKeepsForbiddenStatusAndOffersSafeNavigation()
    {
        using var site = new Site(); using var client = await site.Client();
        var response = await client.GetAsync("/Account/AccessDenied");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Erişim engellendi", html);
        Assert.Contains("href=\"/Account/Login\"", html);
        Assert.DoesNotContain("href=\"/Dashboard\"", html);
    }

    // Each test uses a real migrated SQLite file, independent cookies, and HTTPS.
    private sealed class Site : WebApplicationFactory<Program>
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), $"mediclinic-auth-{Guid.NewGuid()}.db");
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:MediClinic", $"Data Source={path};Foreign Keys=True");
        public async Task<HttpClient> Client()
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<MediClinicDbContext>().Database.MigrateAsync();
            return CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }
    private const string Email = "patient@example.test";
    private static readonly string Password = "Test-" + Guid.NewGuid().ToString("N") + "aA1!";
    private static Dictionary<string, string> Registration() => new()
    {
        ["Email"] = Email, ["Password"] = Password, ["FirstName"] = "Test", ["LastName"] = "Patient", ["DateOfBirth"] = "2000-01-01"
    };
    private static async Task<HttpResponseMessage> Post(HttpClient client, string action, Dictionary<string, string> data)
    {
        var html = await client.GetStringAsync("/Account/" + (action == "Logout" ? "Login" : action));
        data["__RequestVerificationToken"] = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(data["__RequestVerificationToken"]);
        return await client.PostAsync("/Account/" + action, new FormUrlEncodedContent(data));
    }
    [Fact]
    public async Task RegistrationPersistsLinkAndPatientRole_LoginLogoutEnforceAuthorization()
    {
        using var site = new Site(); using var client = await site.Client();
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Dashboard/Patient")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "Register", Registration())).StatusCode);
        using (var scope = site.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediClinicDbContext>();
            var user = await db.Users.Include(x => x.Patient).SingleAsync();
            Assert.NotNull(user.Patient); Assert.Equal(user.Patient.Id, user.PatientId); Assert.Null(user.StaffId);
            Assert.NotNull(user.PasswordHash); Assert.NotEqual(Password, user.PasswordHash);
            Assert.Equal(new[] { AuthRoles.Patient }, await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().GetRolesAsync(user));
        }
        var login = await Post(client, "Login", new() { ["Email"] = Email, ["Password"] = Password });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), x => x.StartsWith("__Host-MediClinic="));
        Assert.Contains("secure", cookie); Assert.Contains("httponly", cookie); Assert.Contains("samesite=lax", cookie);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Dashboard/Patient")).StatusCode);
        var forbidden = await client.GetAsync("/Dashboard/ClinicAdmin");
        Assert.Contains("AccessDenied", forbidden.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(forbidden.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "Logout", new())).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Dashboard/Patient")).StatusCode);
    }
    [Theory]
    [InlineData("Staff")]
    [InlineData("ClinicAdmin")]
    public async Task PublicRegistrationRejectsPrivilegedRole(string role)
    {
        using var site = new Site(); using var client = await site.Client();
        var data = Registration(); data["Role"] = role;
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "Register", data)).StatusCode);
        using var scope = site.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<MediClinicDbContext>().Users.ToListAsync());
    }
    [Fact]
    public async Task InvalidCredentialsAndDuplicateRegistrationHaveGenericResponsesAndNoExtraPeople()
    {
        using var site = new Site(); using var client = await site.Client();
        var first = await (await Post(client, "Register", Registration())).Content.ReadAsStringAsync();
        var duplicate = await (await Post(client, "Register", Registration())).Content.ReadAsStringAsync();
        Assert.Contains("Kayıt talebiniz alındı", WebUtility.HtmlDecode(first)); Assert.Contains("Kayıt talebiniz alındı", WebUtility.HtmlDecode(duplicate));
        foreach (var email in new[] { Email, "unknown@example.test" })
        {
            var response = await Post(client, "Login", new() { ["Email"] = email, ["Password"] = "Wrong-credentials1!" });
            Assert.Contains("E-posta veya parola hatalı.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
            Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Dashboard/Patient")).StatusCode);
        }
        using var scope = site.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<MediClinicDbContext>().Patients.CountAsync());
    }
    [Theory]
    [InlineData(AuthRoles.Patient)]
    [InlineData(AuthRoles.Staff)]
    [InlineData(AuthRoles.ClinicAdmin)]
    public async Task RolesCanOnlyAccessTheirOwnLanding(string role)
    {
        using var site = new Site(); using var client = await site.Client();
        using (var scope = site.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var db = scope.ServiceProvider.GetRequiredService<MediClinicDbContext>();
            var fixture = new DomainFixture();
            ApplicationUser user;
            if (role == AuthRoles.Patient) { db.Add(fixture.Patient); user = ApplicationUser.ForPatient(Email, fixture.Patient); }
            else if (role == AuthRoles.Staff) { db.Add(fixture.Doctor); user = ApplicationUser.ForStaff(Email, fixture.Doctor); }
            else user = ApplicationUser.Administrator(Email);
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        }
        var login = await Post(client, "Login", new() { ["Email"] = Email, ["Password"] = Password });
        Assert.Equal("/Dashboard", login.Headers.Location!.OriginalString);
        var landing = await client.GetAsync(login.Headers.Location);
        Assert.Equal("/Dashboard/" + role, landing.Headers.Location!.OriginalString);
        foreach (var target in AuthRoles.All)
            Assert.Equal(target == role ? HttpStatusCode.OK : HttpStatusCode.Redirect, (await client.GetAsync("/Dashboard/" + target)).StatusCode);
        var shell = WebUtility.HtmlDecode(await client.GetStringAsync("/Dashboard/" + role));
        Assert.Contains("<html lang=\"tr\">", shell);
        Assert.Contains("href=\"#main-content\"", shell);
        Assert.Contains("aria-current=\"page\"", shell);
        Assert.Contains("action=\"/Account/Logout\"", shell);
        Assert.Contains("name=\"__RequestVerificationToken\"", shell);
        Assert.Equal(role == AuthRoles.Patient, shell.Contains("href=\"/Appointments\""));
        Assert.Equal(role == AuthRoles.Patient, shell.Contains("href=\"/Appointments/Book\""));
        Assert.Equal(role == AuthRoles.Staff, shell.Contains("href=\"/StaffAppointments\""));
        using var verify = site.Services.CreateScope();
        var restored = await verify.ServiceProvider.GetRequiredService<MediClinicDbContext>().Users.Include(x => x.Staff).SingleAsync();
        if (role == AuthRoles.Staff) Assert.NotNull(restored.Staff);
        if (role == AuthRoles.ClinicAdmin) { Assert.Null(restored.StaffId); Assert.Null(restored.PatientId); }
    }
    [Fact]
    public async Task UnsafeRequestsRequireAntiforgeryAndLogoutRequiresPost()
    {
        using var site = new Site(); using var client = await site.Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Account/Register", new FormUrlEncodedContent(Registration()))).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.GetAsync("/Account/Logout")).StatusCode);
    }
    [Fact]
    public async Task LockoutBlocksCorrectPasswordAfterRepeatedFailures()
    {
        using var site = new Site(); using var client = await site.Client();
        await Post(client, "Register", Registration());
        for (var attempt = 0; attempt < 5; attempt++)
            await Post(client, "Login", new() { ["Email"] = Email, ["Password"] = "Incorrect1!" });
        var response = await Post(client, "Login", new() { ["Email"] = Email, ["Password"] = Password });
        Assert.Contains("E-posta veya parola hatalı.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        using var scope = site.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await users.IsLockedOutAsync((await users.FindByEmailAsync(Email))!));
    }
    [Theory]
    [InlineData("Password", "short")]
    [InlineData("DateOfBirth", "2999-01-01")]
    [InlineData("FirstName", "   ")]
    public async Task InvalidRegistrationDoesNotPersistAccountOrPatient(string field, string value)
    {
        using var site = new Site(); using var client = await site.Client();
        var data = Registration(); data[field] = value;
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "Register", data)).StatusCode);
        using var scope = site.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediClinicDbContext>();
        Assert.Empty(await db.Users.ToListAsync()); Assert.Empty(await db.Patients.ToListAsync());
    }
    [Fact]
    public async Task BootstrapRefusesProductionBeforeAccessingDatabase()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        await using var app = builder.Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() => MediClinic.Web.IdentityBootstrap.RunAsync(app));
    }

    [Fact]
    public async Task IdentityMigrationPreservesExistingClinicalRecords()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var db = new MediClinicDbContext(new DbContextOptionsBuilder<MediClinicDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261105220707_InitialPersistence");
        var patient = new MediClinic.Domain.Patient(Guid.NewGuid(), "Existing", "Patient", new DateOnly(1990, 1, 1));
        db.Patients.Add(patient); await db.SaveChangesAsync();
        await migrator.MigrateAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(patient.Id, (await db.Patients.SingleAsync()).Id);
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

}
