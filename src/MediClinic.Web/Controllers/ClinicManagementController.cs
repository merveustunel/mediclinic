using System.Security.Claims;
using MediClinic.Infrastructure;
using MediClinic.Domain;
using MediClinic.Infrastructure.Identity;
using MediClinic.Infrastructure.Persistence;
using MediClinic.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediClinic.Web.Controllers;

[Authorize(Roles = AuthRoles.ClinicAdmin)]
[AutoValidateAntiforgeryToken]
public sealed class ClinicManagementController(MediClinicDbContext db) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private IQueryable<Clinic> Scope => db.Clinics.Where(c => db.ClinicAdministrators.Any(a => a.UserId == UserId && a.ClinicId == c.Id));
    private Task<List<Clinic>> Load() => Scope.Include(c => c.Branches).ThenInclude(b => b.Departments).ThenInclude(d => d.Services).ToListAsync();
    public async Task<IActionResult> Index(Guid? clinicId, Guid? branchId, Guid? departmentId)
    {
        var clinics = await Load();
        if (clinicId.HasValue && !clinics.Any(c => c.Id == clinicId)) return NotFound();
        var branches = clinics.SelectMany(c => c.Branches).Where(b => !clinicId.HasValue || b.ClinicId == clinicId).ToList();
        if (branchId.HasValue && !branches.Any(b => b.Id == branchId)) return NotFound();
        if (departmentId.HasValue && !branches.Where(b => !branchId.HasValue || b.Id == branchId).SelectMany(b => b.Departments).Any(d => d.Id == departmentId)) return NotFound();
        var ids = branches.Select(b => b.Id).ToList();
        return View(new ClinicManagementModel { Clinics = clinics, ClinicId = clinicId, BranchId = branchId, DepartmentId = departmentId, Hours = await db.WorkingHours.Where(h => ids.Contains(h.BranchId)).ToListAsync() });
    }
    private static bool ValidKind(string kind) => kind is "Clinic" or "Branch" or "Department" or "Service";
    private static IEnumerable<Entity> Records(List<Clinic> clinics, string kind) => kind switch
    {
        "Clinic" => clinics,
        "Branch" => clinics.SelectMany(c => c.Branches),
        "Department" => clinics.SelectMany(c => c.Branches).SelectMany(b => b.Departments),
        _ => clinics.SelectMany(c => c.Branches).SelectMany(b => b.Departments).SelectMany(d => d.Services)
    };
    public async Task<IActionResult> Edit(string kind = "Clinic", Guid? id = null, Guid? parentId = null)
    {
        if (!ValidKind(kind)) return NotFound();
        var clinics = await Load();
        if (clinics.Count == 0) return Forbid();
        var model = new CatalogEditModel { Kind = kind, Id = id, ParentId = parentId };
        if (id.HasValue)
        {
            var entity = Records(clinics, kind).SingleOrDefault(e => e.Id == id);
            if (entity is null) return NotFound();
            switch (entity)
            {
                case Clinic c: model.Name = c.Name; break;
                case Branch b: model.Name = b.Name; model.Address = b.Address; model.ParentId = b.ClinicId; break;
                case Department d: model.Name = d.Name; model.ParentId = d.BranchId; break;
                case MedicalService s: model.Name = s.Name; model.ParentId = s.DepartmentId; model.DurationMinutes = (int)s.Duration.TotalMinutes; model.Price = s.Price; break;
            }
        }
        else if (kind != "Clinic" && !Records(clinics, kind == "Branch" ? "Clinic" : kind == "Department" ? "Branch" : "Department").Any(e => e.Id == parentId)) return NotFound();
        return View(model);
    }
    [HttpPost]
    public async Task<IActionResult> Edit(CatalogEditModel model)
    {
        if (!ValidKind(model.Kind)) return NotFound();
        var clinics = await Load();
        if (clinics.Count == 0) return Forbid();
        var entity = model.Id.HasValue ? Records(clinics, model.Kind).SingleOrDefault(e => e.Id == model.Id) : null;
        if (model.Id.HasValue && entity is null) return NotFound();
        // Parent is always derived from stored data on edit, never trusted from the submitted form.
        var parentId = entity switch { Branch b => b.ClinicId, Department d => d.BranchId, MedicalService s => s.DepartmentId, _ => model.ParentId };
        var parent = model.Kind == "Clinic" ? null : Records(clinics, model.Kind == "Branch" ? "Clinic" : model.Kind == "Department" ? "Branch" : "Department").SingleOrDefault(e => e.Id == parentId);
        if (model.Kind != "Clinic" && parent is null) return NotFound();
        model.ParentId = parentId;
        if (model.Kind == "Branch" && string.IsNullOrWhiteSpace(model.Address)) ModelState.AddModelError("Address", "Adres zorunludur.");
        if (model.Kind == "Service" && decimal.Round(model.Price, 2) != model.Price) ModelState.AddModelError("Price", "Ücret en fazla iki ondalık basamak içermelidir.");
        if (!ModelState.IsValid) return View(model);
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Include all names only for uniqueness; never expose out-of-scope records.
        var names = model.Kind switch
        {
            "Clinic" => await db.Clinics.Where(x => x.Id != model.Id).Select(x => x.Name).ToListAsync(),
            "Branch" => await db.Branches.Where(x => EF.Property<Guid>(x, "ClinicKey") == parentId && x.Id != model.Id).Select(x => x.Name).ToListAsync(),
            "Department" => await db.Departments.Where(x => EF.Property<Guid>(x, "BranchKey") == parentId && x.Id != model.Id).Select(x => x.Name).ToListAsync(),
            _ => await db.MedicalServices.Where(x => EF.Property<Guid>(x, "DepartmentKey") == parentId && x.Id != model.Id).Select(x => x.Name).ToListAsync()
        };
        if (names.Any(n => string.Equals(n.Trim(), model.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        { ModelState.AddModelError("Name", "Bu ad zaten kullanılıyor."); return View(model); }
        switch (model.Kind)
        {
            case "Clinic":
                if (entity is Clinic c) c.Rename(model.Name);
                else { var created = new Clinic(Guid.NewGuid(), model.Name); db.Clinics.Add(created); db.ClinicAdministrators.Add(new() { UserId = UserId, Clinic = created, ClinicId = created.Id }); }
                break;
            case "Branch":
                if (entity is Branch b) b.Update(model.Name, model.Address!);
                else db.Branches.Add(((Clinic)parent!).AddBranch(Guid.NewGuid(), model.Name, model.Address!));
                break;
            case "Department":
                if (entity is Department d) d.Rename(model.Name);
                else db.Departments.Add(((Branch)parent!).AddDepartment(Guid.NewGuid(), model.Name));
                break;
            case "Service":
                var service = entity as MedicalService ?? ((Department)parent!).AddService(Guid.NewGuid(), model.Name, TimeSpan.FromMinutes(model.DurationMinutes));
                service.Update(model.Name, model.DurationMinutes, model.Price);
                if (entity is null) db.MedicalServices.Add(service);
                break;
        }
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        TempData["ManagementSuccess"] = "Bilgiler kaydedildi.";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> WeeklyHours(WeeklyHoursModel model)
    {
        if (!await db.Branches.AnyAsync(b => b.Id == model.BranchId && Scope.Any(c => c.Id == EF.Property<Guid>(b, "ClinicKey")))) return NotFound();
        if (!ModelState.IsValid || model.Days.Count != 7 || model.Days.Select(d => d.Day).Distinct().Count() != 7 || model.Days.Any(d => !Enum.IsDefined(d.Day)))
            return BadRequest(new { message = "Haftanın yedi gününü geçerli saatlerle gönderiniz." });
        List<WorkingHours> proposed;
        try
        {
            proposed = model.Days.Select(d => new WorkingHours(Guid.NewGuid(), model.BranchId, d.Day, d.Closed, d.Opens, d.Closes)).ToList();
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "Açık günlerde açılış ve kapanış saatlerini giriniz; açılış kapanıştan önce olmalıdır." });
        }
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var active = await db.Appointments.Where(a => EF.Property<Guid>(a.Service.Department, "BranchKey") == model.BranchId &&
                (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed)).ToListAsync();
            if (active.Any(a => !AppointmentBooking.BranchOpen(proposed, a.StartsAt, a.EndsAt)))
                return Conflict(new { message = "Bu program aktif bir randevuyla çakışıyor. Saatleri kontrol ediniz." });
            var existing = await db.WorkingHours.Where(h => h.BranchId == model.BranchId).ToListAsync();
            foreach (var day in proposed)
            {
                var hours = existing.SingleOrDefault(h => h.Day == day.Day);
                if (hours is null) db.WorkingHours.Add(day);
                else hours.Update(day.Closed, day.Opens, day.Closes);
            }
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { message = "Haftalık çalışma saatleri kaydedildi." });
        }
        catch (Exception error) when (error is DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
        {
            return StatusCode(500, new { message = "Çalışma saatleri kaydedilemedi. Değerleriniz korunuyor; lütfen tekrar deneyiniz." });
        }
    }
    [HttpPost]
    public async Task<IActionResult> Hours(Guid branchId, DayOfWeek day, bool closed, TimeOnly? opens, TimeOnly? closes)
    {
        if (!await db.Branches.AnyAsync(b => b.Id == branchId && Scope.Any(c => c.Id == EF.Property<Guid>(b, "ClinicKey")))) return NotFound();
        if (!ModelState.IsValid || !Enum.IsDefined(day)) return BadRequest("Geçersiz gün veya saat.");
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var proposed = new WorkingHours(Guid.NewGuid(), branchId, day, closed, opens, closes);
            var active = await db.Appointments.Where(a => EF.Property<Guid>(a.Service.Department, "BranchKey") == branchId &&
                (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed)).ToListAsync();
            if (active.Any(a => !AppointmentBooking.BranchOpen(new[] { proposed }, a.StartsAt, a.EndsAt)))
            { TempData["ManagementError"] = "Bu saatler aktif bir randevuyla uyumlu değil."; return RedirectToAction(nameof(Index), new { branchId }); }
            var hours = await db.WorkingHours.SingleOrDefaultAsync(h => h.BranchId == branchId && h.Day == day);
            if (hours is null) db.WorkingHours.Add(new WorkingHours(Guid.NewGuid(), branchId, day, closed, opens, closes));
            else hours.Update(closed, opens, closes);
            await db.SaveChangesAsync(); await transaction.CommitAsync(); TempData["ManagementSuccess"] = "çalışma saatleri kaydedildi.";
        }
        catch (ArgumentException) { TempData["ManagementError"] = "Açılış ve kapanış saatlerini kontrol ediniz; açılış kapanıştan önce olmalıdır."; }
        return RedirectToAction(nameof(Index), new { branchId });
    }
}
