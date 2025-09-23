/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using Data.Models;
using Data.Repositories;
using API.Services;
using API.Endpoints;
using Integration.Commits;
using Integration.Common;
using Integration.PullRequests;
using Integration.Repositories;
using Integration.Users;
using Integration.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

// Set Dapper default command timeout
Dapper.SqlMapper.Settings.CommandTimeout = 300; // 5 minutes

var builder = WebApplication.CreateBuilder(args);

// Add memory cache for notifications
builder.Services.AddMemoryCache();

// Register authentication services
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IAuthenticationConfigService, AuthenticationConfigService>();
builder.Services.AddScoped<IMicrosoftGraphService, MicrosoftGraphService>();

// Register repositories
builder.Services.AddScoped<GitConnectionRepository>();
builder.Services.AddScoped<SettingsRepository>();
builder.Services.AddScoped(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetRequiredService<ILogger<AuthRepository>>();
    var connectionString = config.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection not configured");
    return new AuthRepository(connectionString, logger);
});
builder.Services.AddScoped(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetRequiredService<ILogger<NotificationRepository>>();
    var connectionString = config.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection not configured");
    return new NotificationRepository(connectionString, logger);
});
builder.Services.AddScoped(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetRequiredService<ILogger<UserRepository>>();
    var connectionString = config.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection not configured");
    return new UserRepository(connectionString, logger);
});

// Register services
builder.Services.AddScoped<UserApprovalService>();
builder.Services.AddScoped(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var notificationRepo = provider.GetRequiredService<NotificationRepository>();
    var cache = provider.GetRequiredService<IMemoryCache>();
    var logger = provider.GetRequiredService<ILogger<NotificationService>>();
    var connectionString = config.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection not configured");
    return new NotificationService(notificationRepo, cache, logger, connectionString);
});

// Register database configuration service
builder.Services.AddScoped<IDatabaseConfigurationService, DatabaseConfigurationService>();

// Get configuration from database with fallback to appsettings.json
var settingsRepo = new SettingsRepository(builder.Configuration);
var dbConfig = new DatabaseConfigurationService(settingsRepo, builder.Configuration);

// Check if Azure AD is enabled
var azureAdEnabled = dbConfig.GetAzureAdEnabled();

// JWT Auth
var jwtKey = dbConfig.GetJwtKey();
var jwtIssuer = dbConfig.GetJwtIssuer();
var jwtAudience = dbConfig.GetJwtAudience();

if (!string.IsNullOrWhiteSpace(jwtKey))
{
    var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

    // Add Azure AD authentication if enabled
    if (azureAdEnabled)
    {
        // Create Azure AD configuration section from database values
        var azureAdConfig = new Dictionary<string, string>
        {
            ["Instance"] = dbConfig.GetAzureAdInstance(),
            ["TenantId"] = dbConfig.GetAzureAdTenantId(),
            ["ClientId"] = dbConfig.GetAzureAdClientId(),
            ["ClientSecret"] = dbConfig.GetAzureAdClientSecret(),
            ["CallbackPath"] = dbConfig.GetAzureAdCallbackPath()
        };

        // Create in-memory configuration section
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(azureAdConfig.Where(kvp => !string.IsNullOrEmpty(kvp.Value)).Select(kvp => new KeyValuePair<string, string?>(kvp.Key, kvp.Value)));
        var azureAdSection = configBuilder.Build();

        // Use a different scheme name to avoid conflict with JWT Bearer
        authBuilder.AddMicrosoftIdentityWebApi(
            options => azureAdSection.Bind(options),
            options => azureAdSection.Bind(options),
            "AzureAdBearer");
    }
}

// Register BitbucketConfig as scoped service that reads from database
builder.Services.AddScoped<BitbucketConfig>(serviceProvider =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var gitConnectionRepo = serviceProvider.GetRequiredService<GitConnectionRepository>();
    var gitConnection = gitConnectionRepo.GetActiveBitbucketConnectionAsync().GetAwaiter().GetResult();

    if (gitConnection == null)
    {
        // Fallback to appsettings.json if no database connection configured
        return new BitbucketConfig
        {
            DbConnectionString = config.GetConnectionString("DefaultConnection"),
            ApiBaseUrl = config["Bitbucket:ApiBaseUrl"],
            ConsumerKey = config["Bitbucket:ConsumerKey"],
            ConsumerSecret = config["Bitbucket:ConsumerSecret"]
        };
    }

    return new BitbucketConfig
    {
        DbConnectionString = config.GetConnectionString("DefaultConnection"),
        ApiBaseUrl = gitConnection.ApiBaseUrl,
        ConsumerKey = gitConnection.ConsumerKey,
        ConsumerSecret = gitConnection.ConsumerSecret
    };
});

// The ApiClient must be a singleton to manage the lifecycle of the access token
// Use database connection, fallback to appsettings if no active connection found
builder.Services.AddSingleton<BitbucketApiClient>(serviceProvider =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = config.GetConnectionString("DefaultConnection");

    // Try to get active Bitbucket connection from database
    var gitConnectionRepo = new GitConnectionRepository(config);
    var gitConnection = gitConnectionRepo.GetActiveBitbucketConnectionAsync().GetAwaiter().GetResult();

    BitbucketConfig bitbucketConfig;
    if (gitConnection != null)
    {
        // Use database connection
        bitbucketConfig = new BitbucketConfig
        {
            DbConnectionString = connectionString,
            ApiBaseUrl = gitConnection.ApiBaseUrl,
            ConsumerKey = gitConnection.ConsumerKey,
            ConsumerSecret = gitConnection.ConsumerSecret
        };
    }
    else
    {
        // Fallback to appsettings.json
        bitbucketConfig = new BitbucketConfig
        {
            DbConnectionString = connectionString,
            ApiBaseUrl = config["Bitbucket:ApiBaseUrl"],
            ConsumerKey = config["Bitbucket:ConsumerKey"],
            ConsumerSecret = config["Bitbucket:ConsumerSecret"]
        };
    }

    return new BitbucketApiClient(bitbucketConfig);
});
builder.Services.AddScoped<BitbucketUsersService>();
builder.Services.AddScoped<BitbucketRepositoriesService>();
builder.Services.AddScoped<BitbucketCommitsService>();
builder.Services.AddScoped<BitbucketPullRequestsService>();
builder.Services.AddScoped<AnalyticsService>();
// Use DatabaseFileClassificationService for API project to read from database
builder.Services.AddScoped<FileClassificationService, Integration.Utils.DatabaseFileClassificationService>();
builder.Services.AddScoped<DiffParserService>();
builder.Services.AddScoped<CommitRefreshService>(); // Register the new service

// Add CORS for Blazor web app
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorApp", policy =>
    {
        policy.WithOrigins("http://localhost:5084", "https://localhost:7051")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowBlazorApp");

// AuthN/AuthZ
if (!string.IsNullOrWhiteSpace(jwtKey))
{
    app.UseAuthentication();
}
app.UseAuthorization();

app.MapControllers();

// Map custom endpoints
app.MapGitConnectionEndpoints();
app.MapSettingsEndpoints();
app.MapApprovalEndpoints();
app.MapNotificationEndpoints();

app.Run(); 