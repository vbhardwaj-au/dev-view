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
    public class TeamMember
    {
        public int Id { get; set; }
        
        public int TeamId { get; set; }
        
        public int UserId { get; set; }
        
        [Required]
        public DateTime AddedOn { get; set; } = DateTime.UtcNow;
        
        [StringLength(255)]
        public string? AddedBy { get; set; }
        
        // Navigation properties (if using Entity Framework)
        // public virtual Team Team { get; set; }
        // public virtual User User { get; set; }
    }
} 