/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace Entities.DTOs.Analytics
{
    public class UserDashboardResponseDto
    {
        public PeriodStats CurrentPeriod { get; set; } = new PeriodStats();
        public PeriodStats PreviousPeriod { get; set; } = new PeriodStats();
        public PrAgeGraph PrAgeGraphData { get; set; } = new PrAgeGraph();
        public List<ContributorStats> TopContributors { get; set; } = new List<ContributorStats>();
        public int UsersWithNoActivity { get; set; }
        public List<ApproverStats> TopApprovers { get; set; } = new List<ApproverStats>();
        public PrsMergedByWeekdayData PrsMergedByWeekdayData { get; set; } = new PrsMergedByWeekdayData();
    }

    public class PeriodStats
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int ActiveContributingUsers { get; set; }
        public int TotalLicensedUsers { get; set; }
        public int TotalCommits { get; set; }
        public int RepositoriesUpdated { get; set; }
        public int PrsNotApprovedAndMerged { get; set; }
        public int TotalMergedPrs { get; set; }
    }

    public class PrAgeGraph
    {
        public List<PrAgeDataPoint> OpenPrAge { get; set; } = new List<PrAgeDataPoint>();
        public List<PrAgeDataPoint> MergedPrAge { get; set; } = new List<PrAgeDataPoint>();
    }

    public class PrAgeDataPoint
    {
        public int Days { get; set; }
        public int PrCount { get; set; }
    }

    public class ContributorStats
    {
        public string UserName { get; set; } = string.Empty;
        public int Commits { get; set; }
        public int CodeLinesAdded { get; set; }
        public int CodeLinesRemoved { get; set; }
    }

    public class ApproverStats
    {
        public string UserName { get; set; } = string.Empty;
        public int PrApprovalCount { get; set; }
    }

    public class PrsMergedByWeekdayData
    {
        public List<WeekdayPrCount> MergedPrsByWeekday { get; set; } = new List<WeekdayPrCount>();
    }

    public class WeekdayPrCount
    {
        public string DayOfWeek { get; set; } = string.Empty;
        public int PrCount { get; set; }
    }
} 