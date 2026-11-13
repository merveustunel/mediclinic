using MediClinic.Application;
using MediClinic.Domain;
using MediClinic.Infrastructure;
using MediClinic.Infrastructure.Identity;
using MediClinic.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediClinic.Web;

/// <summary>Additive, explicitly enabled synthetic hospital fixture. No migrations or resets.</summary>
public static class RealisticDemoSeed
{
    public static Guid Id(string key) => DemoSeed.Id("hospital/v1/" + key);
    public static bool IsDemoUser(string id) => Enumerable.Range(0, 30).Select(i => "patient/" + i)
        .Concat(["staff/0", "staff/1", "admin"]).Any(key => Id("user/" + key).ToString() == id);

    private static readonly string[] Departments = ["Dahiliye", "Dermatoloji", "Kardiyoloji", "Nöroloji",
        "Ortopedi ve Travmatoloji", "Göz Hastalıkları", "Kulak Burun Boğaz", "Çocuk Sağlığı ve Hastalıkları"];
    private static readonly string[] DoctorNames = ["Selin Arısoy", "Emre Gökçen", "İdil Karaca", "Bora Erdem",
        "Defne Aksu", "Kerem Soylu", "Derya Ilgaz", "Ozan Sezer", "Eylül Taner", "Mert Akpınar",
        "İpek Yalın", "Alp Demirtaş", "Seda Günay", "Umut Eroğlu", "Aslı Sancak", "Cem Öztuna"];
    private static readonly string[] PatientNames = ["Ada Yelkenci", "Aras Deren", "Ela Somer", "Ege Tanyel",
        "Lina Akarsu", "Doruk İleri", "Zeynep Ural", "Kaan Bilgin", "Yağmur Esen", "Deniz Tümer",
        "Nehir Aydınel", "Baran Sevil", "Duru Onaran", "Efe Gündoğan", "Melis Sarıca", "Onur Tekiner",
        "Ceren Ulusoy", "Tolga İnce", "Başak Yücel", "Fırat Ergin", "Nalan Güneş", "Levent Alkan",
        "Sibel Ekinci", "Hakan Aydemir", "Gülay Yaman", "Orhan Tuncer", "Sevim Oral", "Turgut Yıldızel",
        "Ayten Soyer", "Metin Başaran"];
    private static readonly string[][] Services = [
        ["Dahiliye Muayenesi", "Metabolik Risk Değerlendirmesi", "Diyabet Takibi"],
        ["Dermatoloji Muayenesi", "Dermatoskopik İnceleme", "Akne Takibi"],
        ["Kardiyoloji Muayenesi", "EKG", "Ekokardiyografi"],
        ["Nöroloji Muayenesi", "Baş Ağrısı Değerlendirmesi", "Nörolojik Kontrol"],
        ["Ortopedi Muayenesi", "Eklem Değerlendirmesi", "Spor Yaralanması Kontrolü"],
        ["Göz Muayenesi", "Göz Tansiyonu Ölçümü", "Görme Keskinliği Değerlendirmesi"],
        ["KBB Muayenesi", "Odyometri", "Baş Dönmesi Değerlendirmesi"],
        ["Çocuk Sağlığı Muayenesi", "Sağlam Çocuk İzlemi", "Büyüme ve Gelişme Takibi"]];

