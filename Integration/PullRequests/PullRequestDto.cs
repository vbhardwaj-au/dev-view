using Integration.Commits; // Reusing CommitDto and BitbucketUserDto
using Integration.Users;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Integration.PullRequests
{
    public class PullRequestDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("author")]
        public PRAuthorDto Author { get; set; }

        [JsonPropertyName("created_on")]
        public DateTime CreatedOn { get; set; }

        [JsonPropertyName("updated_on")]
        public DateTime? UpdatedOn { get; set; }

        [JsonPropertyName("closed_on")]
        public DateTime? ClosedOn { get; set; }

        [JsonPropertyName("merge_commit")]
        public CommitDto MergeCommit { get; set; }

        [JsonPropertyName("participants")]
        public List<BitbucketPullRequestParticipantDto> Participants { get; set; }

        [JsonPropertyName("reviewers")]
        public List<BitbucketPullRequestParticipantDto> Reviewers { get; set; }
    }

    public class PRAuthorDto
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; }

        // Add other fields as needed
    }

    public class BitbucketPullRequestParticipantDto
    {
        [JsonPropertyName("user")]
        public UserDto User { get; set; } // Reusing existing Integration.Users.UserDto

        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("approved")]
        public bool Approved { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; } // e.g., "approved", "changes_requested", "needs_work"
        
        [JsonPropertyName("status")]
        public string Status { get; set; } // This is for consistency with the Bitbucket API's activity endpoint

        [JsonPropertyName("participated_on")]
        public DateTime? ParticipatedOn { get; set; } // This might be useful if we parse activity later
    }
}
