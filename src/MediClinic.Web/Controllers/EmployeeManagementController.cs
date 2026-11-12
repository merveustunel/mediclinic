using System.Security.Claims;
using MediClinic.Application;
using MediClinic.Domain;
using MediClinic.Infrastructure.Identity;
using MediClinic.Infrastructure.Persistence;
using MediClinic.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediClinic.Web.Controllers;

[Authorize(Roles = AuthRoles.ClinicAdmin)]
[AutoValidateAntiforgeryToken]
public sealed class EmployeeManagementController(MediClinicDbContext db, UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles, TimeProvider clock) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IQueryable<Department> Departments => db.Departments.Where(d => db.ClinicAdministrators.Any(a =>
        a.UserId == UserId && a.ClinicId == EF.Property<Guid>(d.Branch, "ClinicKey")));
    private IQueryable<Staff> Employees => db.Staff.Where(s => Departments.Any(d => d.Id == EF.Property<Guid>(s, "DepartmentKey")));
    private IQueryable<Doctor> Doctors => Employees.OfType<Doctor>();
    private static bool Active(Appointment a) => a.Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed;
    private async Task<List<Appointment>> Appointments(Guid id) => await db.Appointments
        .Include(a => a.Service).Where(a => EF.Property<Guid>(a, "DoctorKey") == id).ToListAsync();
    private async Task Populate(EmployeeEditModel model)
    {
        model.Departments = await Departments.Include(d => d.Branch).ThenInclude(b => b.Clinic).OrderBy(d => d.Name).ToListAsync();
        model.Services = await db.MedicalServices.Where(s => Departments.Any(d => d.Id == EF.Property<Guid>(s, "DepartmentKey")))
            .Include(s => s.Department).OrderBy(s => s.Name).ToListAsync();
        model.HasAccount = model.Id.HasValue && await db.Users.AnyAsync(u => u.StaffId == model.Id);
    }
    public async Task<IActionResult> Index()
    {
        var employees = await Employees.Include(s => s.Department).ThenInclude(d => d.Branch).ThenInclude(b => b.Clinic)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToListAsync();
        var ids = employees.Select(e => e.Id).ToList();
        var accounts = await db.Users.Where(u => u.StaffId.HasValue && ids.Contains(u.StaffId.Value)).ToListAsync();
        return View(new EmployeeListModel(employees, accounts.ToDictionary(u => u.StaffId!.Value, u => u.Email ?? "")));
    }
    public async Task<IActionResult> Edit(Guid? id)
    {
        var model = new EmployeeEditModel();
        if (id.HasValue)
        {
            var employee = await Employees.Include(s => s.Department).SingleOrDefaultAsync(s => s.Id == id);
            if (employee is null) return NotFound();
            model.Id = employee.Id; model.FirstName = employee.FirstName; model.LastName = employee.LastName;
            model.DepartmentId = employee.DepartmentId; model.JobTitle = employee.JobTitle;
            if (employee is Doctor doctor)
            {
                await db.Entry(doctor).Collection(d => d.Services).LoadAsync();
                model.IsDoctor = true; model.Specialty = doctor.Specialty; model.ServiceIds = doctor.Services.Select(s => s.Id).ToList();
            }
        }
        await Populate(model);
        if (model.Departments.Count == 0) return Forbid();
        return View(model);
    }
    [HttpPost]
    public async Task<IActionResult> Edit(EmployeeEditModel model)
    {
        // Credentials must never be echoed through ModelState, HTML or TempData.
        ModelState.Remove(nameof(model.InitialPassword));
        await using var transaction = await db.Database.BeginTransactionAsync();
        var employee = model.Id.HasValue ? await Employees.Include(s => s.Department).SingleOrDefaultAsync(s => s.Id == model.Id) : null;
        if (model.Id.HasValue && employee is null) return NotFound();
        var department = await Departments.Include(d => d.Branch).SingleOrDefaultAsync(d => d.Id == model.DepartmentId);
        if (department is null) return NotFound();
        if (employee is not null) model.IsDoctor = employee is Doctor;
        if (model.IsDoctor && string.IsNullOrWhiteSpace(model.Specialty)) ModelState.AddModelError(nameof(model.Specialty), "Uzmanlık zorunludur.");
        if (!model.IsDoctor && (string.IsNullOrWhiteSpace(model.JobTitle) || model.JobTitle.Trim() is "Doctor" or "ClinicAdmin" or "Patient"))
            ModelState.AddModelError(nameof(model.JobTitle), "Geçerli bir personel görevi giriniz.");
        var services = await db.MedicalServices.Where(s => model.ServiceIds.Contains(s.Id)).Include(s => s.Department).ToListAsync();
        if ((!model.IsDoctor && model.ServiceIds.Count > 0) || services.Count != model.ServiceIds.Distinct().Count() || services.Any(s => s.DepartmentId != department.Id))
            ModelState.AddModelError(nameof(model.ServiceIds), "Yalnızca seçilen bölüme ait hizmetler atanabilir.");
        var names = await Employees.Where(s => s.Id != model.Id && EF.Property<Guid>(s, "DepartmentKey") == department.Id).ToListAsync();
        if (names.Any(s => string.Equals(s.FirstName, model.FirstName?.Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals(s.LastName, model.LastName?.Trim(), StringComparison.OrdinalIgnoreCase)))
            ModelState.AddModelError("", "Bu bölümde aynı ad ve soyad ile bir çalışan kayıtlı.");
        if (employee is not null && employee.DepartmentId != department.Id)
        {
            // Historical appointments and staff dashboard access depend on department membership.
            if ((await Appointments(employee.Id)).Count > 0 || await db.StaffAvailabilities.AnyAsync(a => EF.Property<Guid>(a, "StaffKey") == employee.Id))
                ModelState.AddModelError(nameof(model.DepartmentId), "Randevusu veya uygunluk kaydı olan çalışan başka bölüme taşınamaz.");
        }
        if (employee is Doctor && (await Appointments(employee.Id)).Where(Active).Any(a => !model.ServiceIds.Contains(a.Service.Id)))
            ModelState.AddModelError(nameof(model.ServiceIds), "Aktif randevuların hizmet atamaları kaldırılamaz.");
        var hasAccount = employee is not null && await db.Users.AnyAsync(u => u.StaffId == employee.Id);
        var provision = !hasAccount && !string.IsNullOrWhiteSpace(model.Email);
        if (provision && string.IsNullOrWhiteSpace(model.InitialPassword)) ModelState.AddModelError("", "Hesap oluşturmak için ilk parola zorunludur.");
        if (!provision && !string.IsNullOrEmpty(model.InitialPassword)) ModelState.AddModelError("", "Parola yalnızca yeni hesap oluştururken kullanılabilir.");
        if (!ModelState.IsValid) return await InvalidEmployee(model);
        if (employee is null)
        {
            employee = model.IsDoctor ? new Doctor(Guid.NewGuid(), model.FirstName!, model.LastName!, department, model.Specialty!)
                : new Staff(Guid.NewGuid(), model.FirstName!, model.LastName!, department, model.JobTitle!);
            db.Staff.Add(employee);
        }
        else
        {
            var movingDoctor = employee is Doctor && employee.DepartmentId != department.Id;
            if (movingDoctor)
            {
                var transferring = (Doctor)employee;
                await db.Entry(transferring).Collection(d => d.Services).LoadAsync();
                transferring.AssignServices([]);
            }
            employee.Rename(model.FirstName!, model.LastName!);
            employee.UpdateEmployment(department, model.JobTitle ?? "Doctor");
            // Publish the new department inside this transaction before inserting
            // replacement assignments, which the database compatibility trigger checks.
            if (movingDoctor) await db.SaveChangesAsync();
        }
        if (employee is Doctor doctor)
        {
            await db.Entry(doctor).Collection(d => d.Services).LoadAsync();
            doctor.UpdateSpecialty(model.Specialty!); doctor.AssignServices(services);
        }
        if (provision)
        {
            if (!await roles.RoleExistsAsync(AuthRoles.Staff))
            {
                var roleResult = await roles.CreateAsync(new IdentityRole(AuthRoles.Staff));
                if (!roleResult.Succeeded) throw new InvalidOperationException("Personel rolü oluşturulamadı.");
            }
            var account = ApplicationUser.ForStaff(model.Email!.Trim(), employee);
            var result = await users.CreateAsync(account, model.InitialPassword!);
            if (result.Succeeded) result = await users.AddToRoleAsync(account, AuthRoles.Staff);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync();
                db.ChangeTracker.Clear();
                ModelState.AddModelError("", "Hesap oluşturulamadı. E-posta benzersiz olmalı; parola en az 12 karakter, büyük/küçük harf, rakam ve özel karakter içermelidir.");
                return await InvalidEmployee(model);
            }
        }
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        TempData["EmployeeSuccess"] = "Çalışan bilgileri kaydedildi.";
        return RedirectToAction(nameof(Index));
    }
    private async Task<IActionResult> InvalidEmployee(EmployeeEditModel model)
    {
        model.InitialPassword = null; await Populate(model); return View("Edit", model);
    }
    public async Task<IActionResult> Availability(Guid doctorId, DateOnly? date)
    {
        var doctor = await Doctors.SingleOrDefaultAsync(d => d.Id == doctorId);
        if (doctor is null) return NotFound();
        var query = db.StaffAvailabilities.Where(a => EF.Property<Guid>(a, "StaffKey") == doctorId);
        if (date.HasValue)
        {
            var start = Scheduling.ToUtc(date.Value.ToDateTime(TimeOnly.MinValue));
            var end = start.AddDays(1);
            query = query.Where(a => a.StartsAt < end && a.EndsAt > start);
        }
        return View(new AvailabilityListModel(doctor, date, await query.OrderBy(a => a.StartsAt).ToListAsync()));
    }
    public async Task<IActionResult> Interval(Guid doctorId, Guid? id)
    {
        var doctor = await Doctors.SingleOrDefaultAsync(d => d.Id == doctorId);
        if (doctor is null) return NotFound();
        var model = new AvailabilityEditModel { DoctorId = doctorId, DoctorName = doctor.FullName };
        if (id.HasValue)
        {
            var interval = await db.StaffAvailabilities.SingleOrDefaultAsync(a => a.Id == id && EF.Property<Guid>(a, "StaffKey") == doctorId);
            if (interval is null) return NotFound();
            if (interval.StartsAt <= clock.GetUtcNow()) return BadRequest("Yalnızca gelecek uygunluklar düzenlenebilir.");
            model.Id = id; model.StartsAt = Scheduling.Local(interval.StartsAt); model.EndsAt = Scheduling.Local(interval.EndsAt);
        }
        return View(model);
    }
    [HttpPost]
    public async Task<IActionResult> Interval(AvailabilityEditModel model)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var doctor = await Doctors.SingleOrDefaultAsync(d => d.Id == model.DoctorId);
        if (doctor is null) return NotFound();
        model.DoctorName = doctor.FullName;
        var interval = model.Id.HasValue ? await db.StaffAvailabilities.SingleOrDefaultAsync(a => a.Id == model.Id && EF.Property<Guid>(a, "StaffKey") == doctor.Id) : null;
        if (model.Id.HasValue && interval is null) return NotFound();
        if (interval is not null && interval.StartsAt <= clock.GetUtcNow()) return BadRequest("Yalnızca gelecek uygunluklar düzenlenebilir.");
        if (!ModelState.IsValid) return View(model);
        DateTimeOffset start, end;
        try { start = Scheduling.ToUtc(model.StartsAt!.Value); end = Scheduling.ToUtc(model.EndsAt!.Value); }
        catch (BookingException) { ModelState.AddModelError("", "Geçerli İstanbul saatleri giriniz."); return View(model); }
        if (start <= clock.GetUtcNow() || end <= start) ModelState.AddModelError("", "Başlangıç gelecekte, bitiş başlangıçtan sonra olmalıdır.");
        if (await db.StaffAvailabilities.AnyAsync(a => EF.Property<Guid>(a, "StaffKey") == doctor.Id && a.Id != model.Id && a.StartsAt < end && a.EndsAt > start))
            ModelState.AddModelError("", "Uygunluk aralığı mevcut bir kayıtla çakışıyor.");
        if (interval is not null && !await PreservesAppointments(doctor.Id, interval.Id, start, end))
            ModelState.AddModelError("", "Bu değişiklik mevcut aktif randevuların uygunluğunu bozuyor.");
        if (!ModelState.IsValid) return View(model);
        if (interval is null) db.StaffAvailabilities.Add(new StaffAvailability(Guid.NewGuid(), doctor, start, end));
        else interval.Reschedule(start, end);
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        TempData["EmployeeSuccess"] = "Uygunluk kaydedildi.";
        return RedirectToAction(nameof(Availability), new { doctorId = doctor.Id });
    }
    private async Task<bool> PreservesAppointments(Guid doctorId, Guid excluded, DateTimeOffset? start, DateTimeOffset? end)
    {
        var remaining = await db.StaffAvailabilities.Where(a => EF.Property<Guid>(a, "StaffKey") == doctorId && a.Id != excluded).ToListAsync();
        return (await Appointments(doctorId)).Where(Active).All(a =>
            (start.HasValue && start <= a.StartsAt && end >= a.EndsAt) || remaining.Any(i => i.Covers(a.StartsAt, a.EndsAt)));
    }
    [HttpPost]
    public async Task<IActionResult> RemoveInterval(Guid doctorId, Guid id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await Doctors.AnyAsync(d => d.Id == doctorId)) return NotFound();
        var interval = await db.StaffAvailabilities.SingleOrDefaultAsync(a => a.Id == id && EF.Property<Guid>(a, "StaffKey") == doctorId);
        if (interval is null) return NotFound();
        if (interval.StartsAt <= clock.GetUtcNow() || !await PreservesAppointments(doctorId, id, null, null))
        { TempData["EmployeeError"] = "Geçmiş uygunluklar ve aktif randevuları koruyan aralıklar silinemez."; }
        else { db.Remove(interval); await db.SaveChangesAsync(); await transaction.CommitAsync(); TempData["EmployeeSuccess"] = "Uygunluk silindi."; }
        return RedirectToAction(nameof(Availability), new { doctorId });
    }
}
