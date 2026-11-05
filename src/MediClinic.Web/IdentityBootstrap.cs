using MediClinic.Infrastructure.Identity;
using MediClinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace MediClinic.Web;
public static class IdentityBootstrap
{
    public static async Task RunAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Bootstrap requires Development.");
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var config = app.Configuration;
        var email = config["Bootstrap:Email"] ?? throw new InvalidOperationException("Bootstrap:Email required.");
        var password = config["Bootstrap:Password"] ?? throw new InvalidOperationException("Bootstrap:Password required.");
        var role = config["Bootstrap:Role"];
        if (role is not (AuthRoles.Staff or AuthRoles.ClinicAdmin)) throw new InvalidOperationException("Bootstrap role must be Staff or ClinicAdmin.");
        var db = services.GetRequiredService<MediClinicDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        ApplicationUser user;
        if (role == AuthRoles.Staff)
        {
            var id = Guid.Parse(config["Bootstrap:StaffId"] ?? throw new InvalidOperationException("Existing StaffId required."));
            var staff = await db.Staff.SingleAsync(x => x.Id == id);
            user = ApplicationUser.ForStaff(email, staff);
        }
        else user = ApplicationUser.Administrator(email);
        Check(await users.CreateAsync(user, password));
        foreach (var name in AuthRoles.All)
            if (!await roles.RoleExistsAsync(name)) Check(await roles.CreateAsync(new IdentityRole(name)));
        Check(await users.AddToRoleAsync(user, role));
        await transaction.CommitAsync();
    }
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Bootstrap failed: " + string.Join(", ", result.Errors.Select(e => e.Code)));
    }
}
