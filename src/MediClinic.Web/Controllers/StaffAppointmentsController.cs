using System.Security.Claims;
using MediClinic.Application;
using MediClinic.Domain;
using MediClinic.Infrastructure.Identity;
using MediClinic.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace MediClinic.Web.Controllers;

[Authorize(Roles = AuthRoles.Staff + "," + AuthRoles.ClinicAdmin)]
public sealed class StaffAppointmentsController(IAppointmentBooking booking, TimeProvider clock) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> Index(string? status, DateOnly? date)
    {
        if (!ModelState.IsValid || !PatientAppointmentsModel.IsValidFilter(status) || date == DateOnly.MaxValue)
            return BadRequest("Durum veya tarih filtresi geçersiz.");
        try
        {
            var items = await booking.StaffAppointments(UserId, string.IsNullOrEmpty(status) ? null : Enum.Parse<AppointmentStatus>(status), date);
            return View(new StaffAppointmentsModel(items, status, date));
        }
        catch (BookingException) { return Forbid(); }
    }
    public async Task<IActionResult> Details(Guid id)
    {
        try
        {
            var item = await booking.StaffAppointment(UserId, id);
            return item == null ? NotFound() : View(new StaffAppointmentDetailsModel(item, clock.GetUtcNow() >= item.Appointment.EndsAt));
        }
        catch (BookingException) { return Forbid(); }
    }
    [Authorize(Policy = AuthRoles.Staff)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Review([Bind("Id,Action,Reason")] AppointmentActionModel model)
    {
        StaffAppointment? item;
        try { item = await booking.StaffAppointment(UserId, model.Id); }
        catch (BookingException) { return Forbid(); }
        if (item == null) return NotFound();
        if (!ModelState.IsValid || model.Action is not ("Confirm" or "Reject" or "Complete"))
        {
            ModelState.Clear();
            ModelState.AddModelError("", "Geçerli bir işlem seçin. Gerekçe en fazla 2000 karakter olabilir.");
        }
        if (model.Action == "Reject" && string.IsNullOrWhiteSpace(model.Reason))
            ModelState.AddModelError("Reason", "Ret gerekçesi zorunludur.");
        if (ModelState.IsValid)
        {
            try
            {
                await booking.Act(UserId, model.Id, model.Action, model.Reason);
                TempData["Message"] = model.Action switch { "Confirm" => "Randevu onaylandı.", "Reject" => "Randevu reddedildi.", _ => "Randevu tamamlandı." };
                return RedirectToAction(nameof(Details), new { model.Id });
            }
            catch (BookingException)
            {
                ModelState.AddModelError("", "İşlem gerçekleştirilemedi. Randevunun durumu değişmiş veya bitiş saati henüz gelmemiş olabilir. Güncel bilgileri kontrol edin.");
                item = await booking.StaffAppointment(UserId, model.Id);
                if (item == null) return NotFound();
            }
        }
        return View("Details", new StaffAppointmentDetailsModel(item, clock.GetUtcNow() >= item.Appointment.EndsAt, model.Reason));
    }
}
