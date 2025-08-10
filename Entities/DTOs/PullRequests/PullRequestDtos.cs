/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace Entities.DTOs.PullRequests
{
    public class PullRequestListItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public string RepositorySlug { get; set; } = string.Empty;
        public string Workspace { get; set; } = string.Empty;
        public string BitbucketPrId { get; set; } = string.Empty;
        public int ApprovalCount { get; set; }
        public List<ApprovalDto> Approvals { get; set; } = new();
        public DateTime? MergedOn { get; set; }
        public DateTime? ClosedOn { get; set; }
        public bool IsRevert { get; set; }
        public string RepositoryName { get; set; } = string.Empty;
    }

    public class ApprovalDto
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool Approved { get; set; }
        public DateTime? ApprovedOn { get; set; }
    }

    public class PaginatedPullRequestsResponse
    {
        public List<PullRequestListItemDto> PullRequests { get; set; } = new();
        public int TotalPages { get; set; }
    }
} 