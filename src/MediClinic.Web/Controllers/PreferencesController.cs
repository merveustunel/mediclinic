using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using MediClinic.Web.Localization;

namespace MediClinic.Web.Controllers;

[AutoValidateAntiforgeryToken]
public sealed class PreferencesController : Controller
{
    [HttpPost]
    public IActionResult Language(string culture, string? returnUrl)
    {
        if (culture is not ("tr-TR" or "en-GB")) return BadRequest();
        Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, IsEssential = true });
        if (Request.Headers["Accept"].ToString().Contains("application/json"))
            return Json(new { culture, messages = Ui.Catalog(culture) });
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
