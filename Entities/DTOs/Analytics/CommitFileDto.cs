/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using System.Text.Json.Serialization;

namespace Entities.DTOs.Analytics
{
    public class CommitFileDto
    {
        public int Id { get; set; }
        public string? FilePath { get; set; }
        public string FileType { get; set; } = string.Empty;
        public int LinesAdded { get; set; }
        public int LinesRemoved { get; set; }
        public bool ExcludeFromReporting { get; set; }
        public string CommitHash { get; set; } = string.Empty;

        [JsonIgnore]
        public int DiffLineId { get; set; }
    }

    public class CommitFileUpdateDto
    {
        public int FileId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public bool Value { get; set; }
    }
} 