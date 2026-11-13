using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MediClinic.Web.Localization;

namespace MediClinic.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void ResourcesCoverAllExplicitValidationAndDisplayAttributesAndKeepFormatArguments()
    {
        var tr = Ui.Catalog("tr-TR"); var en = Ui.Catalog("en-GB");
        foreach (var type in typeof(MediClinic.Web.Models.RegisterModel).Assembly.GetTypes().Where(t => t.Namespace == "MediClinic.Web.Models"))
        foreach (var property in type.GetProperties())
        foreach (var attribute in property.GetCustomAttributes(false))
        {
            var key = attribute switch {
                System.ComponentModel.DataAnnotations.ValidationAttribute validation => validation.ErrorMessage,
                System.ComponentModel.DataAnnotations.DisplayAttribute display => display.Name,
                _ => null
            };
            if (key is not null) { Assert.True(tr.ContainsKey(key), key); Assert.True(en.ContainsKey(key), key); }
        }
        foreach (var key in tr.Keys)
        {
            Assert.DoesNotContain('\uFFFD', tr[key]); Assert.DoesNotContain('\uFFFD', en[key]);
            Assert.Equal(Regex.Matches(tr[key], "\\{\\d+\\}").Select(m => m.Value).Order(), Regex.Matches(en[key], "\\{\\d+\\}").Select(m => m.Value).Order());
        }
    }
    private static string Html(string value) => WebUtility.HtmlDecode(Regex.Replace(value, "<script\\b[^>]*>[\\s\\S]*?</script>", "", RegexOptions.IgnoreCase));
    private static async Task<HttpResponseMessage> Post(HttpClient client, string page, string action, Dictionary<string,string> values, bool json = false)
    {
        var html = await client.GetStringAsync(page);
        values["__RequestVerificationToken"] = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        using var request = new HttpRequestMessage(HttpMethod.Post, action) { Content = new FormUrlEncodedContent(values) };
        if (json) request.Headers.Accept.ParseAdd("application/json");
        return await client.SendAsync(request);
    }
    private static Task<HttpResponseMessage> Language(HttpClient client, string culture, string returnUrl = "/Account/Register", bool json = false)
        => Post(client, "/Account/Register", "/Preferences/Language", new() { ["culture"] = culture, ["returnUrl"] = returnUrl }, json);

    [Fact]
    public async Task DefaultIsTurkishAndCookiePersistsEnglishWithoutAcceptLanguageOverride()
    {
        await using var data = await BookingTests.Database.Create(); using var site = new BookingMvcTests.Site(data); using var client = site.Client();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US");
        Assert.Contains("<html lang=\"tr\"", await client.GetStringAsync("/Account/Register"));
        var response = await Language(client, "en-GB", "/Account/Register?from=home");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Register?from=home", response.Headers.Location!.OriginalString);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(v => v.StartsWith(".AspNetCore.Culture="));
        Assert.Contains("secure", cookie); Assert.Contains("httponly", cookie); Assert.Contains("expires=", cookie);
        for (var i = 0; i < 2; i++)
        {
            var html = Html(await client.GetStringAsync("/Account/Register"));
            Assert.Contains("<html lang=\"en\"", html); Assert.Contains("Patient registration", html);
            Assert.Contains("Email is required.", html); Assert.Contains("Date of birth", html);
            Assert.DoesNotContain("Hasta kaydı oluşturun", html);
        }
        await Language(client, "tr-TR");
        Assert.Contains("<html lang=\"tr\"", await client.GetStringAsync("/Account/Login"));
    }
    [Theory]
    [InlineData("en-US")][InlineData("invalid")][InlineData("../en-GB")][InlineData("")]
    public async Task InvalidCulturesNeverSetPreference(string culture)
    {
        await using var data = await BookingTests.Database.Create(); using var site = new BookingMvcTests.Site(data); using var client = site.Client();
        var response = await Language(client, culture);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
    }
    [Theory]
    [InlineData("https://evil.example")][InlineData("//evil.example")][InlineData("/\\evil.example")]
    public async Task ReturnUrlCannotRedirectOffSite(string returnUrl)
    {
        await using var data = await BookingTests.Database.Create(); using var site = new BookingMvcTests.Site(data); using var client = site.Client();
        var response = await Language(client, "en-GB", returnUrl);
        Assert.Equal("/", response.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Preferences/Language", new FormUrlEncodedContent(new Dictionary<string,string> { ["culture"] = "tr-TR" }))).StatusCode);
    }
    [Theory]
    [InlineData("tr-TR", "E-posta alanı zorunludur.", "Parola en az 12 karakter olmalıdır.")]
    [InlineData("en-GB", "Email is required.", "The password must be at least 12 characters long.")]
    public async Task ValidationAndIdentityFollowCulture(string culture, string required, string password)
    {
        await using var data = await BookingTests.Database.Create(); using var site = new BookingMvcTests.Site(data); using var client = site.Client();
        await Language(client, culture);
        var response = await Post(client, "/Account/Login", "/Account/Login", new() { ["Email"] = "", ["Password"] = "" });
        Assert.Contains(required, Html(await response.Content.ReadAsStringAsync()));
        response = await Post(client, "/Account/Register", "/Account/Register", new() { ["Email"] = "localization@example.test", ["Password"] = "short", ["FirstName"] = "Ayşe", ["LastName"] = "Demo", ["DateOfBirth"] = "1990-01-01" });
        var html = Html(await response.Content.ReadAsStringAsync());
        Assert.Contains(password, html); Assert.DoesNotContain("PasswordTooShort", html);
    }
    [Fact]
    public async Task PatientStaffAdminAndManagementScreensRenderEnglish()
    {
        await using var data = await BookingTests.Database.Create(); using var site = new BookingMvcTests.Site(data);
        using var patient = await site.Login(data.PatientId); using var staff = await site.Login(data.StaffId);
        using var admin = await EmployeeManagementTests.Admin(site, data);
        foreach (var client in new[] { patient, staff, admin }) await Language(client, "en-GB");
        foreach (var entry in new[] { (patient, "/Dashboard/Patient", "Track your appointments"), (patient, "/Appointments", "My appointments"), (patient, "/Appointments/Book", "Choose your service"), (staff, "/Dashboard/Staff", "STAFF DASHBOARD"), (staff, "/StaffAppointments", "Department appointments"), (admin, "/Dashboard/ClinicAdmin", "ADMINISTRATOR DASHBOARD"), (admin, "/AdminAppointments", "Clinic appointments"), (admin, "/EmployeeManagement", "Employee management"), (admin, "/ClinicManagement", "Organisation management"), (admin, $"/EmployeeManagement/Weekly?doctorId={data.Fixture.Doctor.Id}", "Create weekly availability") })
        {
            var html = Html(await entry.Item1.GetStringAsync(entry.Item2));
            Assert.Contains("<html lang=\"en\"", html); Assert.Contains(entry.Item3, html);
        }
    }
    [Fact]
    public async Task LiveSwitchReturnsResourceCatalogueAndUsesTheSameSecureCookie()
    {
        await using var data = await BookingTests.Database.Create(); using var site = new BookingMvcTests.Site(data); using var client = site.Client();
        var response = await Language(client, "en-GB", json: true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("en-GB", json.RootElement.GetProperty("culture").GetString());
        Assert.Equal("Sign in", json.RootElement.GetProperty("messages").GetProperty("Giriş yap").GetString());
        Assert.Contains("<html lang=\"en\"", await client.GetStringAsync("/Account/Login"));
    }
    [Theory]
    [InlineData("tr-TR", "1.234,50 TL", "Parola en az bir rakam içermelidir.")]
    [InlineData("en-GB", "1,234.50 TRY", "The password must contain at least one digit.")]
    public void FormatsAndIdentityDescriptionsAreCultureSpecificAndNeverEchoPrivateValues(string culture, string money, string error)
    {
        var previous = CultureInfo.CurrentCulture; var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            Assert.Equal(money, Ui.Money(1234.5m));
            var identity = new LocalizedIdentityErrors();
            Assert.Equal(error, identity.PasswordRequiresDigit().Description);
            Assert.DoesNotContain("private@example.test", identity.InvalidEmail("private@example.test").Description);
            Assert.DoesNotContain("SecretRole", identity.UserNotInRole("SecretRole").Description);
            Assert.Equal(Ui.Catalog("tr-TR").Keys.Order(), Ui.Catalog("en-GB").Keys.Order());
        }
        finally { CultureInfo.CurrentCulture = previous; CultureInfo.CurrentUICulture = previousUi; }
    }
}
