using Data.Models;
using Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Endpoints
{
    public static class GitConnectionEndpoints
    {
        public static void MapGitConnectionEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/gitconnections")
                .RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });

            group.MapGet("/", GetAllConnections)
                .WithName("GetAllGitConnections");

            group.MapGet("/{id:int}", GetConnectionById)
                .WithName("GetGitConnectionById");

            group.MapGet("/active/{serverType}", GetActiveConnection)
                .WithName("GetActiveGitConnection");

            group.MapPost("/", CreateConnection)
                .WithName("CreateGitConnection");

            group.MapPut("/{id:int}", UpdateConnection)
                .WithName("UpdateGitConnection");

            group.MapDelete("/{id:int}", DeleteConnection)
                .WithName("DeleteGitConnection");

            group.MapPatch("/{id:int}/status", UpdateConnectionStatus)
                .WithName("UpdateGitConnectionStatus");

            group.MapPost("/test", TestConnection)
                .WithName("TestGitConnection");
        }

        private static async Task<IResult> GetAllConnections(GitConnectionRepository repository)
        {
            try
            {
                var connections = await repository.GetAllAsync();

                // Mask sensitive fields
                var maskedConnections = connections.Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.GitServerType,
                    c.ApiBaseUrl,
                    ConsumerKey = MaskSensitiveField(c.ConsumerKey),
                    ConsumerSecret = MaskSensitiveField(c.ConsumerSecret),
                    AccessToken = MaskSensitiveField(c.AccessToken),
                    c.Username,
                    Password = MaskSensitiveField(c.Password),
                    PersonalAccessToken = MaskSensitiveField(c.PersonalAccessToken),
                    c.IsActive,
                    c.Priority,
                    c.Workspace,
                    c.CreatedAt,
                    c.UpdatedAt,
                    c.CreatedBy,
                    c.UpdatedBy,
                    c.AdditionalSettings
                });

                return Results.Ok(maskedConnections);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error retrieving git connections: {ex.Message}");
            }
        }

        private static async Task<IResult> GetConnectionById(int id, GitConnectionRepository repository)
        {
            try
            {
                var connection = await repository.GetByIdAsync(id);
                if (connection == null)
                {
                    return Results.NotFound($"Git connection with ID {id} not found.");
                }

                // Mask sensitive fields
                var maskedConnection = new
                {
                    connection.Id,
                    connection.Name,
                    connection.GitServerType,
                    connection.ApiBaseUrl,
                    ConsumerKey = MaskSensitiveField(connection.ConsumerKey),
                    ConsumerSecret = MaskSensitiveField(connection.ConsumerSecret),
                    AccessToken = MaskSensitiveField(connection.AccessToken),
                    connection.Username,
                    Password = MaskSensitiveField(connection.Password),
                    PersonalAccessToken = MaskSensitiveField(connection.PersonalAccessToken),
                    connection.IsActive,
                    connection.Priority,
                    connection.Workspace,
                    connection.CreatedAt,
                    connection.UpdatedAt,
                    connection.CreatedBy,
                    connection.UpdatedBy,
                    connection.AdditionalSettings
                };

                return Results.Ok(maskedConnection);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error retrieving git connection: {ex.Message}");
            }
        }

        private static async Task<IResult> GetActiveConnection(string serverType, GitConnectionRepository repository)
        {
            try
            {
                var connection = await repository.GetActiveConnectionAsync(serverType);
                if (connection == null)
                {
                    return Results.NotFound($"No active git connection found for server type {serverType}.");
                }

                // Mask sensitive fields
                var maskedConnection = new
                {
                    connection.Id,
                    connection.Name,
                    connection.GitServerType,
                    connection.ApiBaseUrl,
                    ConsumerKey = MaskSensitiveField(connection.ConsumerKey),
                    ConsumerSecret = MaskSensitiveField(connection.ConsumerSecret),
                    AccessToken = MaskSensitiveField(connection.AccessToken),
                    connection.Username,
                    Password = MaskSensitiveField(connection.Password),
                    PersonalAccessToken = MaskSensitiveField(connection.PersonalAccessToken),
                    connection.IsActive,
                    connection.Priority,
                    connection.Workspace,
                    connection.CreatedAt,
                    connection.UpdatedAt,
                    connection.CreatedBy,
                    connection.UpdatedBy,
                    connection.AdditionalSettings
                };

                return Results.Ok(maskedConnection);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error retrieving active git connection: {ex.Message}");
            }
        }

        private static async Task<IResult> CreateConnection(
            [FromBody] GitConnection connection,
            GitConnectionRepository repository,
            ClaimsPrincipal user)
        {
            try
            {
                connection.CreatedBy = user.Identity?.Name ?? "System";
                connection.UpdatedBy = connection.CreatedBy;
                connection.CreatedAt = DateTime.UtcNow;
                connection.UpdatedAt = DateTime.UtcNow;

                var id = await repository.CreateAsync(connection);
                connection.Id = id;

                return Results.Created($"/api/gitconnections/{id}", new { id });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error creating git connection: {ex.Message}");
            }
        }

        private static async Task<IResult> UpdateConnection(
            int id,
            [FromBody] GitConnection connection,
            GitConnectionRepository repository,
            ClaimsPrincipal user)
        {
            try
            {
                // Get the existing connection to preserve masked fields
                var existing = await repository.GetByIdAsync(id);
                if (existing == null)
                {
                    return Results.NotFound($"Git connection with ID {id} not found.");
                }

                connection.Id = id;
                connection.UpdatedBy = user.Identity?.Name ?? "System";

                // Preserve existing sensitive fields if the new values are masked
                if (IsMaskedValue(connection.ConsumerKey))
                {
                    connection.ConsumerKey = existing.ConsumerKey;
                }
                if (IsMaskedValue(connection.ConsumerSecret))
                {
                    connection.ConsumerSecret = existing.ConsumerSecret;
                }
                if (IsMaskedValue(connection.AccessToken))
                {
                    connection.AccessToken = existing.AccessToken;
                }
                if (IsMaskedValue(connection.Password))
                {
                    connection.Password = existing.Password;
                }
                if (IsMaskedValue(connection.PersonalAccessToken))
                {
                    connection.PersonalAccessToken = existing.PersonalAccessToken;
                }

                var success = await repository.UpdateAsync(connection);
                if (!success)
                {
                    return Results.NotFound($"Git connection with ID {id} not found.");
                }

                return Results.Ok(new { message = "Git connection updated successfully." });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error updating git connection: {ex.Message}");
            }
        }

        private static async Task<IResult> DeleteConnection(int id, GitConnectionRepository repository)
        {
            try
            {
                var success = await repository.DeleteAsync(id);
                if (!success)
                {
                    return Results.NotFound($"Git connection with ID {id} not found.");
                }

                return Results.Ok(new { message = "Git connection deleted successfully." });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error deleting git connection: {ex.Message}");
            }
        }

        private static async Task<IResult> UpdateConnectionStatus(
            int id,
            [FromBody] UpdateStatusRequest request,
            GitConnectionRepository repository,
            ClaimsPrincipal user)
        {
            try
            {
                var updatedBy = user.Identity?.Name ?? "System";
                var success = await repository.SetActiveStatusAsync(id, request.IsActive, updatedBy);

                if (!success)
                {
                    return Results.NotFound($"Git connection with ID {id} not found.");
                }

                return Results.Ok(new { message = "Git connection status updated successfully." });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error updating git connection status: {ex.Message}");
            }
        }

        private static async Task<IResult> TestConnection([FromBody] GitConnection connection)
        {
            try
            {
                // Basic validation
                if (string.IsNullOrEmpty(connection.ApiBaseUrl))
                {
                    return Results.BadRequest(new { success = false, message = "API Base URL is required." });
                }

                // Test based on server type
                var testResult = connection.GitServerType switch
                {
                    GitServerTypes.BitbucketCloud => await TestBitbucketConnection(connection),
                    _ => new { success = false, message = $"Testing for {connection.GitServerType} is not yet implemented." }
                };

                return Results.Ok(testResult);
            }
            catch (Exception ex)
            {
                return Results.Ok(new { success = false, message = $"Connection test failed: {ex.Message}" });
            }
        }

        private static async Task<object> TestBitbucketConnection(GitConnection connection)
        {
            try
            {
                if (string.IsNullOrEmpty(connection.ConsumerKey) || string.IsNullOrEmpty(connection.ConsumerSecret))
                {
                    return new { success = false, message = "Consumer Key and Consumer Secret are required for Bitbucket Cloud." };
                }

                // Note: Actual API test would go here using the Integration project services
                // For now, we'll just validate that the required fields are present
                return new { success = true, message = "Connection configuration is valid. Full connection test requires Integration layer." };
            }
            catch (Exception ex)
            {
                return new { success = false, message = $"Bitbucket connection test failed: {ex.Message}" };
            }
        }

        private static string MaskSensitiveField(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (value.Length <= 8)
                return new string('*', value.Length);

            return value.Substring(0, 4) + new string('*', Math.Min(value.Length - 8, 20)) + value.Substring(value.Length - 4);
        }

        private static bool IsMaskedValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            // Check if the value contains asterisks in the middle (indicating it's masked)
            return value.Length > 8 && value.Contains("****");
        }

        public class UpdateStatusRequest
        {
            public bool IsActive { get; set; }
        }
    }
}