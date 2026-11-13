using System.Security.Cryptography;
using System.Text;
using MediClinic.Application;
using MediClinic.Domain;
using MediClinic.Infrastructure.Identity;
using MediClinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediClinic.Web;

/// <summary>Explicit, additive development tooling. Never called during normal startup.</summary>
public static class DemoSeed
{
    public static Guid Id(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes("MediClinic.demo.v1/" + key))[..16]);
    public static bool IsDemoUser(string id) => new[] { "patient", "staff-central", "staff-riverside" }
        .Any(key => id == Id("user/" + key).ToString());

    public static async Task RunAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Demo seeding requires Development.");
        var patientPassword = app.Configuration["Demo:PatientPassword"];
        var staffPassword = app.Configuration["Demo:StaffPassword"];
        if (string.IsNullOrWhiteSpace(patientPassword) || string.IsNullOrWhiteSpace(staffPassword))
            throw new InvalidOperationException("Demo:PatientPassword and Demo:StaffPassword are required.");
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediClinicDbContext>();
        if ((await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Apply existing migrations explicitly before seeding.");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var clinic = await db.Clinics.Include(x => x.Branches).ThenInclude(x => x.Departments).ThenInclude(x => x.Services)
            .SingleOrDefaultAsync(x => x.Id == Id("clinic"));
        if (clinic == null)
        {
            clinic = new Clinic(Id("clinic"), "MediClinic Demo");
            db.Add(clinic);
        }
        var doctors = new List<Doctor>();
        foreach (var (key, branchName, departmentName, names) in new[]
        {
            ("central", "Merkez", "Genel Tıp", new[] { "Deniz", "Ece" }),
            ("riverside", "Sahil", "Kardiyoloji", new[] { "Can" })
        })
        {
            var branch = clinic.Branches.SingleOrDefault(x => x.Id == Id(key + "/branch"))
                ?? clinic.AddBranch(Id(key + "/branch"), branchName, branchName + " Örnek Sokak, İstanbul");
            var department = branch.Departments.SingleOrDefault(x => x.Id == Id(key + "/department"))
                ?? branch.AddDepartment(Id(key + "/department"), departmentName);
            if (!department.Services.Any(x => x.Id == Id(key + "/service")))
                department.AddService(Id(key + "/service"), departmentName + " muayenesi", TimeSpan.FromMinutes(30));
            foreach (var name in names)
            {
                var doctor = await db.Doctors.FindAsync(Id("doctor/" + name));
                if (doctor == null)
                {
                    doctor = new Doctor(Id("doctor/" + name), name, "Demo", department, departmentName);
                    doctor.AssignServices(department.Services);
                    db.Add(doctor);
                }
                doctors.Add(doctor);
            }
            var staff = await db.Staff.FindAsync(Id(key + "/staff"));
            if (staff == null)
            {
                staff = new Staff(Id(key + "/staff"), branchName, "Demo Görevlisi", department, "Danışma görevlisi");
                db.Add(staff);
            }
            await Account("staff-" + key, ApplicationUser.ForStaff($"demo.staff.{key}@example.test", staff), AuthRoles.Staff, staffPassword);
        }
        var patient = await db.Patients.FindAsync(Id("patient"));
        if (patient == null)
        {
            patient = new Patient(Id("patient"), "Ayşe", "Demo", new DateOnly(1990, 1, 1));
            db.Add(patient);
        }
        await Account("patient", ApplicationUser.ForPatient("demo.patient@example.test", patient), AuthRoles.Patient, patientPassword);
        var today = DateOnly.FromDateTime(Scheduling.Local(scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow()));
        foreach (var doctor in doctors)
        for (var offset = 1; offset <= 14; offset++)
        {
            var date = today.AddDays(offset);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            var id = Id($"availability/{doctor.Id}/{date:yyyy-MM-dd}");
            if (!await db.StaffAvailabilities.AnyAsync(x => x.Id == id))
                db.Add(new StaffAvailability(id, doctor, Scheduling.ToUtc(date.ToDateTime(new TimeOnly(9, 0))),
                    Scheduling.ToUtc(date.ToDateTime(new TimeOnly(17, 0)))));
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        app.Logger.LogInformation("Demo setup complete: upcoming weekday availability added. Existing credentials and appointments preserved.");

        async Task Account(string key, ApplicationUser candidate, string role, string password)
        {
            candidate.Id = Id("user/" + key).ToString();
            var existing = await users.FindByEmailAsync(candidate.Email!);
            if (existing != null)
            {
                var assigned = await users.GetRolesAsync(existing);
                if (existing.Id != candidate.Id || existing.PatientId != candidate.PatientId || existing.StaffId != candidate.StaffId
                    || assigned.Count != 1 || assigned[0] != role)
                    throw new InvalidOperationException("Demo account collision or unexpected role/link. No existing account will be modified.");
                return;
            }
            Check(await users.CreateAsync(candidate, password));
            if (!await roles.RoleExistsAsync(role)) Check(await roles.CreateAsync(new IdentityRole(role)));
            Check(await users.AddToRoleAsync(candidate, role));
        }
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Demo setup failed: " + string.Join(", ", result.Errors.Select(x => x.Code)));
    }
}
