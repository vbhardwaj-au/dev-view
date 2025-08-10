/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace Entities.DTOs.Analytics
{
    public class UserDto
    {
        public int Id { get; set; }
        public string BitbucketUserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public DateTime? CreatedOn { get; set; }
        public bool ExcludeFromReporting { get; set; }
    }
} 