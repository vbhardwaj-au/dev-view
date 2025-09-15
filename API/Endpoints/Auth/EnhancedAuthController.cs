/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using System.Security.Claims;
using API.Services;
using Entities.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;

namespace API.Endpoints.Auth
{
    [ApiController]
    [Route("api/auth")]
    public class EnhancedAuthController : ControllerBase
    {
        private readonly IAuthenticationService _authService;
        private readonly IAuthenticationConfigService _configService;
        private readonly ILogger<EnhancedAuthController> _logger;

        public EnhancedAuthController(
            IAuthenticationService authService,
            IAuthenticationConfigService configService,
            ILogger<EnhancedAuthController> logger)
        {
            _authService = authService;
            _configService = configService;
            _logger = logger;
        }

        /// <summary>
        /// Get authentication configuration
        /// </summary>
        [HttpGet("config")]
        [AllowAnonymous]
        public IActionResult GetAuthConfig()
        {
            var config = _configService.GetAuthenticationConfig();
            return Ok(config);
        }

        /// <summary>
        /// Get Azure AD sign-in URL
        /// </summary>
        [HttpGet("azure-signin")]
        [AllowAnonymous]
        public IActionResult GetAzureAdSignInUrl()
        {
            if (!_configService.IsAzureAdEnabled)
            {
                return BadRequest("Azure AD is not enabled");
            }

            // For now, return a simple message indicating Azure AD is enabled
            // The actual Azure AD authentication will be handled by the Web app's OpenIdConnect middleware
            return Ok(new AuthResult
            {
                Success = true,
                RedirectUrl = "/login" // Redirect back to login page
            });
        }

        /// <summary>
        /// Database authentication login
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            _logger.LogInformation("Login attempt for user: {Username}", request.Username);

            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Username and password are required");
            }

            // Check if Azure AD is enabled and this should redirect
            if (_configService.IsAzureAdEnabled && !_configService.AllowFallback)
            {
                return Ok(new AuthResult
                {
                    Success = false,
                    RequiresRedirect = true,
                    RedirectUrl = "/auth/azure-signin",
                    ErrorMessage = "Please use Azure AD to sign in"
                });
            }

            var result = await _authService.AuthenticateAsync(request.Username, request.Password);
            
            if (result.Success)
            {
                _logger.LogInformation("Login successful for user: {Username}", request.Username);
                return Ok(new LoginResponse
                {
                    Token = result.Token!,
                    DisplayName = result.DisplayName!,
                    Roles = result.Roles!
                });
            }

            _logger.LogWarning("Login failed for user: {Username}", request.Username);
            return Unauthorized(new { message = result.ErrorMessage });
        }

        /// <summary>
        /// Azure AD authentication endpoint
        /// </summary>
        [HttpPost("azure-login")]
        [Authorize(AuthenticationSchemes = "AzureAd")]
        public async Task<IActionResult> AzureLogin()
        {
            if (!_configService.IsAzureAdEnabled)
            {
                return BadRequest("Azure AD authentication is not enabled");
            }

            _logger.LogInformation("Azure AD login attempt for user: {User}", User.Identity?.Name);

            var result = await _authService.AuthenticateWithAzureAdAsync(User);
            
            if (result.Success)
            {
                _logger.LogInformation("Azure AD login successful for user: {User}", result.DisplayName);
                return Ok(new LoginResponse
                {
                    Token = result.Token!,
                    DisplayName = result.DisplayName!,
                    Roles = result.Roles!
                });
            }

            _logger.LogWarning("Azure AD login failed: {Error}", result.ErrorMessage);
            return Unauthorized(new { message = result.ErrorMessage });
        }

        /// <summary>
        /// Handle Azure AD callback and return JWT token
        /// </summary>
        [HttpPost("azure-callback")]
        public async Task<IActionResult> AzureCallback([FromBody] AzureCallbackRequest request)
        {
            if (!_configService.IsAzureAdEnabled)
            {
                return BadRequest("Azure AD authentication is not enabled");
            }

            try
            {
                // Log the received data
                _logger.LogInformation("Azure callback received: ObjectId={ObjectId}, Email={Email}, DisplayName={DisplayName}, JobTitle={JobTitle}, Department={Department}",
                    request.ObjectId, request.Email, request.DisplayName, request.JobTitle, request.Department);

                // Here you would validate the Azure AD token and extract claims
                // For now, we'll assume the token is valid and extract user info
                var claims = new List<Claim>
                {
                    new("oid", request.ObjectId),
                    new("preferred_username", request.Email),
                    new("name", request.DisplayName)
                };

                if (!string.IsNullOrEmpty(request.JobTitle))
                    claims.Add(new("jobTitle", request.JobTitle));

                if (!string.IsNullOrEmpty(request.Department))
                    claims.Add(new("department", request.Department));

                var azureUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "AzureAd"));
                var result = await _authService.AuthenticateWithAzureAdAsync(azureUser);

                if (result.Success)
                {
                    return Ok(new LoginResponse
                    {
                        Token = result.Token!,
                        DisplayName = result.DisplayName!,
                        Roles = result.Roles!
                    });
                }

                return Unauthorized(new { message = result.ErrorMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Azure AD callback");
                return StatusCode(500, new { message = "Authentication failed" });
            }
        }

        /// <summary>
        /// Create a new database user (Admin only)
        /// </summary>
        [HttpPost("users")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                var user = await _authService.CreateDatabaseUserAsync(request);
                var roles = await _authService.GetUserRolesAsync(user.Id);
                
                return Ok(new AuthUserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    DisplayName = user.DisplayName,
                    Email = user.Email,
                    AuthProvider = user.AuthProvider,
                    IsActive = user.IsActive,
                    CreatedOn = user.CreatedOn,
                    Roles = roles.ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user: {Username}", request.Username);
                return StatusCode(500, new { message = "Failed to create user" });
            }
        }

        /// <summary>
        /// Get user information
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }

            var user = await _authService.GetUserByUsernameAsync(username);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _authService.GetUserRolesAsync(user.Id);

            return Ok(new AuthUserDto
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                Email = user.Email,
                JobTitle = user.JobTitle,
                Department = user.Department,
                AuthProvider = user.AuthProvider,
                IsActive = user.IsActive,
                CreatedOn = user.CreatedOn,
                ModifiedOn = user.ModifiedOn,
                Roles = roles.ToList()
            });
        }

        /// <summary>
        /// Check if this is the first run (no users exist)
        /// </summary>
        [HttpGet("check-first-run")]
        [AllowAnonymous]
        public async Task<IActionResult> CheckFirstRun()
        {
            await Task.CompletedTask; // Remove async warning
            // This method will be implemented to check if any users exist
            // For now, return false (not first run)
            return Ok(new { isFirstRun = false });
        }

        public class LoginRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class LoginResponse
        {
            public string Token { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string[] Roles { get; set; } = Array.Empty<string>();
        }

        public class AzureCallbackRequest
        {
            public string ObjectId { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string? JobTitle { get; set; }
            public string? Department { get; set; }
        }
    }
}
