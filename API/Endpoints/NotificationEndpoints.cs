using API.Services;
using Data.Models;
using Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Endpoints
{
    public static class NotificationEndpoints
    {
        public static void MapNotificationEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/notifications")
                .WithTags("Notifications")
                .RequireAuthorization();

            // Get notification data for a specific menu item
            group.MapGet("/{menuItemKey}", async (
                string menuItemKey,
                HttpContext httpContext,
                NotificationService notificationService) =>
            {
                var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int? userId = null;
                if (int.TryParse(userIdClaim, out var parsedUserId))
                {
                    userId = parsedUserId;
                }

                var data = await notificationService.GetNotificationDataAsync(menuItemKey, userId);
                return Results.Ok(data);
            })
            .WithName("GetNotificationData")
            .WithSummary("Get notification data for a specific menu item");

            // Get all notifications for the current user
            group.MapGet("/all", async (
                HttpContext httpContext,
                NotificationService notificationService) =>
            {
                var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out var userId))
                {
                    return Results.BadRequest("Invalid user");
                }

                var userRole = httpContext.User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
                var notifications = await notificationService.GetAllNotificationsAsync(userId, userRole);
                return Results.Ok(notifications);
            })
            .WithName("GetAllNotifications")
            .WithSummary("Get all notifications for the current user");

            // Mark notification as viewed
            group.MapPost("/{menuItemKey}/mark-viewed", async (
                string menuItemKey,
                HttpContext httpContext,
                NotificationService notificationService) =>
            {
                var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out var userId))
                {
                    return Results.BadRequest("Invalid user");
                }

                await notificationService.MarkAsViewedAsync(userId, menuItemKey);
                return Results.Ok(new { Message = "Notification marked as viewed" });
            })
            .WithName("MarkNotificationAsViewed")
            .WithSummary("Mark a notification as viewed");

            // Admin endpoints for managing notification configurations
            var adminGroup = group.MapGroup("/config")
                .RequireAuthorization(policy => policy.RequireRole("Admin"));

            // Get all notification configurations (Admin only)
            adminGroup.MapGet("/", async (
                NotificationRepository notificationRepository) =>
            {
                var configs = await notificationRepository.GetActiveNotificationConfigsAsync();
                return Results.Ok(configs);
            })
            .WithName("GetNotificationConfigs")
            .WithSummary("Get all notification configurations");

            // Create or update notification configuration (Admin only)
            adminGroup.MapPost("/", async (
                [FromBody] NotificationConfig config,
                NotificationService notificationService) =>
            {
                var result = await notificationService.CreateOrUpdateNotificationConfigAsync(config);
                if (!result)
                {
                    return Results.BadRequest("Failed to save notification configuration");
                }

                return Results.Ok(new { Message = "Notification configuration saved successfully" });
            })
            .WithName("SaveNotificationConfig")
            .WithSummary("Create or update notification configuration");

            // Delete notification configuration (Admin only)
            adminGroup.MapDelete("/{menuItemKey}", async (
                string menuItemKey,
                NotificationRepository notificationRepository) =>
            {
                var result = await notificationRepository.DeleteNotificationConfigAsync(menuItemKey);
                if (!result)
                {
                    return Results.NotFound("Notification configuration not found");
                }

                return Results.Ok(new { Message = "Notification configuration deleted successfully" });
            })
            .WithName("DeleteNotificationConfig")
            .WithSummary("Delete notification configuration");
        }
    }
}