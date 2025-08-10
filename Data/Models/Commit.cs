/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using System;
using System.ComponentModel.DataAnnotations;

namespace Data.Models
{
    public class Commit
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(255)]
        public string BitbucketCommitHash { get; set; } = string.Empty;
        
        public int RepositoryId { get; set; }
        public int AuthorId { get; set; }
        
        [Required]
        public DateTime Date { get; set; }
        
        public string? Message { get; set; }
        
        public int LinesAdded { get; set; }
        public int LinesRemoved { get; set; }
        public bool IsMerge { get; set; }
        public bool IsRevert { get; set; }
        
        // Code-specific line counts (nullable for backward compatibility)
        public int? CodeLinesAdded { get; set; }
        public int? CodeLinesRemoved { get; set; }
        
        // File type-specific line counts (new classification system)
        public int DataLinesAdded { get; set; }
        public int DataLinesRemoved { get; set; }
        public int ConfigLinesAdded { get; set; }
        public int ConfigLinesRemoved { get; set; }
        public int DocsLinesAdded { get; set; }
        public int DocsLinesRemoved { get; set; }
        
        public bool IsPRMergeCommit { get; set; }
        
        // Navigation properties (if using Entity Framework)
        // public virtual Repository Repository { get; set; }
        // public virtual User Author { get; set; }
    }
}
