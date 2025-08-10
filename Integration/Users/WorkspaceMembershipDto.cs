using System.Text.Json.Serialization;

namespace Integration.Users
{
    // Represents a single item in the workspace members list
    public class WorkspaceMembershipDto
    {
        [JsonPropertyName("user")]
        public UserDto User { get; set; }
    }
} 