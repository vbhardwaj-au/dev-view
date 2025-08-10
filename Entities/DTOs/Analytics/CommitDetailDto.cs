/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace Entities.DTOs.Analytics
{
    public class CommitDetailDto
    {
        public int Id { get; set; }
        public string CommitHash { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Message { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string RepositoryName { get; set; } = string.Empty;
        public string RepositorySlug { get; set; } = string.Empty;
        public int LinesAdded { get; set; }
        public int LinesRemoved { get; set; }
        public int CodeLinesAdded { get; set; }
        public int CodeLinesRemoved { get; set; }
        public int DataLinesAdded { get; set; }
        public int DataLinesRemoved { get; set; }
        public int ConfigLinesAdded { get; set; }
        public int ConfigLinesRemoved { get; set; }
        public int DocsLinesAdded { get; set; }
        public int DocsLinesRemoved { get; set; }
        public bool IsMerge { get; set; }
    }
} 