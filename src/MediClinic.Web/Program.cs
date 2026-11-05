using MediClinic.Application;
using MediClinic.Infrastructure;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddApplication();
var connection = new SqliteConnectionStringBuilder(
    builder.Configuration.GetConnectionString("MediClinic")
    ?? throw new InvalidOperationException("ConnectionStrings:MediClinic is required."));
if (connection.DataSource != ":memory:" && !Path.IsPathRooted(connection.DataSource))
    connection.DataSource = Path.Combine(builder.Environment.ContentRootPath, connection.DataSource);
builder.Services.AddInfrastructure(connection.ToString());

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
