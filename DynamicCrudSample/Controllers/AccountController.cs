using System.Security.Claims;
using DynamicCrudSample.Models.Auth;
using DynamicCrudSample.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace DynamicCrudSample.Controllers;

public class AccountController : Controller
{
    private readonly IUserAuthService _users;
    private readonly ILogger<AccountController> _logger;
    private readonly IAuditLogService _audit;

    public AccountController(IUserAuthService users, ILogger<AccountController> logger, IAuditLogService audit)
    {
        _users = users;
        _logger = logger;
        _audit = audit;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _users.ValidateCredentialsAsync(model.UserName, model.Password);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.GivenName, user.DisplayName),
            new("lang", user.PreferredLanguage),
            new(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(user.PreferredLanguage)));

        _logger.LogInformation("User '{UserName}' signed in", user.UserName);
        await TryWriteAuditAsync("login", "account", "Sign in", user.UserName);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "DynamicEntity");
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        var userName = User.Identity?.Name ?? "anonymous";
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("User '{UserName}' signed out", userName);
        await TryWriteAuditAsync("logout", "account", "Sign out", userName);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private async Task TryWriteAuditAsync(string action, string? entity, string? detail, string? userName)
    {
        try
        {
            await _audit.WriteAsync(action, entity, detail, userName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit write failed for action={Action}, user={UserName}", action, userName);
        }
    }
}
