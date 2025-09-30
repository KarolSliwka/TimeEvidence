using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TimeEvidence.Controllers;

[ApiController]
public class AccountController : Controller
{
    private readonly IConfiguration _config;

    public AccountController(IConfiguration config)
    {
        _config = config;
    }

    public record LoginRequest(string? Username, string? Password, string? ReturnUrl);

    [HttpPost("/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromForm] LoginRequest form)
    {
        var expectedUser = Environment.GetEnvironmentVariable("AUTH__USERNAME") ?? _config["Auth:Username"];
        var expectedPass = Environment.GetEnvironmentVariable("AUTH__PASSWORD") ?? _config["Auth:Password"];

        if (string.Equals(form.Username, expectedUser, StringComparison.Ordinal) &&
            string.Equals(form.Password, expectedPass, StringComparison.Ordinal))
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, expectedUser)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

            var returnUrl = string.IsNullOrWhiteSpace(form.ReturnUrl) ? "/" : form.ReturnUrl!;
            // Only allow local redirects
            if (!Url.IsLocalUrl(returnUrl)) returnUrl = "/";
            return Redirect(returnUrl);
        }

        // Failed login -> redirect back to /login with error flag
        var target = string.IsNullOrWhiteSpace(form.ReturnUrl) ? "/login?error=1" : $"/login?error=1&returnUrl={Uri.EscapeDataString(form.ReturnUrl!)}";
        return Redirect(target);
    }

    [HttpPost("/logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/login");
    }
}
