using System.Security.Claims;
using MediClinic.Application;
using MediClinic.Infrastructure.Identity;
using MediClinic.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediClinic.Web.Controllers;

[Authorize(Policy = AuthRoles.Patient)]
public sealed class AppointmentsController(IAppointmentBooking booking, TimeProvider clock) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> Index(string? status)
    {
        try
        {
            var appointments = await booking.List(UserId, false);
            if (!PatientAppointmentsModel.IsValidFilter(status))
                return BadRequest("Geçersiz randevu durumu.");
            return View("PatientList", PatientAppointmentsModel.Create(appointments, status));
        }
        catch (BookingException) { return Forbid(); }
    }
    public async Task<IActionResult> Details(Guid id)
    {
        try { var item = await booking.Find(UserId, id, false); return item == null ? NotFound() : View(item); }
        catch (BookingException) { return Forbid(); }
    }
    [HttpGet]
    public async Task<IActionResult> Book([Bind("ClinicId,BranchId,DepartmentId,DoctorId,ServiceId,Date,LocalStart,Stage")] BookingModel model)
    {
        ModelState.Remove(nameof(model.LocalStart));
        if (!Request.Query.ContainsKey(nameof(model.Stage)) && model.ServiceId != Guid.Empty)
            model.Stage = 2; // Preserve existing fully populated booking links.
        await PrepareBooking(model);
        return View(model);
    }

    private async Task PrepareBooking(BookingModel model)
    {
        var today = DateOnly.FromDateTime(Scheduling.Local(clock.GetUtcNow()));
        var hasDate = Request.HasFormContentType
            ? Request.Form.ContainsKey(nameof(model.Date)) : Request.Query.ContainsKey(nameof(model.Date));
        if (!hasDate) model.Date = today.AddDays(1);
        // Never render framework binding errors (which can contain raw submitted values).
        if (!ModelState.IsValid)
        {
            ModelState.Clear();
            ModelState.AddModelError("", "Seçimleri ve tarih bilgisini kontrol edin.");
        }
        try
        {
            model.Options = await booking.Options(UserId);
            var candidates = model.Options;
            var ids = new[] { model.ClinicId, model.BranchId, model.DepartmentId, model.DoctorId, model.ServiceId };
            Func<BookingOption, Guid>[] keys = [x => x.ClinicId, x => x.BranchId, x => x.DepartmentId, x => x.DoctorId, x => x.ServiceId];
            for (var i = 0; i < ids.Length; i++)
            {
                candidates = candidates.Where(x => keys[i](x) == ids[i]).ToArray();
                if (ids[i] == Guid.Empty || candidates.Count == 0)
                {
                    if (ids[i] != Guid.Empty) ModelState.AddModelError("", "Seçenekler değişti veya seçim geçersiz. Lütfen seçimlerinizi güncelleyin.");
                    for (var j = i; j < ids.Length; j++) ids[j] = Guid.Empty;
                    break;
                }
            }
            (model.ClinicId, model.BranchId, model.DepartmentId, model.DoctorId, model.ServiceId) = (ids[0], ids[1], ids[2], ids[3], ids[4]);
            if (model.Selection == null && model.Stage >= 2)
                ModelState.AddModelError("", "Lütfen klinik, şube, bölüm, doktor ve hizmet seçimlerini tamamlayın.");
            model.Stage = model.Selection == null ? 1 : Math.Clamp(model.Stage, 1, 3);
            if (model.Stage == 3 && !ModelState.IsValid) model.Stage = 2;
            if (model.Stage >= 2 && ModelState.IsValid)
            {
                if (model.Date < today)
                {
                    ModelState.AddModelError(nameof(model.Date), "Geçmiş bir tarihe randevu alınamaz. Lütfen bugün veya ileri bir tarih seçin.");
                    model.Stage = 2;
                    model.LocalStart = null;
                    return;
                }
                model.Slots = await booking.Slots(UserId, model.DoctorId, model.ServiceId, model.Date);
                model.PrepareTimeCards(clock.GetUtcNow());
                if (model.Stage == 3 && (!model.LocalStart.HasValue || !model.Slots.Contains(model.LocalStart.Value)
                    || Scheduling.ToUtc(model.LocalStart.Value) <= clock.GetUtcNow()))
                {
                    ModelState.AddModelError("", model.LocalStart.HasValue
                        ? "Seçtiğiniz saat artık uygun değil. Lütfen güncel saatlerden birini seçin."
                        : "Lütfen uygun saatlerden birini seçin.");
                    model.Stage = 2;
                    model.LocalStart = null;
                }
            }
        }
        catch (BookingException) { model.Stage = 1; ModelState.AddModelError("", "Seçenekler veya uygun saatler yüklenemedi. Tarihi ve seçimlerinizi kontrol edin."); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Book([Bind("ClinicId,BranchId,DepartmentId,DoctorId,ServiceId,LocalStart,Date")] BookingModel model, CancellationToken cancellationToken)
    {
        if (Request.Form.Keys.Any(x => new[] { "PatientId", "StaffId", "Role", "Roles", "Status", "EndsAt" }.Contains(x, StringComparer.OrdinalIgnoreCase)))
            return BadRequest();
        model.Stage = 3;
        await PrepareBooking(model);
        if (model.Selection == null) ModelState.AddModelError("", "Lütfen klinik, şube, bölüm, doktor ve hizmet seçimlerini tamamlayın.");
        if (ModelState.IsValid && model.Selection != null && model.Stage == 3)
        {
            try
            {
                var id = await booking.Book(UserId, new(model.ClinicId, model.BranchId, model.DepartmentId, model.DoctorId, model.ServiceId, model.LocalStart!.Value));
                TempData["Message"] = "Randevu talebiniz alındı. Beklemede; personel incelemesinden sonra sonuçlandırılacaktır.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (BookingException) { ModelState.AddModelError("", "Randevu oluşturulamadı. Seçtiğiniz saat dolmuş olabilir. Güncel saatleri kontrol ederek tekrar deneyin."); model.Stage = 2; }
        }
        return View(model);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel([Bind("Id,Reason")] AppointmentActionModel model)
    {
        // Action comes from the endpoint, never from the submitted form.
        ModelState.Remove(nameof(model.Action));
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Reason)) { TempData["Message"] = "İptal gerekçesi zorunludur ve en fazla 2000 karakter olabilir."; return RedirectToAction(nameof(Details), new { model.Id }); }
        try { await booking.Act(UserId, model.Id, "Cancel", model.Reason); TempData["Message"] = "Randevu iptal edildi."; }
        catch (BookingException) { TempData["Message"] = "Randevu iptal edilemedi. Güncel randevu durumunu kontrol edin."; }
        return RedirectToAction(nameof(Details), new { model.Id });
    }
}
