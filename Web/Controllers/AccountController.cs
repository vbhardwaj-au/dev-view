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
        public IActionResult SignIn(string returnUrl = "/process-auth")
        {
            _logger.LogInformation("SignIn initiated with returnUrl: {ReturnUrl}", returnUrl);
            _logger.LogInformation("User authenticated: {IsAuth}, User: {Name}", User?.Identity?.IsAuthenticated, User?.Identity?.Name);

            // Check if user is already authenticated
            if (User?.Identity?.IsAuthenticated == true)
            {
                _logger.LogInformation("User already authenticated, redirecting to process-auth");
                return Redirect("/process-auth");
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
        public IActionResult SignOut()
        {
            var callbackUrl = Url.Content("~/");
            return SignOut(new AuthenticationProperties { RedirectUri = callbackUrl },
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme);
        }
    }
}