using Data.Models;
using Data.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Services
{
    public class UserApprovalService
    {
        private readonly AuthRepository _authRepository;
        private readonly UserRepository _userRepository;
        private readonly ILogger<UserApprovalService> _logger;

        public UserApprovalService(
            AuthRepository authRepository,
            UserRepository userRepository,
            ILogger<UserApprovalService> logger)
        {
            _authRepository = authRepository;
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<List<AuthUser>> GetPendingApprovalsAsync()
        {
            try
            {
                return await _authRepository.GetUsersByApprovalStatusAsync("Pending");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending approvals");
                throw;
            }
        }

        public async Task<AuthUser?> GetUserDetailsAsync(int userId)
        {
            try
            {
                return await _authRepository.GetUserByIdAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user details for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> RequestAccessAsync(int userId, string? requestReason = null, string? team = null)
        {
            try
            {
                var user = await _authRepository.GetUserByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return false;
                }

                if (user.ApprovalStatus != "Pending")
                {
                    _logger.LogInformation("User {UserId} already has approval status: {Status}",
                        userId, user.ApprovalStatus);
                    return false;
                }

                user.RequestedAt = DateTime.UtcNow;
                user.RequestReason = requestReason;
                user.Team = team;

                return await _authRepository.UpdateUserAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting access for user {UserId}", userId);
                throw;
            }
        }

        public async Task<ApprovalResult> ApproveUserAsync(
            int userId,
            int approvedById,
            int? roleId = null,
            int? linkedBitbucketUserId = null,
            string? notes = null)
        {
            try
            {
                var user = await _authRepository.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return new ApprovalResult
                    {
                        Success = false,
                        Message = "User not found"
                    };
                }

                if (user.ApprovalStatus != "Pending")
                {
                    return new ApprovalResult
                    {
                        Success = false,
                        Message = $"User is already {user.ApprovalStatus.ToLower()}"
                    };
                }

                // Verify linked Bitbucket user exists if provided
                if (linkedBitbucketUserId.HasValue)
                {
                    var bitbucketUser = await _userRepository.GetByIdAsync(linkedBitbucketUserId.Value);
                    if (bitbucketUser == null)
                    {
                        return new ApprovalResult
                        {
                            Success = false,
                            Message = "Linked Bitbucket user not found"
                        };
                    }
                }

                // Update user approval status
                user.ApprovalStatus = "Approved";
                user.ApprovedAt = DateTime.UtcNow;
                user.ApprovedBy = approvedById;
                user.LinkedBitbucketUserId = linkedBitbucketUserId;
                user.Notes = notes;
                user.IsActive = true;

                var updateResult = await _authRepository.UpdateUserAsync(user);

                // Assign role if provided
                if (updateResult && roleId.HasValue)
                {
                    await _authRepository.AssignRoleToUserAsync(userId, roleId.Value);
                }

                _logger.LogInformation("User {UserId} approved by {ApprovedById}", userId, approvedById);

                return new ApprovalResult
                {
                    Success = updateResult,
                    Message = updateResult ? "User approved successfully" : "Failed to approve user"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving user {UserId}", userId);
                return new ApprovalResult
                {
                    Success = false,
                    Message = "An error occurred while approving the user"
                };
            }
        }

        public async Task<ApprovalResult> RejectUserAsync(
            int userId,
            int rejectedById,
            string rejectionReason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(rejectionReason))
                {
                    return new ApprovalResult
                    {
                        Success = false,
                        Message = "Rejection reason is required"
                    };
                }

                var user = await _authRepository.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return new ApprovalResult
                    {
                        Success = false,
                        Message = "User not found"
                    };
                }

                if (user.ApprovalStatus != "Pending")
                {
                    return new ApprovalResult
                    {
                        Success = false,
                        Message = $"User is already {user.ApprovalStatus.ToLower()}"
                    };
                }

                // Update user rejection status
                user.ApprovalStatus = "Rejected";
                user.RejectedAt = DateTime.UtcNow;
                user.RejectedBy = rejectedById;
                user.RejectionReason = rejectionReason;
                user.IsActive = false; // Deactivate rejected users

                var updateResult = await _authRepository.UpdateUserAsync(user);

                _logger.LogInformation("User {UserId} rejected by {RejectedById}. Reason: {Reason}",
                    userId, rejectedById, rejectionReason);

                return new ApprovalResult
                {
                    Success = updateResult,
                    Message = updateResult ? "User rejected" : "Failed to reject user"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting user {UserId}", userId);
                return new ApprovalResult
                {
                    Success = false,
                    Message = "An error occurred while rejecting the user"
                };
            }
        }

        public async Task<List<User>> FindSimilarBitbucketUsersAsync(AuthUser authUser)
        {
            try
            {
                var similarUsers = new List<User>();

                // Search by email if available
                if (!string.IsNullOrEmpty(authUser.Email))
                {
                    var emailMatches = await _userRepository.FindByEmailAsync(authUser.Email);
                    similarUsers.AddRange(emailMatches);
                }

                // Search by display name
                if (!string.IsNullOrEmpty(authUser.DisplayName))
                {
                    var nameMatches = await _userRepository.FindByNameAsync(authUser.DisplayName);
                    similarUsers.AddRange(nameMatches);
                }

                // Remove duplicates and return
                return similarUsers.GroupBy(u => u.Id)
                    .Select(g => g.First())
                    .Take(5) // Limit to top 5 suggestions
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding similar Bitbucket users for {UserId}", authUser.Id);
                return new List<User>();
            }
        }

        public async Task<ApprovalStatistics> GetApprovalStatisticsAsync()
        {
            try
            {
                var allUsers = await _authRepository.GetAllUsersAsync();

                return new ApprovalStatistics
                {
                    PendingCount = allUsers.Count(u => u.ApprovalStatus == "Pending"),
                    ApprovedCount = allUsers.Count(u => u.ApprovalStatus == "Approved"),
                    RejectedCount = allUsers.Count(u => u.ApprovalStatus == "Rejected"),
                    AverageApprovalTimeHours = CalculateAverageApprovalTime(allUsers)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting approval statistics");
                throw;
            }
        }

        private double CalculateAverageApprovalTime(List<AuthUser> users)
        {
            var approvedUsers = users.Where(u =>
                u.ApprovalStatus == "Approved" &&
                u.RequestedAt.HasValue &&
                u.ApprovedAt.HasValue).ToList();

            if (!approvedUsers.Any())
                return 0;

            var totalHours = approvedUsers.Sum(u =>
                (u.ApprovedAt!.Value - u.RequestedAt!.Value).TotalHours);

            return Math.Round(totalHours / approvedUsers.Count, 1);
        }
    }

    public class ApprovalResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class ApprovalStatistics
    {
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public double AverageApprovalTimeHours { get; set; }
    }
}