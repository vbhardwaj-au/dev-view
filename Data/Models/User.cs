using System;

namespace Data.Models
{
    /// <summary>
    /// Represents a Bitbucket/Git user
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        public string BitbucketUserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public DateTime? CreatedOn { get; set; }
        public bool ExcludeFromReporting { get; set; }
    }
}