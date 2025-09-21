using System;

namespace Data.Models
{
    public class GitConnection
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string GitServerType { get; set; } = string.Empty;
        public string ApiBaseUrl { get; set; } = string.Empty;
        public string? ConsumerKey { get; set; }
        public string? ConsumerSecret { get; set; }
        public string? AccessToken { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? PersonalAccessToken { get; set; }
        public bool IsActive { get; set; }
        public int Priority { get; set; }
        public string? Workspace { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public string? AdditionalSettings { get; set; }
    }

    public static class GitServerTypes
    {
        public const string BitbucketCloud = "BitbucketCloud";
        public const string GitHub = "GitHub";
        public const string GitLab = "GitLab";
        public const string AzureDevOps = "AzureDevOps";
        public const string BitbucketServer = "BitbucketServer";
    }
}