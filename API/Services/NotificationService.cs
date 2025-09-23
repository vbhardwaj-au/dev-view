using Data.Models;
using Data.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace API.Services
{
    public class NotificationService
    {
        private readonly NotificationRepository _notificationRepository;
        private readonly IMemoryCache _cache;
        private readonly ILogger<NotificationService> _logger;
        private readonly string _connectionString;

        public NotificationService(
            NotificationRepository notificationRepository,
            IMemoryCache cache,
            ILogger<NotificationService> logger,
            string connectionString)
        {
            _notificationRepository = notificationRepository;
            _cache = cache;
            _logger = logger;
            _connectionString = connectionString;
        }

        public async Task<NotificationData> GetNotificationDataAsync(string menuItemKey, int? userId = null)
        {
            try
            {
                // Get notification config
                var config = await GetNotificationConfigAsync(menuItemKey);
                if (config == null || !config.IsActive)
                {
                    return new NotificationData { Count = 0, IsNew = false };
                }

                // Get current count based on query type
                int currentCount = config.QueryType switch
                {
                    "SQL" => await ExecuteSqlQueryAsync(config.Query ?? string.Empty),
                    "API" => await ExecuteApiQueryAsync(config.ApiEndpoint ?? string.Empty),
                    "Static" => config.StaticValue ?? 0,
                    _ => 0
                };

                // Check if this is new for the user
                bool isNew = false;
                if (userId.HasValue)
                {
                    var state = await _notificationRepository.GetNotificationStateAsync(userId.Value, menuItemKey);
                    isNew = state == null || state.LastViewedCount < currentCount;
                }

                return new NotificationData
                {
                    Count = currentCount,
                    IsNew = isNew,
                    DisplayType = config.DisplayType,
                    PulseOnNew = config.PulseOnNew
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notification data for {MenuItemKey}", menuItemKey);
                return new NotificationData { Count = 0, IsNew = false };
            }
        }

        public async Task<Dictionary<string, NotificationData>> GetAllNotificationsAsync(int userId, string userRole)
        {
            try
            {
                var notifications = new Dictionary<string, NotificationData>();

                // Get all active notification configs for user's role
                var configs = await _notificationRepository.GetActiveNotificationConfigsAsync();

                foreach (var config in configs)
                {
                    // Check if user has required role
                    if (!string.IsNullOrEmpty(config.MinimumRole) &&
                        !HasRequiredRole(userRole, config.MinimumRole))
                    {
                        continue;
                    }

                    var data = await GetNotificationDataAsync(config.MenuItemKey, userId);
                    notifications[config.MenuItemKey] = data;
                }

                return notifications;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all notifications for user {UserId}", userId);
                return new Dictionary<string, NotificationData>();
            }
        }

        public async Task MarkAsViewedAsync(int userId, string menuItemKey)
        {
            try
            {
                var data = await GetNotificationDataAsync(menuItemKey);
                await _notificationRepository.UpdateNotificationStateAsync(userId, menuItemKey, data.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as viewed for user {UserId}, menu {MenuItemKey}",
                    userId, menuItemKey);
            }
        }

        private async Task<NotificationConfig?> GetNotificationConfigAsync(string menuItemKey)
        {
            // Cache notification configs for performance
            var cacheKey = $"notification_config_{menuItemKey}";

            if (_cache.TryGetValue(cacheKey, out NotificationConfig? config))
            {
                return config;
            }

            config = await _notificationRepository.GetNotificationConfigAsync(menuItemKey);

            if (config != null)
            {
                _cache.Set(cacheKey, config, TimeSpan.FromMinutes(5));
            }

            return config;
        }

        private async Task<int> ExecuteSqlQueryAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return 0;

            try
            {
                using var connection = new SqlConnection(_connectionString);
                var result = await connection.ExecuteScalarAsync<int>(query);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing SQL query: {Query}", query);
                return 0;
            }
        }

        private async Task<int> ExecuteApiQueryAsync(string endpoint)
        {
            // TODO: Implement API query execution when needed
            // This would call internal API endpoints to get counts
            await Task.Delay(1); // Placeholder
            return 0;
        }

        private bool HasRequiredRole(string userRole, string requiredRole)
        {
            // Role hierarchy: Admin > Manager > User
            var roleHierarchy = new Dictionary<string, int>
            {
                { "Admin", 3 },
                { "Manager", 2 },
                { "User", 1 }
            };

            if (!roleHierarchy.TryGetValue(userRole, out int userLevel))
                userLevel = 0;

            if (!roleHierarchy.TryGetValue(requiredRole, out int requiredLevel))
                requiredLevel = 0;

            return userLevel >= requiredLevel;
        }

        public async Task<bool> CreateOrUpdateNotificationConfigAsync(NotificationConfig config)
        {
            try
            {
                var result = await _notificationRepository.SaveNotificationConfigAsync(config);

                // Clear cache for this config
                var cacheKey = $"notification_config_{config.MenuItemKey}";
                _cache.Remove(cacheKey);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving notification config for {MenuItemKey}", config.MenuItemKey);
                return false;
            }
        }
    }

    public class NotificationData
    {
        public int Count { get; set; }
        public bool IsNew { get; set; }
        public string DisplayType { get; set; } = "Badge";
        public bool PulseOnNew { get; set; } = true;
    }
}