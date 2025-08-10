/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using Data.Models;
using API.Services;
using Integration.Commits;
using Integration.Common;
using Integration.PullRequests;
using Integration.Repositories;
using Integration.Users;
using Integration.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// Set Dapper default command timeout
Dapper.SqlMapper.Settings.CommandTimeout = 300; // 5 minutes

var builder = WebApplication.CreateBuilder(args);
// JWT Auth
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "devview-api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "devview-api";
if (!string.IsNullOrWhiteSpace(jwtKey))
{
    builder.Services.AddAuthentication(options =>
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
}

// 1. Create BitbucketConfig from appsettings.json
var bitbucketConfig = new BitbucketConfig
{
    DbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection"),
    ApiBaseUrl = builder.Configuration["Bitbucket:ApiBaseUrl"],
    ConsumerKey = builder.Configuration["Bitbucket:ConsumerKey"],
    ConsumerSecret = builder.Configuration["Bitbucket:ConsumerSecret"]
};

// 2. Register config and services for Dependency Injection
builder.Services.AddSingleton(bitbucketConfig);
// The ApiClient must be a singleton to manage the lifecycle of the access token
builder.Services.AddSingleton<BitbucketApiClient>();
builder.Services.AddScoped<BitbucketUsersService>();
builder.Services.AddScoped<BitbucketRepositoriesService>();
builder.Services.AddScoped<BitbucketCommitsService>();
builder.Services.AddScoped<BitbucketPullRequestsService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<FileClassificationService>();
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

app.Run(); 