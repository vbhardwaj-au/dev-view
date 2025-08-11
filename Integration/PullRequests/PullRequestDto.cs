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

        private DateTime _createdOn;
        private DateTime? _updatedOn;
        private DateTime? _closedOn;

        [JsonPropertyName("created_on")]
        public DateTime CreatedOn 
        { 
            get => _createdOn;
            set 
            {
                _createdOn = value.Kind switch
                {
                    DateTimeKind.Local => value.ToUniversalTime(),
                    DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
                    _ => value // Already UTC
                };
            }
        }

        [JsonPropertyName("updated_on")]
        public DateTime? UpdatedOn 
        { 
            get => _updatedOn;
            set 
            {
                if (value.HasValue)
                {
                    _updatedOn = value.Value.Kind switch
                    {
                        DateTimeKind.Local => value.Value.ToUniversalTime(),
                        DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
                        _ => value.Value // Already UTC
                    };
                }
                else
                {
                    _updatedOn = null;
                }
            }
        }

        [JsonPropertyName("closed_on")]
        public DateTime? ClosedOn 
        { 
            get => _closedOn;
            set 
            {
                if (value.HasValue)
                {
                    _closedOn = value.Value.Kind switch
                    {
                        DateTimeKind.Local => value.Value.ToUniversalTime(),
                        DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
                        _ => value.Value // Already UTC
                    };
                }
                else
                {
                    _closedOn = null;
                }
            }
        }

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
