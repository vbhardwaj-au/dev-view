/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using Entities.DTOs.Analytics;

namespace Entities.DTOs.Teams
{
    public class TeamDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
        public bool IsActive { get; set; } = true;
        public int MemberCount { get; set; }
        public List<UserDto> Members { get; set; } = new List<UserDto>();
    }

    public class CreateTeamDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<int> UserIds { get; set; } = new List<int>();
    }

    public class UpdateTeamDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class TeamMemberDto
    {
        public int Id { get; set; }
        public int TeamId { get; set; }
        public int UserId { get; set; }
        public DateTime AddedOn { get; set; }
        public string? AddedBy { get; set; }
        public UserDto User { get; set; } = new UserDto();
    }

    public class AddTeamMemberDto
    {
        public List<int> UserIds { get; set; } = new List<int>();
        public bool IncludeAlreadyMapped { get; set; } = false;
    }

    public class TeamMembershipDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public List<string> Teams { get; set; } = new List<string>();
        public bool IsAlreadyMapped { get; set; }
    }

    public class AvailableUserDto
    {
        public int Id { get; set; }
        public string BitbucketUserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public DateTime? CreatedOn { get; set; }
        public bool IsAlreadyMapped { get; set; }
        public string ExistingTeams { get; set; } = string.Empty;
    }

    public class AddTeamMembersResponseDto
    {
        public int AddedMembers { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }
} 