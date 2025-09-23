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
    public class NotificationRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<NotificationRepository> _logger;

        public NotificationRepository(string connectionString, ILogger<NotificationRepository> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public async Task<NotificationConfig?> GetNotificationConfigAsync(string menuItemKey)
        {
            const string sql = @"
                SELECT * FROM NotificationConfig
                WHERE MenuItemKey = @MenuItemKey";

            using var connection = new SqlConnection(_connectionString);
            var config = await connection.QuerySingleOrDefaultAsync<NotificationConfig>(sql,
                new { MenuItemKey = menuItemKey });
            return config;
        }

        public async Task<List<NotificationConfig>> GetActiveNotificationConfigsAsync()
        {
            const string sql = @"
                SELECT * FROM NotificationConfig
                WHERE IsActive = 1
                ORDER BY MenuItemKey";

            using var connection = new SqlConnection(_connectionString);
            var configs = await connection.QueryAsync<NotificationConfig>(sql);
            return configs.ToList();
        }

        public async Task<bool> SaveNotificationConfigAsync(NotificationConfig config)
        {
            const string updateSql = @"
                UPDATE NotificationConfig
                SET DisplayName = @DisplayName,
                    QueryType = @QueryType,
                    Query = @Query,
                    ApiEndpoint = @ApiEndpoint,
                    StaticValue = @StaticValue,
                    RefreshIntervalSeconds = @RefreshIntervalSeconds,
                    DisplayType = @DisplayType,
                    PulseOnNew = @PulseOnNew,
                    MinimumRole = @MinimumRole,
                    IsActive = @IsActive,
                    UpdatedAt = GETUTCDATE()
                WHERE MenuItemKey = @MenuItemKey";

            const string insertSql = @"
                INSERT INTO NotificationConfig
                (MenuItemKey, DisplayName, QueryType, Query, ApiEndpoint, StaticValue,
                 RefreshIntervalSeconds, DisplayType, PulseOnNew, MinimumRole, IsActive,
                 CreatedAt)
                VALUES
                (@MenuItemKey, @DisplayName, @QueryType, @Query, @ApiEndpoint, @StaticValue,
                 @RefreshIntervalSeconds, @DisplayType, @PulseOnNew, @MinimumRole, @IsActive,
                 GETUTCDATE())";

            using var connection = new SqlConnection(_connectionString);

            // Check if config exists
            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM NotificationConfig WHERE MenuItemKey = @MenuItemKey",
                new { config.MenuItemKey }) > 0;

            int affected;
            if (exists)
            {
                affected = await connection.ExecuteAsync(updateSql, config);
            }
            else
            {
                affected = await connection.ExecuteAsync(insertSql, config);
            }

            return affected > 0;
        }

        public async Task<NotificationState?> GetNotificationStateAsync(int userId, string menuItemKey)
        {
            const string sql = @"
                SELECT * FROM NotificationState
                WHERE UserId = @UserId AND MenuItemKey = @MenuItemKey";

            using var connection = new SqlConnection(_connectionString);
            var state = await connection.QuerySingleOrDefaultAsync<NotificationState>(sql,
                new { UserId = userId, MenuItemKey = menuItemKey });
            return state;
        }

        public async Task<bool> UpdateNotificationStateAsync(int userId, string menuItemKey, int viewedCount)
        {
            const string updateSql = @"
                UPDATE NotificationState
                SET LastViewedCount = @ViewedCount,
                    LastViewedAt = GETUTCDATE(),
                    LastCheckedAt = GETUTCDATE()
                WHERE UserId = @UserId AND MenuItemKey = @MenuItemKey";

            const string insertSql = @"
                INSERT INTO NotificationState
                (UserId, MenuItemKey, LastViewedCount, LastViewedAt, LastCheckedAt)
                VALUES
                (@UserId, @MenuItemKey, @ViewedCount, GETUTCDATE(), GETUTCDATE())";

            using var connection = new SqlConnection(_connectionString);

            // Check if state exists
            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM NotificationState WHERE UserId = @UserId AND MenuItemKey = @MenuItemKey",
                new { UserId = userId, MenuItemKey = menuItemKey }) > 0;

            int affected;
            if (exists)
            {
                affected = await connection.ExecuteAsync(updateSql,
                    new { UserId = userId, MenuItemKey = menuItemKey, ViewedCount = viewedCount });
            }
            else
            {
                affected = await connection.ExecuteAsync(insertSql,
                    new { UserId = userId, MenuItemKey = menuItemKey, ViewedCount = viewedCount });
            }

            return affected > 0;
        }

        public async Task<Dictionary<string, NotificationState>> GetUserNotificationStatesAsync(int userId)
        {
            const string sql = @"
                SELECT * FROM NotificationState
                WHERE UserId = @UserId";

            using var connection = new SqlConnection(_connectionString);
            var states = await connection.QueryAsync<NotificationState>(sql, new { UserId = userId });
            return states.ToDictionary(s => s.MenuItemKey);
        }

        public async Task<bool> DeleteNotificationConfigAsync(string menuItemKey)
        {
            const string sql = @"
                DELETE FROM NotificationConfig
                WHERE MenuItemKey = @MenuItemKey";

            using var connection = new SqlConnection(_connectionString);
            var affected = await connection.ExecuteAsync(sql, new { MenuItemKey = menuItemKey });
            return affected > 0;
        }
    }
}