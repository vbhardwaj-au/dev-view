using System.Text.Json.Serialization;

namespace Integration.Users
{
    // Represents the "user" object inside the Bitbucket API response
    public class UserDto
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("created_on")]
        public DateTime? CreatedOn { get; set; }

        [JsonPropertyName("links")]
        public UserLinksDto Links { get; set; }
    }

    public class UserLinksDto
    {
        [JsonPropertyName("avatar")]
        public AvatarLinkDto Avatar { get; set; }
    }

    public class AvatarLinkDto
    {
        [JsonPropertyName("href")]
        public string Href { get; set; }
    }
} 