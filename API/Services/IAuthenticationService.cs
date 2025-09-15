/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using System.Security.Claims;
using Data.Models;
using Entities.DTOs.Auth;

namespace API.Services
{
    public interface IAuthenticationService
    {
        Task<AuthResult> AuthenticateAsync(string username, string password);
        Task<AuthResult> AuthenticateWithAzureAdAsync(ClaimsPrincipal azureUser);
        Task<AuthUser?> GetUserByUsernameAsync(string username);
        Task<AuthUser?> GetUserByAzureObjectIdAsync(string objectId);
        Task<AuthUser?> GetUserByEmailAsync(string email);
        Task<AuthUser> CreateUserFromAzureAdAsync(CreateAzureAdUserRequest request);
        Task<AuthUser> CreateDatabaseUserAsync(CreateUserRequest request);
        Task<AuthUser> UpdateUserAsync(int userId, UpdateUserRequest request);
        Task<string> GenerateJwtTokenAsync(AuthUser user, string[] roles);
        Task<string[]> GetUserRolesAsync(int userId);
        Task<bool> ValidatePasswordAsync(string password, byte[] hash, byte[] salt);
        Task<(byte[] hash, byte[] salt)> HashPasswordAsync(string password);
    }
}
