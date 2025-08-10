using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Integration.Users
{
    // Represents a paginated list from the Bitbucket API
    public class PaginatedResponseDto<T>
    {
        [JsonPropertyName("values")]
        public List<T> Values { get; set; }

        [JsonPropertyName("next")]
        public string NextPageUrl { get; set; }
    }
} 