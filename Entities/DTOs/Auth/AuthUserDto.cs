/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using System;

namespace Entities.DTOs.Auth
{
    public class AuthUserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? JobTitle { get; set; }
        public string? Department { get; set; }
        public string AuthProvider { get; set; } = "Database";
        public string? AzureAdObjectId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public List<string> Roles { get; set; } = new();
    }
    
    public class CreateUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? JobTitle { get; set; }
        public string? Department { get; set; }
        public List<string> Roles { get; set; } = new();
    }
    
    public class CreateAzureAdUserRequest
    {
        public string AzureAdObjectId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? JobTitle { get; set; }
        public string? Department { get; set; }
        public string DefaultRole { get; set; } = "User";
    }
    
    public class UpdateUserRequest
    {
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? JobTitle { get; set; }
        public string? Department { get; set; }
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = new();
    }
    
    public class AzureLoginRequest
    {
        public string AccessToken { get; set; } = string.Empty;
        public string IdToken { get; set; } = string.Empty;
    }
    
    public class AuthResult
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? DisplayName { get; set; }
        public string[]? Roles { get; set; }
        public bool RequiresRedirect { get; set; }
        public string? RedirectUrl { get; set; }
        public string? ErrorMessage { get; set; }
        public AuthUserDto? User { get; set; }

        // Approval workflow properties
        public bool RequiresApproval { get; set; }
        public DateTime? RequestedAt { get; set; }
        public bool IsRejected { get; set; }
        public string? RejectionReason { get; set; }
    }
    
    public class AuthenticationConfig
    {
        public bool AzureAdEnabled { get; set; }
        public string DefaultProvider { get; set; } = "Database";
        public bool AllowFallback { get; set; } = true;
        public bool AutoCreateUsers { get; set; } = true;
        public string? AzureAdLoginUrl { get; set; }
    }
}
