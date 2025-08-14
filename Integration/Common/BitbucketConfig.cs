using Microsoft.Extensions.Configuration;

namespace Integration.Common
{
    public class BitbucketConfig
    {
        // Database connection string, populated from configuration
        public string DbConnectionString { get; set; }

        // Bitbucket API base URL, populated from configuration
        public string ApiBaseUrl { get; set; }

        // Bitbucket OAuth Consumer Key and Secret
        public string ConsumerKey { get; set; }
        public string ConsumerSecret { get; set; }

        // Rate limit handling configuration
        public int? RateLimitMaxWaitSeconds { get; set; } // e.g., cap each wait chunk at 55 seconds
        public int? RateLimitHeartbeatSeconds { get; set; } // e.g., emit a heartbeat every 10 seconds
    }
}
