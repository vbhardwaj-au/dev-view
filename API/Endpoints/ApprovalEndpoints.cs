using API.Services;
using Data.Models;
using Data.Repositories;
using Entities.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Endpoints
{
    public static class ApprovalEndpoints
    {
        public static void MapApprovalEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/approvals")
                .WithTags("Approvals")
                .RequireAuthorization();

            // Get pending approvals (Admin only)
            group.MapGet("/pending", [Authorize(Roles = "Admin")] async (
                UserApprovalService approvalService) =>
            {
                var pendingUsers = await approvalService.GetPendingApprovalsAsync();
                return Results.Ok(pendingUsers);
            })
            .WithName("GetPendingApprovals")
            .WithSummary("Get all pending user approvals")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

            // Get user details for approval
            group.MapGet("/user/{userId}", [Authorize(Roles = "Admin")] async (
                int userId,
                UserApprovalService approvalService) =>
            {
                var user = await approvalService.GetUserDetailsAsync(userId);
                if (user == null)
                {
                    return Results.NotFound("User not found");
                }

                // Get similar Bitbucket users for linking
                var similarUsers = await approvalService.FindSimilarBitbucketUsersAsync(user);

                return Results.Ok(new
                {
                    User = user,
                    SimilarBitbucketUsers = similarUsers
                });
            })
            .WithName("GetUserDetailsForApproval")
            .WithSummary("Get user details and similar Bitbucket users for approval");

            // Request access (for pending users)
            group.MapPost("/request-access", async (
                [FromBody] RequestAccessDto request,
                HttpContext httpContext,
                UserApprovalService approvalService) =>
            {
                var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out var userId))
                {
                    return Results.BadRequest("Invalid user");
                }

                var result = await approvalService.RequestAccessAsync(
                    userId,
                    request.RequestReason,
                    request.Team);

                if (!result)
                {
                    return Results.BadRequest("Failed to submit access request");
                }

                return Results.Ok(new { Message = "Access request submitted successfully" });
            })
            .WithName("RequestAccess")
            .WithSummary("Submit access request with additional details");

            // Approve user (Admin only)
            group.MapPost("/approve/{userId}", [Authorize(Roles = "Admin")] async (
                int userId,
                [FromBody] ApproveUserDto request,
                HttpContext httpContext,
                UserApprovalService approvalService,
                AuthRepository authRepository) =>
            {
                // Try to get user ID from claims - prefer userId claim first
                var approverIdClaim = httpContext.User.FindFirst("userId")?.Value;

                // If not found, try NameIdentifier but only if it's numeric
                if (string.IsNullOrEmpty(approverIdClaim))
                {
                    var nameIdClaims = httpContext.User.FindAll(ClaimTypes.NameIdentifier);
                    foreach (var claim in nameIdClaims)
                    {
                        if (int.TryParse(claim.Value, out _))
                        {
                            approverIdClaim = claim.Value;
                            break;
                        }
                    }
                }

                if (!int.TryParse(approverIdClaim, out var approverId))
                {
                    // Log available claims for debugging
                    var availableClaims = string.Join(", ", httpContext.User.Claims.Select(c => $"{c.Type}={c.Value}"));
                    return Results.BadRequest($"Invalid approver. Available claims: {availableClaims}");
                }

                // Get role ID if role name provided, default to "User" role if not specified
                int? roleId = null;
                var roleName = !string.IsNullOrEmpty(request.RoleName) ? request.RoleName : "User";
                var role = await authRepository.GetRoleByNameAsync(roleName);
                roleId = role?.Id;

                if (!roleId.HasValue)
                {
                    // If User role doesn't exist, try to create it or log error
                    return Results.BadRequest($"Role '{roleName}' not found in database");
                }

                var result = await approvalService.ApproveUserAsync(
                    userId,
                    approverId,
                    roleId,
                    request.LinkedBitbucketUserId,
                    request.Notes);

                if (!result.Success)
                {
                    return Results.BadRequest(result.Message);
                }

                return Results.Ok(new { Message = result.Message });
            })
            .WithName("ApproveUser")
            .WithSummary("Approve a pending user");

            // Reject user (Admin only)
            group.MapPost("/reject/{userId}", [Authorize(Roles = "Admin")] async (
                int userId,
                [FromBody] RejectUserDto request,
                HttpContext httpContext,
                UserApprovalService approvalService) =>
            {
                var rejectorIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(rejectorIdClaim, out var rejectorId))
                {
                    return Results.BadRequest("Invalid rejector");
                }

                if (string.IsNullOrWhiteSpace(request.RejectionReason))
                {
                    return Results.BadRequest("Rejection reason is required");
                }

                var result = await approvalService.RejectUserAsync(
                    userId,
                    rejectorId,
                    request.RejectionReason);

                if (!result.Success)
                {
                    return Results.BadRequest(result.Message);
                }

                return Results.Ok(new { Message = result.Message });
            })
            .WithName("RejectUser")
            .WithSummary("Reject a pending user");

            // Get approval statistics (Admin only)
            group.MapGet("/statistics", [Authorize(Roles = "Admin")] async (
                UserApprovalService approvalService) =>
            {
                var stats = await approvalService.GetApprovalStatisticsAsync();
                return Results.Ok(stats);
            })
            .WithName("GetApprovalStatistics")
            .WithSummary("Get approval statistics");

            // Get current user approval status
            group.MapGet("/my-status", async (
                HttpContext httpContext,
                UserApprovalService approvalService) =>
            {
                var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(userIdClaim, out var userId))
                {
                    return Results.BadRequest("Invalid user");
                }

                var user = await approvalService.GetUserDetailsAsync(userId);
                if (user == null)
                {
                    return Results.NotFound("User not found");
                }

                return Results.Ok(new
                {
                    ApprovalStatus = user.ApprovalStatus,
                    RequestedAt = user.RequestedAt,
                    ApprovedAt = user.ApprovedAt,
                    RejectedAt = user.RejectedAt,
                    RejectionReason = user.RejectionReason
                });
            })
            .WithName("GetMyApprovalStatus")
            .WithSummary("Get current user's approval status");
        }
    }

    // DTOs for approval endpoints
    public class RequestAccessDto
    {
        public string? RequestReason { get; set; }
        public string? Team { get; set; }
    }

    public class ApproveUserDto
    {
        public string? RoleName { get; set; }
        public int? LinkedBitbucketUserId { get; set; }
        public string? Notes { get; set; }
        public int? TeamId { get; set; } // Add team assignment support
    }

    public class RejectUserDto
    {
        public string RejectionReason { get; set; } = string.Empty;
    }
}