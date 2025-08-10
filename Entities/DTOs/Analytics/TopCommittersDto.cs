/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace Entities.DTOs.Analytics
{
    public class TopCommittersDto
    {
        public int UserId { get; set; }
        public string DisplayName { get; set; } = "";
        public string? AvatarUrl { get; set; }
        public int TotalCommits { get; set; }
        public int TotalLinesAdded { get; set; }
        public int TotalLinesRemoved { get; set; }
        public int CodeLinesAdded { get; set; }
        public int CodeLinesRemoved { get; set; }
        public int DataLinesAdded { get; set; }
        public int DataLinesRemoved { get; set; }
        public int ConfigLinesAdded { get; set; }
        public int ConfigLinesRemoved { get; set; }
        public string? CommitterType { get; set; }
        public string? ActivityData { get; set; }
    }

    public class TopCommittersResponseDto
    {
        public List<TopCommittersDto> TopCommitters { get; set; } = new List<TopCommittersDto>();
        public List<TopCommittersDto> BottomCommitters { get; set; } = new List<TopCommittersDto>();
    }
} 