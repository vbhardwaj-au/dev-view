using Data.Models;
using Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace API.Endpoints
{
    public static class SettingsEndpoints
    {
        public static void MapSettingsEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/settings")
                .RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });

            // Get all settings
            group.MapGet("/", GetAllSettings)
                .WithName("GetAllSettings");

            // Get settings by category
            group.MapGet("/category/{category}", GetSettingsByCategory)
                .WithName("GetSettingsByCategory");

            // Get single setting
            group.MapGet("/{id:int}", GetSettingById)
                .WithName("GetSettingById");

            // Get single setting value
            group.MapGet("/{category}/{key}", GetSettingValue)
                .WithName("GetSettingValue");

            // Get file classification config
            group.MapGet("/file-classification", GetFileClassificationConfig)
                .WithName("GetFileClassificationConfig")
                .RequireAuthorization(); // Allow all authenticated users to read

            // Create setting
            group.MapPost("/", CreateSetting)
                .WithName("CreateSetting");

            // Update setting
            group.MapPut("/{id:int}", UpdateSetting)
                .WithName("UpdateSetting");

            // Update setting value only
            group.MapPatch("/{category}/{key}", UpdateSettingValue)
                .WithName("UpdateSettingValue");

            // Delete setting
            group.MapDelete("/{id:int}", DeleteSetting)
                .WithName("DeleteSetting");

            // Toggle active state
            group.MapPatch("/{id:int}/active", ToggleActive)
                .WithName("ToggleSettingActive");

            // Import settings from JSON
            group.MapPost("/import", ImportSettings)
                .WithName("ImportSettings");

            // Export settings to JSON
            group.MapGet("/export/{category?}", ExportSettings)
                .WithName("ExportSettings");
        }

        private static async Task<IResult> GetAllSettings(SettingsRepository repository)
        {
            try
            {
                var settings = await repository.GetAllAsync();
                return Results.Ok(settings);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error retrieving settings: {ex.Message}");
            }
        }

        private static async Task<IResult> GetSettingsByCategory(string category, SettingsRepository repository)
        {
            try
            {
                var settings = await repository.GetByCategoryAsync(category);
                return Results.Ok(settings);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error retrieving settings for category {category}: {ex.Message}");
            }
        }

        private static async Task<IResult> GetSettingById(int id, SettingsRepository repository)
        {
            try
            {
                var setting = await repository.GetByIdAsync(id);
                if (setting == null)
                    return Results.NotFound($"Setting with ID {id} not found.");

                return Results.Ok(setting);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error retrieving setting: {ex.Message}");
            }
        }

        private static async Task<IResult> GetSettingValue(string category, string key, SettingsRepository repository)
        {
            try
            {
                var value = await repository.GetValueAsync(category, key);
                if (value == null)
                    return Results.NotFound($"Setting {category}.{key} not found.");

                return Results.Ok(new { category, key, value });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error retrieving setting value: {ex.Message}");
            }
        }

        private static async Task<IResult> GetFileClassificationConfig(SettingsRepository repository)
        {
            try
            {
                var config = await repository.GetFileClassificationConfigAsync();
                return Results.Ok(config);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error retrieving file classification config: {ex.Message}");
            }
        }

        private static async Task<IResult> CreateSetting(
            [FromBody] Setting setting,
            SettingsRepository repository,
            ClaimsPrincipal user)
        {
            try
            {
                setting.CreatedBy = user.Identity?.Name ?? "System";
                setting.UpdatedBy = setting.CreatedBy;
                setting.CreatedAt = DateTime.UtcNow;
                setting.UpdatedAt = DateTime.UtcNow;

                var id = await repository.CreateAsync(setting);
                setting.Id = id;

                return Results.Created($"/api/settings/{id}", setting);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error creating setting: {ex.Message}");
            }
        }

        private static async Task<IResult> UpdateSetting(
            int id,
            [FromBody] Setting setting,
            SettingsRepository repository,
            ClaimsPrincipal user)
        {
            try
            {
                setting.Id = id;
                setting.UpdatedBy = user.Identity?.Name ?? "System";

                var success = await repository.UpdateAsync(setting);
                if (!success)
                    return Results.NotFound($"Setting with ID {id} not found.");

                return Results.Ok(new { message = "Setting updated successfully." });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error updating setting: {ex.Message}");
            }
        }

        private static async Task<IResult> UpdateSettingValue(
            string category,
            string key,
            [FromBody] UpdateValueRequest request,
            SettingsRepository repository,
            ClaimsPrincipal user)
        {
            try
            {
                var updatedBy = user.Identity?.Name ?? "System";
                var success = await repository.UpdateValueAsync(category, key, request.Value, updatedBy);

                if (!success)
                    return Results.NotFound($"Setting {category}.{key} not found.");

                return Results.Ok(new { message = "Setting value updated successfully." });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error updating setting value: {ex.Message}");
            }
        }

        private static async Task<IResult> DeleteSetting(int id, SettingsRepository repository)
        {
            try
            {
                var success = await repository.DeleteAsync(id);
                if (!success)
                    return Results.NotFound($"Setting with ID {id} not found or is a system setting.");

                return Results.Ok(new { message = "Setting deleted successfully." });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error deleting setting: {ex.Message}");
            }
        }

        private static async Task<IResult> ToggleActive(
            int id,
            [FromBody] ToggleActiveRequest request,
            SettingsRepository repository,
            ClaimsPrincipal user)
        {
            try
            {
                var updatedBy = user.Identity?.Name ?? "System";
                var success = await repository.SetActiveAsync(id, request.IsActive, updatedBy);

                if (!success)
                    return Results.NotFound($"Setting with ID {id} not found.");

                return Results.Ok(new { message = $"Setting {(request.IsActive ? "activated" : "deactivated")} successfully." });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error toggling setting active state: {ex.Message}");
            }
        }

        private static async Task<IResult> ImportSettings(
            [FromBody] ImportSettingsRequest request,
            SettingsRepository repository,
            ClaimsPrincipal user)
        {
            try
            {
                var updatedBy = user.Identity?.Name ?? "System";
                var imported = 0;
                var updated = 0;

                foreach (var setting in request.Settings)
                {
                    setting.UpdatedBy = updatedBy;
                    var existing = await repository.GetSettingAsync(setting.Category, setting.Key);

                    if (existing != null)
                    {
                        setting.Id = existing.Id;
                        if (await repository.UpdateAsync(setting))
                            updated++;
                    }
                    else
                    {
                        setting.CreatedBy = updatedBy;
                        await repository.CreateAsync(setting);
                        imported++;
                    }
                }

                return Results.Ok(new { imported, updated, total = imported + updated });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error importing settings: {ex.Message}");
            }
        }

        private static async Task<IResult> ExportSettings(string category, SettingsRepository repository)
        {
            try
            {
                IEnumerable<Setting> settings;

                if (string.IsNullOrEmpty(category))
                    settings = await repository.GetAllAsync();
                else
                    settings = await repository.GetByCategoryAsync(category);

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return Results.Ok(new { settings, json });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error exporting settings: {ex.Message}");
            }
        }

        public class UpdateValueRequest
        {
            public string Value { get; set; }
        }

        public class ToggleActiveRequest
        {
            public bool IsActive { get; set; }
        }

        public class ImportSettingsRequest
        {
            public List<Setting> Settings { get; set; }
        }
    }
}