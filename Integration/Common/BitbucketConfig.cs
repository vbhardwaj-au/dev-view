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
    }
}
