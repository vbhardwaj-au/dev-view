using System;
using System.ComponentModel.DataAnnotations;

namespace Data.Models
{
    public class CommitFile
    {
        public int Id { get; set; }
        
        public int CommitId { get; set; }
        
        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;
        
        [Required]
        [StringLength(20)]
        public string FileType { get; set; } = string.Empty; // 'code', 'data', 'config', 'docs', 'other'
        
        [Required]
        [StringLength(20)]
        public string ChangeStatus { get; set; } = string.Empty; // 'added', 'modified', 'removed'
        
        public int LinesAdded { get; set; }
        public int LinesRemoved { get; set; }
        
        [StringLength(50)]
        public string? FileExtension { get; set; }
        
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        
        public bool ExcludeFromReporting { get; set; } = false;
        
        // Navigation property
        // public virtual Commit Commit { get; set; }
    }
} 