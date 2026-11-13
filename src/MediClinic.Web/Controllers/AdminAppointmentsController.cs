using MediClinic.Web.Localization;
﻿using System.Security.Claims;
using MediClinic.Application;
using MediClinic.Domain;
using MediClinic.Infrastructure.Identity;
using MediClinic.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace MediClinic.Web.Controllers;

[Authorize(Policy = AuthRoles.ClinicAdmin)]
public sealed class AdminAppointmentsController(IAppointmentBooking booking, TimeProvider clock) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> Index(string? status, DateOnly? from, DateOnly? to)
    {
        if (!ModelState.IsValid || !PatientAppointmentsModel.IsValidFilter(status) || to == DateOnly.MaxValue || from > to)
            return BadRequest(Ui.Text("Durum veya tarih filtresi geçersiz."));
        try
        {
            var items = await booking.AdminAppointments(UserId, string.IsNullOrEmpty(status) ? null : Enum.Parse<AppointmentStatus>(status), from, to);
            ViewData["From"] = from?.ToString("yyyy-MM-dd"); ViewData["To"] = to?.ToString("yyyy-MM-dd");
            return View(new StaffAppointmentsModel(items, status, null));
        }
        catch (BookingException) { return Forbid(); }
    }
    public async Task<IActionResult> Details(Guid id)
    {
        try
        {
            var item = await booking.AdminAppointment(UserId, id);
            return item == null ? NotFound() : View(new StaffAppointmentDetailsModel(item, clock.GetUtcNow() >= item.Appointment.EndsAt));
        }
        catch (BookingException) { return Forbid(); }
    }
    [Authorize(Policy = AuthRoles.ClinicAdmin)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Review([Bind("Id,Action,Reason")] AppointmentActionModel model)
    {
        StaffAppointment? item;
        try { item = await booking.AdminAppointment(UserId, model.Id); }
        catch (BookingException) { return Forbid(); }
        if (item == null) return NotFound();
        if (!ModelState.IsValid || model.Action is not ("Confirm" or "Reject"))
        {
            ModelState.Clear();
            ModelState.AddModelError("", Ui.Text("Geçerli bir işlem seçin. Gerekçe en fazla 2000 karakter olabilir."));
        }
        if (model.Action == "Reject" && string.IsNullOrWhiteSpace(model.Reason))
            ModelState.AddModelError("Reason", Ui.Text("Ret gerekçesi zorunludur."));
        if (ModelState.IsValid)
        {
            try
            {
                await booking.AdminAct(UserId, model.Id, model.Action, model.Reason);
                TempData["Message"] = model.Action switch { "Confirm" => Ui.Text("Randevu onaylandı."), "Reject" => Ui.Text("Randevu reddedildi."), _ => Ui.Text("Randevu tamamlandı.") };
                return RedirectToAction(nameof(Details), new { model.Id });
            }
            catch (BookingException)
            {
                ModelState.AddModelError("", Ui.Text("İşlem gerçekleştirilemedi. Randevunun durumu değişmiş veya bitiş saati henüz gelmemiş olabilir. Güncel bilgileri kontrol edin."));
                item = await booking.AdminAppointment(UserId, model.Id);
                if (item == null) return NotFound();
            }
        }
        return View("Details", new StaffAppointmentDetailsModel(item, clock.GetUtcNow() >= item.Appointment.EndsAt, model.Reason));
    }
}
