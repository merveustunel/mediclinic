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
    [Authorize(Policy = AuthRoles.Staff)]
    public async Task<IActionResult> Staff()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();
        try { return View("Staff", await booking.Dashboard(userId)); }
        catch (BookingException) { return Forbid(); }
    }
    [Authorize(Policy = AuthRoles.ClinicAdmin)]
    public async Task<IActionResult> ClinicAdmin()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Forbid();
        try { return View(await booking.ClinicAdminDashboard(userId)); }
        catch (BookingException) { return Forbid(); }
    }
}
