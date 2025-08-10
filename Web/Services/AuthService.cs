using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace Web.Services
{
    public class AuthService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly NavigationManager _nav;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly IJSRuntime _jsRuntime;

        public string? Token { get; private set; }
        public string? DisplayName { get; private set; }
        public string[] Roles { get; private set; } = Array.Empty<string>();

        public bool IsAuthenticated => !string.IsNullOrEmpty(Token);

        public AuthService(IHttpClientFactory httpClientFactory,
                           NavigationManager nav,
                           AuthenticationStateProvider authStateProvider,
                           IJSRuntime jsRuntime)
        {
            _httpClientFactory = httpClientFactory;
            _nav = nav;
            _authStateProvider = authStateProvider;
            _jsRuntime = jsRuntime;
        }

        private record LoginResponse(string Token, string DisplayName, string[] Roles);
        private record LoginRequest(string Username, string Password);

        public async Task<bool> LoginAsync(string username, string password)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("api");
                var loginRequest = new LoginRequest(username, password);
                var json = JsonSerializer.Serialize(loginRequest);
                
                Console.WriteLine($"[AuthService] Attempting login for user: {username}");
                Console.WriteLine($"[AuthService] API URL: {client.BaseAddress}api/auth/login");
                Console.WriteLine($"[AuthService] Request JSON: {json}");
                
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp = await client.PostAsync("api/auth/login", content);
                
                Console.WriteLine($"[AuthService] Response status: {resp.StatusCode}");
                
                if (!resp.IsSuccessStatusCode)
                {
                    var errorContent = await resp.Content.ReadAsStringAsync();
                    Console.WriteLine($"[AuthService] Login failed. Status: {resp.StatusCode}, Error: {errorContent}");
                    return false;
                }
                
                var responseContent = await resp.Content.ReadAsStringAsync();
                Console.WriteLine($"[AuthService] Response content: {responseContent}");
                
                var data = JsonSerializer.Deserialize<LoginResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data == null || string.IsNullOrEmpty(data.Token))
                {
                    Console.WriteLine("[AuthService] Invalid response data or missing token");
                    return false;
                }
                
                Token = data.Token;
                DisplayName = data.DisplayName;
                Roles = data.Roles ?? Array.Empty<string>();
                BearerHandler.Token = Token;
                
                Console.WriteLine($"[AuthService] Login successful. DisplayName: {DisplayName}, Roles: {string.Join(", ", Roles)}");
                
                // Save token to localStorage via JavaScript
                try
                {
                    await _jsRuntime.InvokeVoidAsync("authHelper.saveToken", Token);
                    Console.WriteLine("[AuthService] Token saved to localStorage");
                }
                catch (Exception jsEx)
                {
                    Console.WriteLine($"[AuthService] Failed to save token to localStorage: {jsEx.Message}");
                }
                
                // Update the authentication state provider - this is the key!
                if (_authStateProvider is JwtAuthStateProvider jwt)
                {
                    await jwt.SetUserAsync(DisplayName ?? username, Roles, Token);
                    Console.WriteLine("[AuthService] Authentication state updated");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthService] Login exception: {ex.Message}");
                Console.WriteLine($"[AuthService] Stack trace: {ex.StackTrace}");
                return false;
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
                    // TODO: Validate token and decode claims
                    Token = storedToken;
                    BearerHandler.Token = Token;
                    Console.WriteLine("[AuthService] Restored token from localStorage");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuthService] Failed to restore token: {ex.Message}");
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
            _nav.NavigateTo("/login");
        }
    }
}


