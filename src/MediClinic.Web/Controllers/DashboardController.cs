using MediClinic.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace MediClinic.Web.Controllers;
[Authorize]
public sealed class DashboardController : Controller
{
    public IActionResult Index() => RedirectToAction(User.IsInRole(AuthRoles.ClinicAdmin) ? "ClinicAdmin" : User.IsInRole(AuthRoles.Staff) ? "Staff" : "Patient");
    [Authorize(Policy = AuthRoles.Patient)] public IActionResult Patient() => View("Landing", "Patient dashboard");
    [Authorize(Policy = AuthRoles.Staff)] public IActionResult Staff() => View("Landing", "Staff dashboard");
    [Authorize(Policy = AuthRoles.ClinicAdmin)] public IActionResult ClinicAdmin() => View("Landing", "Clinic administrator dashboard");
}
