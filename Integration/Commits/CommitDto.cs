using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Integration.Commits
{
    public class CommitDto
    {
        [JsonPropertyName("hash")]
        public string Hash { get; set; }

        private DateTime _date;
        
        [JsonPropertyName("date")]
        public DateTime Date 
        { 
            get => _date;
            set 
            {
                // Ensure the date is always stored as UTC
                // If the DateTime has Unspecified kind, it means timezone info was lost
                // If it's Local, convert to UTC
                // If it's already UTC, keep as is
                _date = value.Kind switch
                {
                    DateTimeKind.Local => value.ToUniversalTime(),
                    DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
                    _ => value // Already UTC
                };
            }
        }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("author")]
        public CommitAuthorDto Author { get; set; }

        [JsonPropertyName("parents")]
        public List<CommitParentDto> Parents { get; set; } = new();

        public bool IsRevert { get; set; }
    }

    public class CommitParentDto
    {
        [JsonPropertyName("hash")]
        public string Hash { get; set; }
    }

    public class CommitAuthorDto
    {
        // The 'user' object is sometimes nested within the 'author' object
        [JsonPropertyName("user")]
        public AuthorUserDto User { get; set; }

        // Sometimes the author email is in the 'raw' field
        [JsonPropertyName("raw")]
        public string Raw { get; set; }
    }

    public class AuthorUserDto
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; }
    }

    // For deserializing the diffstat response
    public class DiffStatDto
    {
        [JsonPropertyName("lines_added")]
        public int LinesAdded { get; set; }

        [JsonPropertyName("lines_removed")]
        public int LinesRemoved { get; set; }
    }
}
