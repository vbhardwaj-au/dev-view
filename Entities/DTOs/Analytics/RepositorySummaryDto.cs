/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace Entities.DTOs.Analytics
{
    public class RepositorySummaryDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Slug { get; set; }
        public required string Workspace { get; set; }
        public DateTime? OldestCommitDate { get; set; }
        public DateTime? LastDeltaSyncDate { get; set; }
        public int OpenPullRequestCount { get; set; }
        public DateTime? OldestOpenPullRequestDate { get; set; }
        public int PRsMissingApprovalCount { get; set; }
        public bool ExcludeFromSync { get; set; }
        public bool ExcludeFromReporting { get; set; }
    }
} 