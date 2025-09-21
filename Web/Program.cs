/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using Web.Components;
using Web.Services;
using Radzen;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();
builder.Services.AddControllersWithViews(); // Add MVC support for AccountController
builder.Services.AddRadzenComponents();

// Add WorkspaceService as singleton to maintain state across the application
builder.Services.AddSingleton<WorkspaceService>();
builder.Services.AddScoped<BitbucketUrlService>();

// Add Data layer services
builder.Services.AddScoped<Data.Repositories.SettingsRepository>();

// Check if Azure AD is enabled
var azureAdEnabled = builder.Configuration.GetValue<bool>("AzureAd:Enabled", false);

// Register authentication services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IHybridAuthService, HybridAuthService>();
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider => provider.GetRequiredService<JwtAuthStateProvider>());
builder.Services.AddHttpContextAccessor();

// Configure HttpClient for API with bearer injection
builder.Services.AddHttpClient("api", client =>
{
    var baseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:5001/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromHours(1);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    // In development, ignore SSL certificate errors
    if (builder.Environment.IsDevelopment())
    {
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
    }
    return new HttpClientHandler();
})
.AddHttpMessageHandler(() => new BearerHandler());

// Register HttpClient with BearerHandler for dependency injection
builder.Services.AddScoped(sp => 
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return factory.CreateClient("api");
});

// Server-side auth for component [Authorize]
if (azureAdEnabled)
{
    // For hybrid mode, use Cookie as default scheme, not OpenIdConnect
    // This prevents automatic redirect to Azure AD
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(options =>
    {
        builder.Configuration.GetSection("AzureAd").Bind(options);
        options.SaveTokens = true; // Save tokens in the auth cookie

        // Request additional scopes for user profile information
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("User.Read");

        // Fix correlation issues in development
        options.Events = new OpenIdConnectEvents
        {
            OnRemoteFailure = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError("Remote authentication failure: {Error}", context.Failure?.Message);
                context.Response.Redirect("/login?error=remote");
                context.HandleResponse();
                return Task.CompletedTask;
            },
            OnRedirectToIdentityProvider = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("Redirecting to Azure AD for authentication");
                // Ensure proper redirect URI
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                // After successful Azure AD authentication, log the user info
                // The actual processing will happen on the client side after redirect
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("Azure AD token validated for user: {Name}, Email: {Email}, OID: {OID}",
                    context.Principal?.Identity?.Name,
                    context.Principal?.FindFirst("preferred_username")?.Value ?? context.Principal?.FindFirst("email")?.Value,
                    context.Principal?.FindFirst("oid")?.Value);

                // Log ALL claims for debugging
                if (context.Principal?.Claims != null)
                {
                    foreach (var claim in context.Principal.Claims)
                    {
                        logger.LogInformation("Azure AD Claim: {Type} = {Value}", claim.Type, claim.Value);
                    }
                }

                // The cookie auth is now established, we'll process the user on the client side
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError("Authentication failed: {Error}", context.Exception?.Message);
                context.Response.Redirect("/login?error=authfailed");
                context.HandleResponse();
                return Task.CompletedTask;
            }
        };
    });

    // Configure cookie authentication options (this modifies the existing cookie scheme added by MicrosoftIdentityWebApp)
    builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        // Safari-friendly cookie settings
        options.Cookie.SameSite = builder.Environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.Cookie.IsEssential = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.Name = "DevViewAuth";
    });

    // Add Microsoft Identity UI
    builder.Services.AddRazorPages()
        .AddMicrosoftIdentityUI();
}
else
{
    // Add Razor Pages (required for MapRazorPages even when not using Azure AD)
    builder.Services.AddRazorPages();

    // Cookie authentication for database users
    builder.Services.AddAuthentication("DevView")
        .AddCookie("DevView", options =>
        {
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/login";
            options.SlidingExpiration = true;
        });
}

builder.Services.AddAuthorization();

// Add data protection for session state
builder.Services.AddDataProtection();

builder.Services.AddServerSideBlazor().AddCircuitOptions(options => { options.DetailedErrors = true; });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// Map Controllers (for API endpoints and Account controller)
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map Razor Pages (required for Microsoft Identity UI)
app.MapRazorPages();

app.MapRazorComponents<Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
