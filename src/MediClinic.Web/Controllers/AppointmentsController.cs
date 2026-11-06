using System.Security.Claims;
using MediClinic.Application;
using MediClinic.Infrastructure.Identity;
using MediClinic.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediClinic.Web.Controllers;

[Authorize(Policy = AuthRoles.Patient)]
public sealed class AppointmentsController(IAppointmentBooking booking) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> Index()
    {
        try { return View("List", await booking.List(UserId, false)); }
        catch (BookingException) { return Forbid(); }
    }
    public async Task<IActionResult> Details(Guid id)
    {
        try { var item = await booking.Find(UserId, id, false); return item == null ? NotFound() : View(item); }
        catch (BookingException) { return Forbid(); }
    }
    [HttpGet]
    public async Task<IActionResult> Book([Bind("ClinicId,BranchId,DepartmentId,DoctorId,ServiceId,Date")] BookingModel model)
    {
        ModelState.Remove(nameof(model.LocalStart));
        try
        {
            model.Options = await booking.Options(UserId);
            if (model.DoctorId != Guid.Empty && model.ServiceId != Guid.Empty && ModelState.IsValid)
                model.Slots = await booking.Slots(UserId, model.DoctorId, model.ServiceId, model.Date);
        }
        catch (BookingException ex) { ModelState.AddModelError("", ex.Message); }
        return View(model);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Book([Bind("ClinicId,BranchId,DepartmentId,DoctorId,ServiceId,LocalStart,Date")] BookingModel model, CancellationToken cancellationToken)
    {
        if (Request.Form.Keys.Any(x => new[] { "PatientId", "StaffId", "Role", "Roles", "Status", "EndsAt" }.Contains(x, StringComparer.OrdinalIgnoreCase)))
            return BadRequest();
        if (ModelState.IsValid)
        {
            try
            {
                var id = await booking.Book(UserId, new(model.ClinicId, model.BranchId, model.DepartmentId, model.DoctorId, model.ServiceId, model.LocalStart!.Value));
                TempData["Message"] = "Appointment requested. Awaiting staff review.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (BookingException ex) { ModelState.AddModelError("", ex.Message); }
        }
        try { model.Options = await booking.Options(UserId); }
        catch (BookingException) { return Forbid(); }
        return View(model);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel([Bind("Id,Reason")] AppointmentActionModel model)
    {
        // Action comes from the endpoint, never from the submitted form.
        ModelState.Remove(nameof(model.Action));
        if (!ModelState.IsValid) { TempData["Message"] = "A reason of at most 2000 characters is required."; return RedirectToAction(nameof(Details), new { model.Id }); }
        try { await booking.Act(UserId, model.Id, "Cancel", model.Reason); TempData["Message"] = "Appointment cancelled."; }
        catch (BookingException ex) { TempData["Message"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { model.Id });
    }
}
