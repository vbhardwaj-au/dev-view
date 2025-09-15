using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    [Route("[controller]/[action]")]
    public class AccountController : Controller
    {
        private readonly ILogger<AccountController> _logger;

        public AccountController(ILogger<AccountController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> SignIn(string returnUrl = "/process-auth")
        {
            _logger.LogInformation("SignIn initiated with returnUrl: {ReturnUrl}", returnUrl);
            _logger.LogInformation("User authenticated: {IsAuth}, User: {Name}", User?.Identity?.IsAuthenticated, User?.Identity?.Name);

            // Clear any stale authentication cookies first
            if (User?.Identity?.IsAuthenticated == true)
            {
                _logger.LogInformation("User already authenticated, clearing and re-authenticating");
                // Clear existing cookies to force re-authentication
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }

            // After Azure AD authentication, redirect to process-auth page
            var properties = new AuthenticationProperties
            {
                RedirectUri = "/process-auth",
                Items = { { "returnUrl", returnUrl } }
            };

            _logger.LogInformation("Triggering OpenID Connect challenge");
            // This will trigger the OpenID Connect authentication flow
            return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> SignOut()
        {
            _logger.LogInformation("SignOut initiated");

            // Clear all authentication cookies
            Response.Cookies.Delete("DevViewAuth");
            Response.Cookies.Delete(".AspNetCore.Cookies");
            Response.Cookies.Delete(".AspNetCore.CookiesC1");
            Response.Cookies.Delete(".AspNetCore.CookiesC2");

            // Sign out from both schemes
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            try
            {
                await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
            }
            catch
            {
                // Ignore if OpenID Connect is not configured
            }

            // Redirect to login page with a clear parameter to avoid auto-redirect
            return Redirect("/login?signout=true");
        }
    }
}