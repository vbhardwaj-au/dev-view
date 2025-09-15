/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using System;

namespace Data.Models
{
    public class AuthUser
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
        public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? JobTitle { get; set; }
        public string? Department { get; set; }
        public string AuthProvider { get; set; } = "Database"; // "Database" or "AzureAd"
        public string? AzureAdObjectId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedOn { get; set; }
        
        // Navigation properties
        public List<AuthRole> Roles { get; set; } = new();
        
        // Helper properties
        public bool IsAzureAdUser => AuthProvider == "AzureAd";
        public bool IsDatabaseUser => AuthProvider == "Database";
        
        /// <summary>
        /// Gets the display name or falls back to username if display name is empty
        /// </summary>
        public string DisplayNameOrUsername => !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName : Username;
    }
    
    public enum AuthProvider
    {
        Database,
        AzureAd
    }
}
