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
    public class PullRequest
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(255)]
        public string BitbucketPrId { get; set; } = string.Empty;
        
        public int RepositoryId { get; set; }
        
        public int AuthorId { get; set; }
        
        public string? Title { get; set; }
        
        [StringLength(50)]
        public string? State { get; set; }
        
        public DateTime? CreatedOn { get; set; }
        
        public DateTime? UpdatedOn { get; set; }
        
        public DateTime? MergedOn { get; set; }
        
        public DateTime? ClosedOn { get; set; }
        
        public bool IsRevert { get; set; } = false;
        
        // Navigation properties
        // public virtual Repository Repository { get; set; }
        // public virtual User Author { get; set; }
    }
}
