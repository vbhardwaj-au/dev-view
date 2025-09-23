using Data.Models;
using Dapper;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace Data.Repositories
{
    public class AuthRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<AuthRepository> _logger;

        public AuthRepository(string connectionString, ILogger<AuthRepository> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public async Task<AuthUser?> GetUserByIdAsync(int id)
        {
            const string sql = @"
                SELECT u.*, r.*
                FROM AuthUsers u
                LEFT JOIN AuthUserRoles ur ON ur.UserId = u.Id
                LEFT JOIN AuthRoles r ON r.Id = ur.RoleId
                WHERE u.Id = @Id";

            using var connection = new SqlConnection(_connectionString);
            var userDict = new Dictionary<int, AuthUser>();

            var result = await connection.QueryAsync<AuthUser, AuthRole, AuthUser>(
                sql,
                (user, role) =>
                {
                    if (!userDict.TryGetValue(user.Id, out var existingUser))
                    {
                        existingUser = user;
                        existingUser.Roles = new List<AuthRole>();
                        userDict[user.Id] = existingUser;
                    }

                    if (role != null)
                    {
                        existingUser.Roles.Add(role);
                    }

                    return existingUser;
                },
                new { Id = id },
                splitOn: "Id"
            );

            return userDict.Values.FirstOrDefault();
        }

        public async Task<AuthUser?> GetUserByUsernameAsync(string username)
        {
            const string sql = @"
                SELECT u.*, r.*
                FROM AuthUsers u
                LEFT JOIN AuthUserRoles ur ON ur.UserId = u.Id
                LEFT JOIN AuthRoles r ON r.Id = ur.RoleId
                WHERE u.Username = @Username";

            using var connection = new SqlConnection(_connectionString);
            var userDict = new Dictionary<int, AuthUser>();

            var result = await connection.QueryAsync<AuthUser, AuthRole, AuthUser>(
                sql,
                (user, role) =>
                {
                    if (!userDict.TryGetValue(user.Id, out var existingUser))
                    {
                        existingUser = user;
                        existingUser.Roles = new List<AuthRole>();
                        userDict[user.Id] = existingUser;
                    }

                    if (role != null)
                    {
                        existingUser.Roles.Add(role);
                    }

                    return existingUser;
                },
                new { Username = username },
                splitOn: "Id"
            );

            return userDict.Values.FirstOrDefault();
        }

        public async Task<List<AuthUser>> GetUsersByApprovalStatusAsync(string approvalStatus)
        {
            const string sql = @"
                SELECT u.*, r.*,
                       bu.Id as BitbucketUserId, bu.DisplayName as BitbucketDisplayName,
                       bu.AvatarUrl as BitbucketAvatarUrl
                FROM AuthUsers u
                LEFT JOIN AuthUserRoles ur ON ur.UserId = u.Id
                LEFT JOIN AuthRoles r ON r.Id = ur.RoleId
                LEFT JOIN Users bu ON bu.Id = u.LinkedBitbucketUserId
                WHERE u.ApprovalStatus = @ApprovalStatus
                ORDER BY u.RequestedAt DESC";

            using var connection = new SqlConnection(_connectionString);
            var userDict = new Dictionary<int, AuthUser>();

            var result = await connection.QueryAsync<AuthUser, AuthRole, User, AuthUser>(
                sql,
                (user, role, bitbucketUser) =>
                {
                    if (!userDict.TryGetValue(user.Id, out var existingUser))
                    {
                        existingUser = user;
                        existingUser.Roles = new List<AuthRole>();
                        existingUser.LinkedBitbucketUser = bitbucketUser;
                        userDict[user.Id] = existingUser;
                    }

                    if (role != null && !existingUser.Roles.Any(r => r.Id == role.Id))
                    {
                        existingUser.Roles.Add(role);
                    }

                    return existingUser;
                },
                new { ApprovalStatus = approvalStatus },
                splitOn: "Id,BitbucketUserId"
            );

            return userDict.Values.ToList();
        }

        public async Task<List<AuthUser>> GetAllUsersAsync()
        {
            const string sql = @"
                SELECT u.*, r.*
                FROM AuthUsers u
                LEFT JOIN AuthUserRoles ur ON ur.UserId = u.Id
                LEFT JOIN AuthRoles r ON r.Id = ur.RoleId
                ORDER BY u.CreatedOn DESC";

            using var connection = new SqlConnection(_connectionString);
            var userDict = new Dictionary<int, AuthUser>();

            var result = await connection.QueryAsync<AuthUser, AuthRole, AuthUser>(
                sql,
                (user, role) =>
                {
                    if (!userDict.TryGetValue(user.Id, out var existingUser))
                    {
                        existingUser = user;
                        existingUser.Roles = new List<AuthRole>();
                        userDict[user.Id] = existingUser;
                    }

                    if (role != null)
                    {
                        existingUser.Roles.Add(role);
                    }

                    return existingUser;
                },
                splitOn: "Id"
            );

            return userDict.Values.ToList();
        }

        public async Task<bool> UpdateUserAsync(AuthUser user)
        {
            const string sql = @"
                UPDATE AuthUsers
                SET Username = @Username,
                    DisplayName = @DisplayName,
                    Email = @Email,
                    JobTitle = @JobTitle,
                    Department = @Department,
                    AuthProvider = @AuthProvider,
                    AzureAdObjectId = @AzureAdObjectId,
                    IsActive = @IsActive,
                    ModifiedOn = GETUTCDATE(),
                    ApprovalStatus = @ApprovalStatus,
                    RequestedAt = @RequestedAt,
                    ApprovedAt = @ApprovedAt,
                    ApprovedBy = @ApprovedBy,
                    RejectedAt = @RejectedAt,
                    RejectedBy = @RejectedBy,
                    RejectionReason = @RejectionReason,
                    LinkedBitbucketUserId = @LinkedBitbucketUserId,
                    Notes = @Notes,
                    RequestReason = @RequestReason,
                    Team = @Team
                WHERE Id = @Id";

            using var connection = new SqlConnection(_connectionString);
            var affected = await connection.ExecuteAsync(sql, user);
            return affected > 0;
        }

        public async Task<int> CreateUserAsync(AuthUser user)
        {
            const string sql = @"
                INSERT INTO AuthUsers
                (Username, PasswordHash, PasswordSalt, DisplayName, Email, JobTitle, Department,
                 AuthProvider, AzureAdObjectId, IsActive, CreatedOn, ApprovalStatus,
                 RequestedAt, RequestReason, Team)
                VALUES
                (@Username, @PasswordHash, @PasswordSalt, @DisplayName, @Email, @JobTitle, @Department,
                 @AuthProvider, @AzureAdObjectId, @IsActive, GETUTCDATE(), @ApprovalStatus,
                 @RequestedAt, @RequestReason, @Team);

                SELECT CAST(SCOPE_IDENTITY() as int);";

            using var connection = new SqlConnection(_connectionString);
            var userId = await connection.QuerySingleAsync<int>(sql, user);
            return userId;
        }

        public async Task<bool> AssignRoleToUserAsync(int userId, int roleId)
        {
            const string sql = @"
                IF NOT EXISTS (SELECT 1 FROM AuthUserRoles WHERE UserId = @UserId AND RoleId = @RoleId)
                BEGIN
                    INSERT INTO AuthUserRoles (UserId, RoleId)
                    VALUES (@UserId, @RoleId)
                END";

            using var connection = new SqlConnection(_connectionString);
            var affected = await connection.ExecuteAsync(sql, new { UserId = userId, RoleId = roleId });
            return affected > 0;
        }

        public async Task<List<AuthRole>> GetRolesAsync()
        {
            const string sql = "SELECT * FROM AuthRoles ORDER BY Name";

            using var connection = new SqlConnection(_connectionString);
            var roles = await connection.QueryAsync<AuthRole>(sql);
            return roles.ToList();
        }

        public async Task<AuthRole?> GetRoleByNameAsync(string roleName)
        {
            const string sql = "SELECT * FROM AuthRoles WHERE Name = @Name";

            using var connection = new SqlConnection(_connectionString);
            var role = await connection.QuerySingleOrDefaultAsync<AuthRole>(sql, new { Name = roleName });
            return role;
        }
    }
}