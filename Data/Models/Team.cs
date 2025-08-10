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
    public class Team
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(1000)]
        public string? Description { get; set; }
        
        [Required]
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        
        [StringLength(255)]
        public string? CreatedBy { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        // Navigation properties (if using Entity Framework)
        // public virtual ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
    }
} 