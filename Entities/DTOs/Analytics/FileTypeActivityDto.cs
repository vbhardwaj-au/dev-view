/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace Entities.DTOs.Analytics
{
    public class FileTypeActivityDto
    {
        public DateTime Date { get; set; }
        public string FileType { get; set; } = string.Empty; // "code", "data", "config", "docs"
        public int CommitCount { get; set; }
        public int LinesAdded { get; set; }
        public int LinesRemoved { get; set; }
        public int NetLinesChanged { get; set; }
    }
} 