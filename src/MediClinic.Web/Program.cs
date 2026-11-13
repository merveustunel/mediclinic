using MediClinic.Infrastructure.Identity;
using MediClinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using MediClinic.Application;
using MediClinic.Infrastructure;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(options =>
{
    var messages = options.ModelBindingMessageProvider;
    messages.SetAttemptedValueIsInvalidAccessor((value, field) => "Geçerli bir değer giriniz.");
    messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => "Geçerli bir değer giriniz.");
    messages.SetValueIsInvalidAccessor(value => "Geçerli bir değer giriniz.");
    messages.SetValueMustNotBeNullAccessor(value => "Bu alan zorunludur.");
    messages.SetMissingBindRequiredValueAccessor(field => "Bu alan zorunludur.");
    messages.SetValueMustBeANumberAccessor(field => "Geçerli bir sayı giriniz.");
    messages.SetNonPropertyValueMustBeANumberAccessor(() => "Geçerli bir sayı giriniz.");
    messages.SetUnknownValueIsInvalidAccessor(field => "Geçerli bir değer giriniz.");
    messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Geçerli bir değer giriniz.");
    messages.SetMissingKeyOrValueAccessor(() => "Bu alan zorunludur.");
    messages.SetMissingRequestBodyRequiredValueAccessor(() => "Form bilgileri zorunludur.");
});
builder.Services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture("tr-TR").AddSupportedCultures("tr-TR").AddSupportedUICultures("tr-TR");
    options.RequestCultureProviders.Clear();
});
builder.Services.AddApplication();
var connection = new SqliteConnectionStringBuilder(
    builder.Configuration.GetConnectionString("MediClinic")
    ?? throw new InvalidOperationException("ConnectionStrings:MediClinic is required."));
if (connection.DataSource != ":memory:" && !Path.IsPathRooted(connection.DataSource))
    connection.DataSource = Path.Combine(builder.Environment.ContentRootPath, connection.DataSource);
builder.Services.AddInfrastructure(connection.ToString());

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 12;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<MediClinicDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-MediClinic";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});
builder.Services.AddAuthorization(options =>
{
    foreach (var role in AuthRoles.All) options.AddPolicy(role, policy => policy.RequireAuthenticatedUser().RequireRole(role));
});
var app = builder.Build();
if (args.Contains("--seed-demo"))
{
    await MediClinic.Web.DemoSeed.RunAsync(app);
    return;
}
if (args.Contains("--bootstrap-identity"))
{
    await MediClinic.Web.IdentityBootstrap.RunAsync(app);
    return;
}
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseRequestLocalization();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();

public partial class Program { }