    public static async Task RunAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Hospital demo requires Development.");
        if (!app.Configuration.GetValue<bool>("Demo:Enabled"))
            throw new InvalidOperationException("Hospital demo requires Demo:Enabled=true and --seed-realistic-demo.");
        var password = app.Configuration["Demo:PatientPassword"];
        var staffPassword = app.Configuration["Demo:StaffPassword"];
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(staffPassword))
            throw new InvalidOperationException("Demo:PatientPassword and Demo:StaffPassword are required.");
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediClinicDbContext>();
        if ((await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Apply existing migrations explicitly before seeding.");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        var today = DateOnly.FromDateTime(Scheduling.Local(now));
        await using var transaction = await db.Database.BeginTransactionAsync();
        var before = await Counts();
        var clinic = await db.Clinics.Include(c => c.Branches).ThenInclude(b => b.Departments).ThenInclude(d => d.Services)
            .SingleOrDefaultAsync(c => c.Id == DemoSeed.Id("clinic"));
        if (clinic == null) { clinic = new Clinic(DemoSeed.Id("clinic"), "MediClinic Demo Hastanesi"); db.Add(clinic); }
        // Reuse legacy branch identities without renaming or moving existing relationships.
        var branches = new[] { "central", "riverside" }.Select((key, i) =>
            clinic.Branches.SingleOrDefault(b => b.Id == DemoSeed.Id(key + "/branch")) ??
            clinic.AddBranch(DemoSeed.Id(key + "/branch"), i == 0 ? "MediClinic Merkez Hastanesi" : "MediClinic Serdivan Polikliniği",
                i == 0 ? "Sentetik Sağlık Caddesi No: 10, Adapazarı / Sakarya" : "Sentetik Bahar Sokak No: 20, Serdivan / Sakarya")).ToArray();
        var hours = await db.WorkingHours.ToListAsync();
        var availability = await db.StaffAvailabilities.Include(x => x.Staff).ThenInclude(x => x.Department).ThenInclude(x => x.Branch).ToListAsync();
        var appointments = await db.Appointments.Include(x => x.Patient).Include(x => x.Doctor)
            .Include(x => x.Service).ThenInclude(x => x.Department).ThenInclude(x => x.Branch).ToListAsync();
        foreach (var branch in branches)
        foreach (var day in Enum.GetValues<DayOfWeek>())
            if (!hours.Any(h => h.BranchId == branch.Id && h.Day == day))
            {
                var closed = day is DayOfWeek.Saturday or DayOfWeek.Sunday;
                var h = new WorkingHours(Id($"hours/{branch.Id}/{day}"), branch.Id, day, closed,
                    closed ? null : new TimeOnly(8, 0), closed ? null : new TimeOnly(18, 0));
                if (availability.Any(a => a.Staff.Department.BranchId == branch.Id && !AppointmentBooking.BranchOpen([h], a.StartsAt, a.EndsAt)) ||
                    appointments.Any(a => a.BranchId == branch.Id && !AppointmentBooking.BranchOpen([h], a.StartsAt, a.EndsAt)))
                    throw new InvalidOperationException("Default demo branch hours would invalidate an existing schedule. Configure compatible branch hours explicitly; no records changed.");
                hours.Add(h); db.Add(h);
            }
        var doctors = new List<Doctor>();
        var departments = new List<Department>();
        for (var d = 0; d < 8; d++)
        {
            var branch = branches[d % 2];
            var department = branch.Departments.SingleOrDefault(x => x.Id == Id("department/" + d))
                ?? branch.AddDepartment(Id("department/" + d), Departments[d]);
            departments.Add(department);
            for (var s = 0; s < 3; s++)
                if (!department.Services.Any(x => x.Id == Id($"service/{d}/{s}")))
                {
                    var service = department.AddService(Id($"service/{d}/{s}"), Services[d][s], TimeSpan.FromMinutes(s == 1 ? 15 : 30));
                    service.Update(service.Name, s == 1 ? 15 : 30, 650 + d * 110 + s * 75);
                }
            for (var n = 0; n < 2; n++)
            {
                var i = d * 2 + n;
                var doctor = await db.Doctors.Include(x => x.Services).SingleOrDefaultAsync(x => x.Id == Id("doctor/" + i));
                if (doctor == null)
                {
                    var name = DoctorNames[i].Split(' ');
                    doctor = new Doctor(Id("doctor/" + i), name[0], name[1], department, Departments[d]);
                    doctor.AssignServices(department.Services); db.Add(doctor);
                }
                if (doctor.DepartmentId != department.Id || doctor.Specialty != Departments[d] ||
                    department.Services.Any(s => !doctor.Offers(s)))
                    throw new InvalidOperationException($"Hospital demo doctor assignment changed: {i}. No records overwritten.");
                doctors.Add(doctor);
            }
        }
        var patients = new List<Patient>();
        for (var i = 0; i < 30; i++)
        {
            var patient = await db.Patients.FindAsync(Id("patient/" + i));
            if (patient == null)
            {
                var name = PatientNames[i].Split(' ');
                patient = new Patient(Id("patient/" + i), name[0], name[1], today.AddYears(-(2 + i * 3)).AddDays(-i));
                db.Add(patient);
            }
            // Non-dialable placeholder, deliberately not a plausible real subscriber number.
            var candidate = ApplicationUser.ForPatient($"hospital.patient.{i + 1:00}@mediclinic.test", patient);
            candidate.PhoneNumber = $"+90 000 000 {i + 1:0000}";
            await Account("patient/" + i, candidate, AuthRoles.Patient, password);
            patients.Add(patient);
        }
        for (var i = 0; i < 2; i++)
        {
            var staff = await db.Staff.FindAsync(Id("staff/" + i));
            if (staff == null) { staff = new Staff(Id("staff/" + i), i == 0 ? "Pelin" : "Eray", "Örnek", departments[i], "Hasta kabul görevlisi"); db.Add(staff); }
            await Account("staff/" + i, ApplicationUser.ForStaff($"hospital.staff.{i + 1}@mediclinic.test", staff), AuthRoles.Staff, staffPassword);
        }
        await Account("admin", ApplicationUser.Administrator("hospital.admin@mediclinic.test"), AuthRoles.ClinicAdmin, staffPassword);
        var adminId = Id("user/admin").ToString();
        if (!await db.ClinicAdministrators.AnyAsync(x => x.UserId == adminId && x.ClinicId == clinic.Id))
            db.Add(new ClinicAdministrator { UserId = adminId, ClinicId = clinic.Id, Clinic = clinic });
        await db.SaveChangesAsync();

        // Persist the date anchor through the first stable appointment rather than a new schema field.
        var first = await db.Appointments.SingleOrDefaultAsync(a => a.Id == Id("appointment/0"));
        var anchor = first == null ? today : DateOnly.FromDateTime(Scheduling.Local(first.StartsAt)).AddDays(14);
        // Appointment zero always lies on anchor-14, even on weekends (Saturday clinics stay closed).
        // Select an anchor whose anchor-14 is a weekday, including for weekend invocations.
        if (first == null) while (anchor.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) anchor = anchor.AddDays(-1);
        for (var i = 0; i < 60; i++)
        {
            if (appointments.Any(x => x.Id == Id("appointment/" + i))) continue;
            var doctor = doctors[i % 16];
            var service = doctor.Department.Services.Single(x => x.Id == Id($"service/{i % 16 / 2}/{i % 3}"));
            var date = anchor.AddDays(i < 24 ? -14 + i / 8 : i < 36 ? 0 : 1 + (i - 36) / 8);
            while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) date = date.AddDays(1);
            var status = i < 12 ? AppointmentStatus.Completed : i < 24 ? (i % 2 == 0 ? AppointmentStatus.Rejected : AppointmentStatus.Cancelled)
                : i % 2 == 0 ? AppointmentStatus.Pending : AppointmentStatus.Confirmed;
            // Young patients attend pediatrics; older patients attend adult specialties.
            var patient = patients[i % 16 >= 14 ? i % 5 : 6 + i % 24];
            Appointment? appointment = null;
            for (var attempt = 0; attempt < 45 && appointment == null; attempt++, date = date.AddDays(1))
            {
                var branchHours = hours.Where(h => h.BranchId == doctor.Department.BranchId).ToList();
                for (var minute = 9 * 60; minute < 17 * 60; minute += 30)
                {
                    if (minute >= 12 * 60 && minute < 13 * 60) continue;
                    var start = Scheduling.ToUtc(date.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minute))));
                    var end = start.Add(service.Duration);
                    if (status == AppointmentStatus.Completed && end > now) continue;
                    if (!AppointmentBooking.BranchOpen(branchHours, start, end)) continue;
                    if (appointments.Any(a => (a.DoctorId == doctor.Id || a.PatientId == patient.Id) && a.StartsAt < end && start < a.EndsAt)) continue;
                    var intervals = availability.Where(a => a.StaffId == doctor.Id).ToList();
                    if (!intervals.Any(a => a.Covers(start, end)))
                    {
                        var shiftStart = Scheduling.ToUtc(date.ToDateTime(new TimeOnly(minute < 12 * 60 ? 9 : 13, 0)));
                        var shiftEnd = Scheduling.ToUtc(date.ToDateTime(new TimeOnly(minute < 12 * 60 ? 12 : 17, 0)));
                        if (!AppointmentBooking.BranchOpen(branchHours, shiftStart, shiftEnd) || intervals.Any(a => a.StartsAt < shiftEnd && shiftStart < a.EndsAt)) continue;
                        var interval = new StaffAvailability(Id($"availability/{doctor.Id}/{date:yyyy-MM-dd}"), doctor, shiftStart, shiftEnd);
                        db.Add(interval); availability.Add(interval);
                    }
                    appointment = new Appointment(Id("appointment/" + i), patient, doctor, service, start);
                    if (status is AppointmentStatus.Confirmed or AppointmentStatus.Completed) appointment.Confirm();
                    if (status == AppointmentStatus.Completed) appointment.Complete(now);
                    if (status == AppointmentStatus.Rejected) appointment.Reject("Sentetik senaryo: farklı değerlendirme planlandı.");
                    if (status == AppointmentStatus.Cancelled) appointment.Cancel("Sentetik senaryo: hasta plan değişikliği.");
                    db.Add(appointment); appointments.Add(appointment); break;
                }
            }
            if (appointment == null) throw new InvalidOperationException($"No safe hospital demo slot for appointment {i}; transaction rolled back.");
        }
        // Future weekday sessions make all 16 doctors bookable, with a lunch break.
        foreach (var doctor in doctors)
        for (var day = 1; day <= 14; day++)
        foreach (var (from, to) in new[] { (9, 12), (13, 17) })
        {
            var date = today.AddDays(day);
            var start = Scheduling.ToUtc(date.ToDateTime(new TimeOnly(from, 0)));
            var end = Scheduling.ToUtc(date.ToDateTime(new TimeOnly(to, 0)));
            if (!AppointmentBooking.BranchOpen(hours.Where(h => h.BranchId == doctor.Department.BranchId).ToList(), start, end) ||
                availability.Any(a => a.StaffId == doctor.Id && a.StartsAt < end && start < a.EndsAt)) continue;
            var interval = new StaffAvailability(Id($"session/{doctor.Id}/{date:yyyy-MM-dd}/{from}"), doctor, start, end);
            db.Add(interval); availability.Add(interval);
        }
        await db.SaveChangesAsync();
        var after = await Counts();
        var states = await db.Appointments.GroupBy(a => a.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync();
        await transaction.CommitAsync();
        app.Logger.LogInformation("Synthetic hospital demo committed. Counts before [{Before}], after [{After}] (clinics, branches, departments, doctors, patients, services, appointments).", string.Join(",", before), string.Join(",", after));
        foreach (var state in states) app.Logger.LogInformation("Appointment status {Status}: {Count}", state.Status, state.Count);

        async Task<int[]> Counts() => [await db.Clinics.CountAsync(), await db.Branches.CountAsync(), await db.Departments.CountAsync(),
            await db.Doctors.CountAsync(), await db.Patients.CountAsync(), await db.MedicalServices.CountAsync(), await db.Appointments.CountAsync()];

        async Task Account(string key, ApplicationUser candidate, string role, string secret)
        {
            candidate.Id = Id("user/" + key).ToString();
            var existing = await users.FindByEmailAsync(candidate.Email!);
            if (existing != null)
            {
                var assigned = await users.GetRolesAsync(existing);
                if (existing.Id != candidate.Id || existing.PatientId != candidate.PatientId || existing.StaffId != candidate.StaffId || assigned.Count != 1 || assigned[0] != role)
                    throw new InvalidOperationException("Hospital demo account collision or changed role/link; no account overwritten.");
                return;
            }
            Check(await users.CreateAsync(candidate, secret));
            if (!await roles.RoleExistsAsync(role)) Check(await roles.CreateAsync(new IdentityRole(role)));
            Check(await users.AddToRoleAsync(candidate, role));
        }
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Hospital demo identity failure: " + string.Join(", ", result.Errors.Select(e => e.Code)));
    }
}
