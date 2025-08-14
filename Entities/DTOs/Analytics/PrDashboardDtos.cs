/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace Entities.DTOs.Analytics
{
    public class PrDashboardResponseDto
    {
        public SecurityComplianceKpis SecurityCompliance { get; set; } = new();
        public ProcessEfficiencyKpis ProcessEfficiency { get; set; } = new();
        public TeamWorkloadKpis TeamWorkload { get; set; } = new();
    }

    // Row 1: Security & Compliance
    public class SecurityComplianceKpis
    {
        public int PrsMergedWithoutApproval { get; set; }
        public int PrsMergedWithoutApprovalPrevious { get; set; }
        
        public int ReposWithApprovalBypasses { get; set; }
        public int ReposWithApprovalBypassesPrevious { get; set; }
        
        public decimal ApprovalBypassRate { get; set; }
        public decimal ApprovalBypassRatePrevious { get; set; }
        
        public int TotalMergedPrs { get; set; }
        public int TotalMergedPrsPrevious { get; set; }
    }

    // Row 2: Process Efficiency
    public class ProcessEfficiencyKpis
    {
        public decimal AverageReviewTimeHours { get; set; }
        public decimal AverageReviewTimeHoursPrevious { get; set; }
        
        public decimal AverageMergeTimeDays { get; set; }
        public decimal AverageMergeTimeDaysPrevious { get; set; }
        
        public decimal ReviewBottleneckScore { get; set; }
        public decimal ReviewBottleneckScorePrevious { get; set; }
        
        public int OpenPrsNeedingReview { get; set; }
        public int ActiveReviewers { get; set; }
    }

    // Row 3: Team Workload
    public class TeamWorkloadKpis
    {
        public int FreshPrs { get; set; }      // <3 days
        public int ActivePrs { get; set; }     // 3-7 days  
        public int StalePrs { get; set; }      // >7 days
        
        public int TeamPrVelocity { get; set; }
        public decimal TeamPrVelocityAverage { get; set; }
        
        public decimal ReviewDistributionBalance { get; set; }
        public decimal ReviewDistributionStdDev { get; set; }
        public int ActiveReviewersCount { get; set; }
        
        public List<ReviewerStats> ReviewerDistribution { get; set; } = new();
    }

    public class ReviewerStats
    {
        public string DisplayName { get; set; } = string.Empty;
        public int ReviewCount { get; set; }
        public int ApprovalCount { get; set; }
    }

    // DTOs for modal drill-down data
    public class PrDetailsDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string RepositoryName { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public DateTime? MergedOn { get; set; }
        public string State { get; set; } = string.Empty;
        public string? Url { get; set; }
        public int ApprovalCount { get; set; }
    }

    public class RepositoryBypassDetailsDto
    {
        public string RepositoryName { get; set; } = string.Empty;
        public int BypassCount { get; set; }
        public DateTime? LatestBypassDate { get; set; }
        public List<string> RecentBypassAuthors { get; set; } = new();
    }
}