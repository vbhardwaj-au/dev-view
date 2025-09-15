/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace Web.Services
{
    public interface IHybridAuthService
    {
        Task<AuthResult> LoginAsync(string username, string password);
        Task<AuthResult> HandleAzureCallbackAsync(ClaimsPrincipal user);
        Task<AuthConfig> GetAuthConfigAsync();
        Task<AuthResult> GetAzureAdLoginUrlAsync();
        Task LogoutAsync();
        Task<bool> InitializeAsync();
        bool IsAuthenticated { get; }
        string? Token { get; }
        string? DisplayName { get; }
        string[] Roles { get; }
    }

    public class HybridAuthService : IHybridAuthService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly NavigationManager _nav;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<HybridAuthService> _logger;

        public string? Token { get; private set; }
        public string? DisplayName { get; private set; }
        public string[] Roles { get; private set; } = Array.Empty<string>();
        public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

        public HybridAuthService(
            IHttpClientFactory httpClientFactory,
            NavigationManager nav,
            AuthenticationStateProvider authStateProvider,
            IJSRuntime jsRuntime,
            ILogger<HybridAuthService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _nav = nav;
            _authStateProvider = authStateProvider;
            _jsRuntime = jsRuntime;
            _logger = logger;
        }

        public async Task<AuthConfig> GetAuthConfigAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("api");
                var response = await client.GetAsync("api/auth/config");
                
                if (response.IsSuccessStatusCode)
                {
                    var config = await response.Content.ReadFromJsonAsync<AuthConfig>();
                    return config ?? new AuthConfig();
                }
                
                _logger.LogWarning("Failed to get auth config. Status: {StatusCode}", response.StatusCode);
                return new AuthConfig(); // Return default config
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting auth configuration");
                return new AuthConfig(); // Return default config
            }
        }

        public async Task<AuthResult> LoginAsync(string username, string password)
        {
            try
            {
                var authConfig = await GetAuthConfigAsync();
                
                // If Azure AD is enabled and no fallback, redirect to Azure AD
                if (authConfig.AzureAdEnabled && !authConfig.AllowFallback)
                {
                    return new AuthResult
                    {
                        Success = false,
                        RequiresRedirect = true,
                        RedirectUrl = "/MicrosoftIdentity/Account/SignIn"
                    };
                }

                var client = _httpClientFactory.CreateClient("api");
                var loginRequest = new LoginRequest(username, password);
                var json = JsonSerializer.Serialize(loginRequest);
                
                _logger.LogInformation("Attempting database login for user: {Username}", username);
                
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("api/auth/login", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.Token))
                    {
                        await SetAuthenticationAsync(loginResponse.Token, loginResponse.DisplayName, loginResponse.Roles);
                        
                        return new AuthResult
                        {
                            Success = true,
                            Token = loginResponse.Token,
                            DisplayName = loginResponse.DisplayName,
                            Roles = loginResponse.Roles
                        };
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    
                    // Check if this requires Azure AD redirect
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<AuthResult>(errorContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (errorResponse?.RequiresRedirect == true)
                        {
                            return errorResponse;
                        }
                    }
                    catch
                    {
                        // Ignore JSON parsing errors
                    }
                    
                    _logger.LogWarning("Login failed. Status: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
                }
                
                return new AuthResult { Success = false, ErrorMessage = "Invalid username or password" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for user: {Username}", username);
                return new AuthResult { Success = false, ErrorMessage = $"Login error: {ex.Message}" };
            }
        }

        public async Task<AuthResult> HandleAzureCallbackAsync(ClaimsPrincipal user)
        {
            try
            {
                // Log all claims for debugging
                _logger.LogInformation("HandleAzureCallbackAsync: Processing claims for user");
                foreach (var claim in user.Claims)
                {
                    _logger.LogInformation("Claim: Type={Type}, Value={Value}", claim.Type, claim.Value);
                }

                var client = _httpClientFactory.CreateClient("api");

                // Extract claims using standard claim types
                // Azure AD via Microsoft Identity Web uses different claim types
                var callbackRequest = new AzureCallbackRequest
                {
                    // Object ID can be in different claim types
                    ObjectId = user.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                        ?? user.FindFirst("oid")?.Value
                        ?? string.Empty,

                    // Email can be in various claim types
                    Email = user.FindFirst(ClaimTypes.Email)?.Value
                        ?? user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value
                        ?? user.FindFirst("preferred_username")?.Value
                        ?? user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn")?.Value
                        ?? user.FindFirst("upn")?.Value
                        ?? user.FindFirst(ClaimTypes.Name)?.Value
                        ?? string.Empty,

                    // Display name from various sources
                    DisplayName = user.FindFirst("name")?.Value
                        ?? user.FindFirst(ClaimTypes.GivenName)?.Value
                        ?? user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value
                        ?? string.Empty,

                    // Job title and department
                    JobTitle = user.FindFirst("jobTitle")?.Value
                        ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/jobtitle")?.Value,

                    Department = user.FindFirst("department")?.Value
                        ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/department")?.Value
                };

                // Log the extracted data
                _logger.LogInformation("Extracted Azure AD data: ObjectId={ObjectId}, Email={Email}, DisplayName={DisplayName}, JobTitle={JobTitle}, Department={Department}",
                    callbackRequest.ObjectId, callbackRequest.Email, callbackRequest.DisplayName, callbackRequest.JobTitle, callbackRequest.Department);

                var json = JsonSerializer.Serialize(callbackRequest);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await client.PostAsync("api/auth/azure-callback", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.Token))
                    {
                        await SetAuthenticationAsync(loginResponse.Token, loginResponse.DisplayName, loginResponse.Roles);
                        
                        return new AuthResult
                        {
                            Success = true,
                            Token = loginResponse.Token,
                            DisplayName = loginResponse.DisplayName,
                            Roles = loginResponse.Roles
                        };
                    }
                }
                
                return new AuthResult { Success = false, ErrorMessage = "Azure AD authentication failed" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Azure AD callback");
                return new AuthResult { Success = false, ErrorMessage = "Azure AD authentication failed" };
            }
        }

        public async Task<AuthResult> GetAzureAdLoginUrlAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("api");
                var response = await client.GetAsync("api/auth/azure-signin");
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<AuthResult>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (result != null && !string.IsNullOrEmpty(result.RedirectUrl))
                    {
                        return new AuthResult
                        {
                            Success = true,
                            RedirectUrl = result.RedirectUrl
                        };
                    }
                }
                
                return new AuthResult { Success = false, ErrorMessage = "Failed to get Azure AD login URL" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Azure AD login URL");
                return new AuthResult { Success = false, ErrorMessage = $"Error getting Azure AD login URL: {ex.Message}" };
            }
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                // Check if we have a token in localStorage
                var storedToken = await _jsRuntime.InvokeAsync<string>("authHelper.getToken");
                if (!string.IsNullOrEmpty(storedToken))
                {
                    Token = storedToken;
                    BearerHandler.Token = Token;
                    _logger.LogInformation("Restored token from localStorage");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore token");
            }
            return false;
        }

        public async Task LogoutAsync()
        {
            Token = null;
            DisplayName = null;
            Roles = Array.Empty<string>();
            BearerHandler.Token = null;

            // Clear token from localStorage
            try
            {
                await _jsRuntime.InvokeVoidAsync("authHelper.removeToken");
            }
            catch { }

            if (_authStateProvider is JwtAuthStateProvider jwt)
            {
                await jwt.ClearAsync();
            }

            // Sign out from both cookie auth and Azure AD
            _nav.NavigateTo("/Account/SignOut", forceLoad: true);
        }

        private async Task SetAuthenticationAsync(string token, string displayName, string[] roles)
        {
            Token = token;
            DisplayName = displayName;
            Roles = roles ?? Array.Empty<string>();
            BearerHandler.Token = Token;
            
            _logger.LogInformation("Authentication successful. DisplayName: {DisplayName}, Roles: {Roles}", 
                DisplayName, string.Join(", ", Roles));
            
            // Save token to localStorage
            try
            {
                await _jsRuntime.InvokeVoidAsync("authHelper.saveToken", Token);
                _logger.LogInformation("Token saved to localStorage");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save token to localStorage");
            }
            
            // Update authentication state
            if (_authStateProvider is JwtAuthStateProvider jwt)
            {
                await jwt.SetUserAsync(DisplayName, Roles, Token);
                _logger.LogInformation("Authentication state updated");
            }
        }
    }

    // DTOs for the hybrid auth service
    public record LoginRequest(string Username, string Password);
    
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string[] Roles { get; set; } = Array.Empty<string>();
    }

    public class AuthResult
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? DisplayName { get; set; }
        public string[]? Roles { get; set; }
        public bool RequiresRedirect { get; set; }
        public string? RedirectUrl { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class AuthConfig
    {
        public bool AzureAdEnabled { get; set; }
        public string DefaultProvider { get; set; } = "Database";
        public bool AllowFallback { get; set; } = true;
        public bool AutoCreateUsers { get; set; } = true;
        public string? AzureAdLoginUrl { get; set; }
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
