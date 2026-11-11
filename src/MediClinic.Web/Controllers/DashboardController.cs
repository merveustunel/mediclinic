using MediClinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MediClinic.Application;
using MediClinic.Web.Models;
namespace MediClinic.Web.Controllers;
[Authorize]
public sealed class DashboardController(IAppointmentBooking booking, TimeProvider clock) : Controller
{
    public IActionResult Index() => RedirectToAction(User.IsInRole(AuthRoles.ClinicAdmin) ? "ClinicAdmin" : User.IsInRole(AuthRoles.Staff) ? "Staff" : "Patient");
    [Authorize(Policy = AuthRoles.Patient)]
    public async Task<IActionResult> Patient()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();
        try
        {
            var appointments = await booking.List(userId, false);
            return View(PatientDashboardModel.Create(appointments, clock.GetUtcNow()));
        }
        catch (BookingException) { return Forbid(); }
    }
    [Authorize(Policy = AuthRoles.Staff)] public IActionResult Staff() => View("Landing", "Staff dashboard");
    [Authorize(Policy = AuthRoles.ClinicAdmin)] public IActionResult ClinicAdmin() => View("Landing", "Clinic administrator dashboard");
}
