using MediClinic.Domain;
using MediClinic.Infrastructure.Identity;
using MediClinic.Infrastructure.Persistence;
using MediClinic.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace MediClinic.Web.Controllers;

[AutoValidateAntiforgeryToken]
public sealed class AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
    RoleManager<IdentityRole> roles, MediClinicDbContext db, IWebHostEnvironment environment) : Controller
{
    [AllowAnonymous, HttpGet] public IActionResult Register() => View(new RegisterModel());
    [AllowAnonymous, HttpPost] public async Task<IActionResult> Register(RegisterModel model)
    {
        // Reject privilege/link injection explicitly; the binding model exposes neither.
        if (Request.Form.Keys.Any(k => new[] { "role", "roles", "patientid", "staffid" }.Contains(k.ToLowerInvariant())))
            return BadRequest();
        if (model.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow)) ModelState.AddModelError("DateOfBirth", "Birth date cannot be in the future.");
        if (!ModelState.IsValid) return View(model);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var patient = new Patient(Guid.NewGuid(), model.FirstName, model.LastName, model.DateOfBirth!.Value);
        var user = ApplicationUser.ForPatient(model.Email.Trim(), patient);
        db.Patients.Add(patient);
        var result = await users.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            if (!await roles.RoleExistsAsync(AuthRoles.Patient))
            {
                var roleResult = await roles.CreateAsync(new IdentityRole(AuthRoles.Patient));
                if (!roleResult.Succeeded) throw new InvalidOperationException("Could not initialize patient role.");
            }
            var assigned = await users.AddToRoleAsync(user, AuthRoles.Patient);
            if (!assigned.Succeeded) throw new InvalidOperationException("Could not assign patient role.");
            await transaction.CommitAsync();
        }
        // Identical response for existing emails and successful registration. Never auto-login.
        else if (result.Errors.Any(e => e.Code is not ("DuplicateUserName" or "DuplicateEmail")))
        {
            foreach (var error in result.Errors.Where(e => e.Code is not ("DuplicateUserName" or "DuplicateEmail")))
                ModelState.AddModelError("", error.Description);
            return View(model);
        }
        return View("RegistrationComplete");
    }
    [AllowAnonymous, HttpGet] public IActionResult Login() => View(new LoginModel());
    [AllowAnonymous, HttpPost] public async Task<IActionResult> Login(LoginModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await users.FindByEmailAsync(model.Email.Trim());
        if (!environment.IsDevelopment() && user != null && DemoSeed.IsDemoUser(user.Id))
        {
            ModelState.AddModelError("", "Unable to sign in with these credentials.");
            return View(model);
        }
        var result = await signIn.PasswordSignInAsync(model.Email.Trim(), model.Password, false, lockoutOnFailure: true);
        if (result.Succeeded) return RedirectToAction("Index", "Dashboard");
        ModelState.AddModelError("", "Unable to sign in with these credentials.");
        return View(model);
    }
    [Authorize, HttpPost] public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }
    [AllowAnonymous, HttpGet] public IActionResult AccessDenied() { Response.StatusCode = 403; return View(); }
}
