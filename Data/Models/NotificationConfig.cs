using System;

namespace Data.Models
{
    public class NotificationConfig
    {
        public int Id { get; set; }
        public string MenuItemKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string QueryType { get; set; } = "SQL"; // SQL, API, Static
        public string? Query { get; set; }
        public string? ApiEndpoint { get; set; }
        public int? StaticValue { get; set; }
        public int RefreshIntervalSeconds { get; set; } = 60;
        public string DisplayType { get; set; } = "Badge"; // Badge, Dot, Both
        public bool PulseOnNew { get; set; } = true;
        public string? MinimumRole { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    public class NotificationState
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string MenuItemKey { get; set; } = string.Empty;
        public int LastViewedCount { get; set; } = 0;
        public DateTime LastViewedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastCheckedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public AuthUser? User { get; set; }
    }

    public enum NotificationQueryType
    {
        SQL,
        API,
        Static
    }

    public enum NotificationDisplayType
    {
        Badge,
        Dot,
        Both
    }
}