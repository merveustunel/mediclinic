using System.Security.Claims;
using MediClinic.Application;
using MediClinic.Infrastructure.Identity;
using MediClinic.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediClinic.Web.Controllers;

[Authorize(Roles = AuthRoles.Staff + "," + AuthRoles.ClinicAdmin)]
public sealed class StaffAppointmentsController(IAppointmentBooking booking) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> Index()
    {
        try { return View("~/Views/Appointments/List.cshtml", await booking.List(UserId, true)); }
        catch (BookingException) { return Forbid(); }
    }
    public async Task<IActionResult> Details(Guid id)
    {
        try { var item = await booking.Find(UserId, id, true); return item == null ? NotFound() : View("~/Views/Appointments/Details.cshtml", item); }
        catch (BookingException) { return Forbid(); }
    }
    [Authorize(Policy = AuthRoles.Staff)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Review([Bind("Id,Action,Reason")] AppointmentActionModel model)
    {
        if (!ModelState.IsValid || model.Action is not ("Confirm" or "Reject" or "Complete"))
        { TempData["Message"] = "Choose a valid action and a reason of at most 2000 characters."; return RedirectToAction(nameof(Details), new { model.Id }); }
        try { await booking.Act(UserId, model.Id, model.Action, model.Reason); TempData["Message"] = "Appointment updated."; }
        catch (BookingException ex) { TempData["Message"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { model.Id });
    }
}
