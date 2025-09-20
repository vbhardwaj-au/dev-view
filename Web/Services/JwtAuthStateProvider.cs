using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace Web.Services
{
    public class JwtAuthStateProvider : AuthenticationStateProvider
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<JwtAuthStateProvider> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private ClaimsPrincipal _anonymous = new(new ClaimsIdentity());
        private AuthenticationState? _cachedAuthState;
        private bool _isInitialized = false;

        public JwtAuthStateProvider(IJSRuntime jsRuntime, ILogger<JwtAuthStateProvider> logger, IHttpContextAccessor httpContextAccessor)
        {
            _jsRuntime = jsRuntime;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // Return cached state if available and initialized
            if (_cachedAuthState != null && _isInitialized)
            {
                return _cachedAuthState;
            }

            try
            {
                // Always try to get JWT token from localStorage first
                // This is our primary authentication mechanism that includes roles
                var token = await _jsRuntime.InvokeAsync<string>("authHelper.getToken", TimeSpan.FromSeconds(2));
                
                if (string.IsNullOrEmpty(token))
                {
                    // No JWT token, check for cookie authentication (from Azure AD)
                    var httpContext = _httpContextAccessor.HttpContext;
                    if (httpContext?.User?.Identity?.IsAuthenticated == true)
                    {
                        _logger.LogInformation("No JWT token found, but cookie authentication detected for user: {Name}", httpContext.User.Identity.Name);
                        _logger.LogWarning("Cookie authentication alone does not provide roles. User should complete Azure AD callback to get JWT token.");
                        // Return cookie auth but don't cache it - we want to check for JWT token next time
                        return new AuthenticationState(httpContext.User);
                    }

                    _cachedAuthState = new AuthenticationState(_anonymous);
                    _isInitialized = true;
                    return _cachedAuthState;
                }
                
                var (name, roles) = ParseJwtToken(token);
                
                if (string.IsNullOrEmpty(name))
                {
                    _logger.LogWarning("Invalid or expired token found");
                    _cachedAuthState = new AuthenticationState(_anonymous);
                    _isInitialized = true;
                    return _cachedAuthState;
                }
                
                // Create authenticated user
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, name)
                };
                foreach (var r in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, r));
                }
                
                var identity = new ClaimsIdentity(claims, authenticationType: "JwtAuth");
                var user = new ClaimsPrincipal(identity);
                
                // Set token for HTTP client
                BearerHandler.Token = token;

                _logger.LogInformation("Authentication restored for user: {Name} with roles: [{Roles}]", name, string.Join(", ", roles));
                _cachedAuthState = new AuthenticationState(user);
                _isInitialized = true;
                return _cachedAuthState;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("JavaScript interop"))
            {
                // This happens during prerendering - return anonymous state but don't cache it yet
                return new AuthenticationState(_anonymous);
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Timeout while getting authentication token");
                _cachedAuthState = new AuthenticationState(_anonymous);
                _isInitialized = true;
                return _cachedAuthState;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting authentication state");
                _cachedAuthState = new AuthenticationState(_anonymous);
                _isInitialized = true;
                return _cachedAuthState;
            }
        }

        public async Task SetUserAsync(string name, string[] roles, string token)
        {
            _logger.LogInformation("SetUserAsync called for user: {Name} with roles: {Roles}", 
                name, string.Join(", ", roles));
            
            // Store token in localStorage
            await _jsRuntime.InvokeVoidAsync("authHelper.saveToken", token);
            
            // Set token for HTTP client
            BearerHandler.Token = token;
            
            // Clear cached state
            _cachedAuthState = null;
            _isInitialized = false;
            
            // Notify that auth state has changed
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        public async Task ClearAsync()
        {
            _logger.LogInformation("ClearAsync called");
            
            try
            {
                await _jsRuntime.InvokeVoidAsync("authHelper.removeToken");
            }
            catch { }
            
            BearerHandler.Token = null;
            
            // Set cached state to anonymous
            _cachedAuthState = new AuthenticationState(_anonymous);
            _isInitialized = true;
            
            // Notify that auth state has changed
            NotifyAuthenticationStateChanged(Task.FromResult(_cachedAuthState));
        }
        
        private (string name, string[] roles) ParseJwtToken(string token)
        {
            try
            {
                var parts = token.Split('.');
                if (parts.Length != 3)
                {
                    _logger.LogWarning("Invalid JWT token format - expected 3 parts, got {Parts}", parts.Length);
                    return ("", Array.Empty<string>());
                }

                var payload = parts[1];
                var jsonBytes = ParseBase64WithoutPadding(payload);
                var json = Encoding.UTF8.GetString(jsonBytes);
                
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                // Check if token is expired
                if (root.TryGetProperty("exp", out var expElement))
                {
                    var exp = expElement.GetInt64();
                    var expDate = DateTimeOffset.FromUnixTimeSeconds(exp);
                    var now = DateTimeOffset.UtcNow;

                    if (expDate < now)
                    {
                        _logger.LogInformation("Token is expired - expDate: {ExpDate}, now: {Now}", expDate, now);
                        // Clear the expired token
                        try
                        {
                            _jsRuntime.InvokeVoidAsync("authHelper.removeToken").AsTask().Wait(TimeSpan.FromMilliseconds(500));
                        }
                        catch { }
                        return ("", Array.Empty<string>());
                    }
                }
                
                var name = root.TryGetProperty("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", out var nameElement) 
                    ? nameElement.GetString() ?? ""
                    : root.TryGetProperty("unique_name", out var uniqueNameElement) 
                        ? uniqueNameElement.GetString() ?? ""
                        : "";
                
                var roles = new List<string>();
                if (root.TryGetProperty("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", out var rolesElement))
                {
                    if (rolesElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var role in rolesElement.EnumerateArray())
                        {
                            var roleValue = role.GetString();
                            if (!string.IsNullOrEmpty(roleValue))
                            {
                                roles.Add(roleValue);
                                _logger.LogDebug("Found role in JWT: {Role}", roleValue);
                            }
                        }
                    }
                    else if (rolesElement.ValueKind == JsonValueKind.String)
                    {
                        var roleValue = rolesElement.GetString();
                        if (!string.IsNullOrEmpty(roleValue))
                        {
                            roles.Add(roleValue);
                            _logger.LogDebug("Found role in JWT: {Role}", roleValue);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("No roles found in JWT token - looked for claim type: http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
                }

                _logger.LogInformation("Parsed JWT token for user: {Name} with {RoleCount} role(s)", name, roles.Count);
                return (name, roles.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse JWT token");
                return ("", Array.Empty<string>());
            }
        }

        private byte[] ParseBase64WithoutPadding(string base64)
        {
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }
    }
}