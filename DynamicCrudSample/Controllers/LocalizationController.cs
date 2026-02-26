using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace DynamicCrudSample.Controllers;

public class LocalizationController : Controller
{
    [HttpPost]
    public IActionResult SetLanguage(string culture, string? returnUrl = null)
    {
        var value = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));
        Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName, value);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Index", "DynamicEntity");
    }
}
