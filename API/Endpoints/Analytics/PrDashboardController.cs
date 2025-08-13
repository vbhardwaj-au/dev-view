using Data.Models;
using Microsoft.AspNetCore.Mvc;
using Entities.DTOs.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace API.Endpoints.Analytics
{
    [ApiController]
    [Route("api/[controller]")]
    public class PrDashboardController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly ILogger<PrDashboardController> _logger;

        public PrDashboardController(IConfiguration config, ILogger<PrDashboardController> logger)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? 
                                throw new InvalidOperationException("DefaultConnection connection string not found.");
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetPrDashboard(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? repoSlug = null,
            string? workspace = null,
            int? userId = null,
            int? teamId = null)
        {
            try
            {
                // Handle date ranges
                if (!startDate.HasValue && !endDate.HasValue)
                {
                    endDate = DateTime.Today;
                    startDate = endDate.Value.AddDays(-30);
                }
                else if (!startDate.HasValue)
                {
                    startDate = endDate!.Value.AddDays(-30);
                }
                else if (!endDate.HasValue)
                {
                    endDate = DateTime.Today;
                }

                var currentPeriodLength = (endDate.Value - startDate.Value).Days + 1;
                var previousPeriodStartDate = startDate.Value.AddDays(-currentPeriodLength);
                var previousPeriodEndDate = startDate.Value.AddDays(-1);

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var securityKpis = await GetSecurityComplianceKpis(connection, 
                    startDate.Value, endDate.Value, previousPeriodStartDate, previousPeriodEndDate,
                    repoSlug, workspace, userId, teamId);

                var processKpis = await GetProcessEfficiencyKpis(connection,
                    startDate.Value, endDate.Value, previousPeriodStartDate, previousPeriodEndDate,
                    repoSlug, workspace, userId, teamId);

                var teamKpis = await GetTeamWorkloadKpis(connection,
                    startDate.Value, endDate.Value,
                    repoSlug, workspace, userId, teamId);

                var response = new PrDashboardResponseDto
                {
                    SecurityCompliance = securityKpis,
                    ProcessEfficiency = processKpis,
                    TeamWorkload = teamKpis
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetPrDashboard with teamId={teamId}, userId={userId}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private async Task<SecurityComplianceKpis> GetSecurityComplianceKpis(
            SqlConnection connection, DateTime currentStart, DateTime currentEnd, 
            DateTime previousStart, DateTime previousEnd,
            string? repoSlug, string? workspace, int? userId, int? teamId)
        {
            var whereConditions = BuildWhereConditions(repoSlug, workspace, userId, teamId);
            var whereClause = whereConditions.Any() ? "AND " + string.Join(" AND ", whereConditions) : "";

            // PRs merged without approval (current period)
            var prsMergedWithoutApprovalSql = $@"
                SELECT COUNT(DISTINCT pr.Id)
                FROM PullRequests pr
                JOIN Repositories r ON pr.RepositoryId = r.Id
                LEFT JOIN PullRequestApprovals pra ON pr.Id = pra.PullRequestId AND pra.Approved = 1
                WHERE pr.State = 'MERGED' 
                  AND pr.MergedOn >= @currentStart 
                  AND pr.MergedOn <= @currentEnd
                  AND pra.Id IS NULL  -- No approvals
                  {whereClause}";

            // PRs merged without approval (previous period)  
            var prsMergedWithoutApprovalPreviousSql = $@"
                SELECT COUNT(DISTINCT pr.Id)
                FROM PullRequests pr
                JOIN Repositories r ON pr.RepositoryId = r.Id
                LEFT JOIN PullRequestApprovals pra ON pr.Id = pra.PullRequestId AND pra.Approved = 1
                WHERE pr.State = 'MERGED' 
                  AND pr.MergedOn >= @previousStart 
                  AND pr.MergedOn <= @previousEnd
                  AND pra.Id IS NULL  -- No approvals
                  {whereClause}";

            // Repositories with approval bypasses (current period)
            var reposWithBypassesSql = $@"
                SELECT COUNT(DISTINCT pr.RepositoryId)
                FROM PullRequests pr
                JOIN Repositories r ON pr.RepositoryId = r.Id
                LEFT JOIN PullRequestApprovals pra ON pr.Id = pra.PullRequestId AND pra.Approved = 1
                WHERE pr.State = 'MERGED' 
                  AND pr.MergedOn >= @currentStart 
                  AND pr.MergedOn <= @currentEnd
                  AND pra.Id IS NULL  -- No approvals
                  {whereClause}";

            // Repositories with approval bypasses (previous period)
            var reposWithBypassesPreviousSql = $@"
                SELECT COUNT(DISTINCT pr.RepositoryId)
                FROM PullRequests pr
                JOIN Repositories r ON pr.RepositoryId = r.Id
                LEFT JOIN PullRequestApprovals pra ON pr.Id = pra.PullRequestId AND pra.Approved = 1
                WHERE pr.State = 'MERGED' 
                  AND pr.MergedOn >= @previousStart 
                  AND pr.MergedOn <= @previousEnd
                  AND pra.Id IS NULL  -- No approvals
                  {whereClause}";

            // Total merged PRs for rate calculation (current period)
            var totalMergedCurrentSql = $@"
                SELECT COUNT(DISTINCT pr.Id)
                FROM PullRequests pr
                JOIN Repositories r ON pr.RepositoryId = r.Id
                WHERE pr.State = 'MERGED' 
                  AND pr.MergedOn >= @currentStart 
                  AND pr.MergedOn <= @currentEnd
                  {whereClause}";

            // Total merged PRs for rate calculation (previous period)
            var totalMergedPreviousSql = $@"
                SELECT COUNT(DISTINCT pr.Id)
                FROM PullRequests pr
                JOIN Repositories r ON pr.RepositoryId = r.Id
                WHERE pr.State = 'MERGED' 
                  AND pr.MergedOn >= @previousStart 
                  AND pr.MergedOn <= @previousEnd
                  {whereClause}";

            var parameters = new
            {
                currentStart,
                currentEnd,
                previousStart,
                previousEnd,
                repoSlug,
                workspace,
                userId,
                teamId
            };

            var prsMergedWithoutApproval = await connection.QuerySingleOrDefaultAsync<int>(prsMergedWithoutApprovalSql, parameters);
            var prsMergedWithoutApprovalPrevious = await connection.QuerySingleOrDefaultAsync<int>(prsMergedWithoutApprovalPreviousSql, parameters);
            var reposWithBypasses = await connection.QuerySingleOrDefaultAsync<int>(reposWithBypassesSql, parameters);
            var reposWithBypassesPrevious = await connection.QuerySingleOrDefaultAsync<int>(reposWithBypassesPreviousSql, parameters);
            var totalMergedCurrent = await connection.QuerySingleOrDefaultAsync<int>(totalMergedCurrentSql, parameters);
            var totalMergedPrevious = await connection.QuerySingleOrDefaultAsync<int>(totalMergedPreviousSql, parameters);

            // Calculate bypass rates
            var bypassRate = totalMergedCurrent > 0 ? (decimal)prsMergedWithoutApproval / totalMergedCurrent * 100 : 0;
            var bypassRatePrevious = totalMergedPrevious > 0 ? (decimal)prsMergedWithoutApprovalPrevious / totalMergedPrevious * 100 : 0;

            return new SecurityComplianceKpis
            {
                PrsMergedWithoutApproval = prsMergedWithoutApproval,
                PrsMergedWithoutApprovalPrevious = prsMergedWithoutApprovalPrevious,
                ReposWithApprovalBypasses = reposWithBypasses,
                ReposWithApprovalBypassesPrevious = reposWithBypassesPrevious,
                ApprovalBypassRate = bypassRate,
                ApprovalBypassRatePrevious = bypassRatePrevious,
                TotalMergedPrs = totalMergedCurrent,
                TotalMergedPrsPrevious = totalMergedPrevious
            };
        }

        private async Task<ProcessEfficiencyKpis> GetProcessEfficiencyKpis(
            SqlConnection connection, DateTime currentStart, DateTime currentEnd,
            DateTime previousStart, DateTime previousEnd,
            string? repoSlug, string? workspace, int? userId, int? teamId)
        {
            var whereConditions = BuildWhereConditions(repoSlug, workspace, userId, teamId);
            var whereClause = whereConditions.Any() ? "AND " + string.Join(" AND ", whereConditions) : "";

            // Average time from PR creation to first approval (current period)
            var avgReviewTimeSql = $@"
                SELECT AVG(CAST(DATEDIFF(HOUR, pr.CreatedOn, pra.ApprovedOn) AS FLOAT))
                FROM PullRequests pr
                JOIN Repositories r ON pr.RepositoryId = r.Id
                JOIN (
                    SELECT PullRequestId, MIN(ApprovedOn) AS FirstApprovalTime
                    FROM PullRequestApprovals 
                    WHERE Approved = 1 AND ApprovedOn IS NOT NULL
                    GROUP BY PullRequestId
                ) first_approval ON pr.Id = first_approval.PullRequestId
                JOIN PullRequestApprovals pra ON pr.Id = pra.PullRequestId AND pra.ApprovedOn = first_approval.FirstApprovalTime
                WHERE pr.CreatedOn >= @currentStart 
                  AND pr.CreatedOn <= @currentEnd
                  {whereClause}";

            // Average time from PR creation to first approval (previous period)
            var avgReviewTimePreviousSql = $@"
                SELECT AVG(CAST(DATEDIFF(HOUR, pr.CreatedOn, pra.ApprovedOn) AS FLOAT))
                FROM PullRequests pr
                JOIN Repositories r ON pr.RepositoryId = r.Id
                JOIN (
                    SELECT PullRequestId, MIN(ApprovedOn) AS FirstApprovalTime
                    FROM PullRequestApprovals 
                    WHERE Approved = 1 AND ApprovedOn IS NOT NULL
                    GROUP BY PullRequestId
                ) first_approval ON pr.Id = first_approval.PullRequestId
                JOIN PullRequestApprovals pra ON pr.Id = pra.PullRequestId AND pra.ApprovedOn = first_approval.FirstApprovalTime
                WHERE pr.CreatedOn >= @previousStart 
                  AND pr.CreatedOn <= @previousEnd
                  {whereClause}";

            // Average merge time (current period)
            var avgMergeTimeSql = $@"
                SELECT AVG(CAST(DATEDIFF(HOUR, pr.CreatedOn, pr.MergedOn) AS FLOAT)) / 24.0
                FROM PullRequests pr
                JOIN Repositories r ON pr.RepositoryId = r.Id
                WHERE pr.State = 'MERGED'
                  AND pr.MergedOn >= @currentStart 
                  AND pr.MergedOn <= @currentEnd
                  {whereClause}";

            // Average merge time (previous period)
            var avgMergeTimePreviousSql = $@"
                SELECT AVG(CAST(DATEDIFF(HOUR, pr.CreatedOn, pr.MergedOn) AS FLOAT)) / 24.0
                FROM PullRequests pr
                JOIN Repositories r ON pr.RepositoryId = r.Id
                WHERE pr.State = 'MERGED'
                  AND pr.MergedOn >= @previousStart 
                  AND pr.MergedOn <= @previousEnd
                  {whereClause}";

            // Open PRs needing review and active reviewers for bottleneck score
            var openPrsNeedingReviewSql = $@"
                SELECT COUNT(DISTINCT pr.Id)
                FROM PullRequests pr
                JOIN Repositories r ON pr.RepositoryId = r.Id
                LEFT JOIN PullRequestApprovals pra ON pr.Id = pra.PullRequestId AND pra.Approved = 1
                WHERE pr.State = 'OPEN'
                  AND pra.Id IS NULL  -- No approvals yet
                  {whereClause}";

            // Count unique reviewers who have been active recently
            var activeReviewersSql = $@"
                SELECT COUNT(DISTINCT u.Id)
                FROM Users u
                JOIN PullRequestApprovals pra ON u.BitbucketUserId = pra.UserUuid
                JOIN PullRequests pr ON pra.PullRequestId = pr.Id
                JOIN Repositories r ON pr.RepositoryId = r.Id
                WHERE pra.ApprovedOn >= @currentStart
                  AND pra.ApprovedOn <= @currentEnd
                  {whereClause}";

            var parameters = new
            {
                currentStart,
                currentEnd,
                previousStart,
                previousEnd,
                repoSlug,
                workspace,
                userId,
                teamId
            };

            var avgReviewTime = await connection.QuerySingleOrDefaultAsync<decimal?>(avgReviewTimeSql, parameters) ?? 0;
            var avgReviewTimePrevious = await connection.QuerySingleOrDefaultAsync<decimal?>(avgReviewTimePreviousSql, parameters) ?? 0;
            var avgMergeTime = await connection.QuerySingleOrDefaultAsync<decimal?>(avgMergeTimeSql, parameters) ?? 0;
            var avgMergeTimePrevious = await connection.QuerySingleOrDefaultAsync<decimal?>(avgMergeTimePreviousSql, parameters) ?? 0;
            var openPrsNeedingReview = await connection.QuerySingleOrDefaultAsync<int>(openPrsNeedingReviewSql, parameters);
            var activeReviewers = await connection.QuerySingleOrDefaultAsync<int>(activeReviewersSql, parameters);

            // Calculate bottleneck score
            var bottleneckScore = activeReviewers > 0 ? (decimal)openPrsNeedingReview / activeReviewers : 0;

            // For previous bottleneck score, we'd need to calculate based on previous period data
            // This is a simplified calculation - in practice, you might want to store historical data
            var bottleneckScorePrevious = bottleneckScore; // Simplified for now

            return new ProcessEfficiencyKpis
            {
                AverageReviewTimeHours = avgReviewTime,
                AverageReviewTimeHoursPrevious = avgReviewTimePrevious,
                AverageMergeTimeDays = avgMergeTime,
                AverageMergeTimeDaysPrevious = avgMergeTimePrevious,
                ReviewBottleneckScore = bottleneckScore,
                ReviewBottleneckScorePrevious = bottleneckScorePrevious,
                OpenPrsNeedingReview = openPrsNeedingReview,
                ActiveReviewers = activeReviewers
            };
        }

        private async Task<TeamWorkloadKpis> GetTeamWorkloadKpis(
            SqlConnection connection, DateTime currentStart, DateTime currentEnd,
            string? repoSlug, string? workspace, int? userId, int? teamId)
        {
            var whereConditions = BuildWhereConditions(repoSlug, workspace, userId, teamId);
            var whereClause = whereConditions.Any() ? "AND " + string.Join(" AND ", whereConditions) : "";

            // Open PRs by age categories
            var prsByAgeSql = $@"
                SELECT 
                    SUM(CASE WHEN DATEDIFF(DAY, pr.CreatedOn, GETUTCDATE()) < 3 THEN 1 ELSE 0 END) AS FreshPrs,
                    SUM(CASE WHEN DATEDIFF(DAY, pr.CreatedOn, GETUTCDATE()) BETWEEN 3 AND 7 THEN 1 ELSE 0 END) AS ActivePrs,
                    SUM(CASE WHEN DATEDIFF(DAY, pr.CreatedOn, GETUTCDATE()) > 7 THEN 1 ELSE 0 END) AS StalePrs
                FROM PullRequests pr
                JOIN Repositories r ON pr.RepositoryId = r.Id
                WHERE pr.State = 'OPEN'
                  {whereClause}";

            // Team PR velocity (PRs merged this week)
            var teamVelocitySql = $@"
                SELECT COUNT(DISTINCT pr.Id)
                FROM PullRequests pr
                JOIN Repositories r ON pr.RepositoryId = r.Id
                WHERE pr.State = 'MERGED'
                  AND pr.MergedOn >= @currentStart
                  AND pr.MergedOn <= @currentEnd
                  {whereClause}";

            // Team PR velocity average (4-week average)
            var teamVelocityAvgSql = $@"
                SELECT AVG(CAST(WeeklyCount AS FLOAT))
                FROM (
                    SELECT 
                        DATEPART(WEEK, pr.MergedOn) AS WeekNum,
                        COUNT(DISTINCT pr.Id) AS WeeklyCount
                    FROM PullRequests pr
                    JOIN Repositories r ON pr.RepositoryId = r.Id
                    WHERE pr.State = 'MERGED'
                      AND pr.MergedOn >= DATEADD(DAY, -28, @currentEnd)
                      AND pr.MergedOn <= @currentEnd
                      {whereClause}
                    GROUP BY DATEPART(WEEK, pr.MergedOn)
                ) weekly_counts";

            // Reviewer distribution for balance calculation
            var reviewerDistributionSql = $@"
                SELECT 
                    u.DisplayName,
                    COUNT(pra.Id) AS ReviewCount,
                    SUM(CASE WHEN pra.Approved = 1 THEN 1 ELSE 0 END) AS ApprovalCount
                FROM Users u
                JOIN PullRequestApprovals pra ON u.BitbucketUserId = pra.UserUuid
                JOIN PullRequests pr ON pra.PullRequestId = pr.Id
                JOIN Repositories r ON pr.RepositoryId = r.Id
                WHERE pra.ApprovedOn >= @currentStart
                  AND pra.ApprovedOn <= @currentEnd
                  {whereClause}
                GROUP BY u.Id, u.DisplayName
                HAVING COUNT(pra.Id) > 0";

            var parameters = new
            {
                currentStart,
                currentEnd,
                repoSlug,
                workspace,
                userId,
                teamId
            };

            var prsByAge = await connection.QuerySingleOrDefaultAsync<(int FreshPrs, int ActivePrs, int StalePrs)>(prsByAgeSql, parameters);
            var teamVelocity = await connection.QuerySingleOrDefaultAsync<int>(teamVelocitySql, parameters);
            var teamVelocityAvg = await connection.QuerySingleOrDefaultAsync<decimal?>(teamVelocityAvgSql, parameters) ?? 0;
            var reviewerDistribution = await connection.QueryAsync<ReviewerStats>(reviewerDistributionSql, parameters);

            // Calculate review distribution balance
            var reviewCounts = reviewerDistribution.Select(r => r.ReviewCount).ToArray();
            var reviewDistributionStdDev = reviewCounts.Length > 0 ? CalculateStandardDeviation(reviewCounts) : 0;
            var reviewDistributionBalance = reviewCounts.Length > 0 ? CalculateDistributionBalance(reviewCounts) : 0;

            return new TeamWorkloadKpis
            {
                FreshPrs = prsByAge.FreshPrs,
                ActivePrs = prsByAge.ActivePrs,
                StalePrs = prsByAge.StalePrs,
                TeamPrVelocity = teamVelocity,
                TeamPrVelocityAverage = teamVelocityAvg,
                ReviewDistributionBalance = reviewDistributionBalance,
                ReviewDistributionStdDev = reviewDistributionStdDev,
                ActiveReviewersCount = reviewerDistribution.Count(),
                ReviewerDistribution = reviewerDistribution.ToList()
            };
        }

        private List<string> BuildWhereConditions(string? repoSlug, string? workspace, int? userId, int? teamId)
        {
            var conditions = new List<string>();

            if (!string.IsNullOrEmpty(workspace))
                conditions.Add("r.Workspace = @workspace");
            
            if (!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase))
                conditions.Add("r.Slug = @repoSlug");
            
            if (userId.HasValue)
                conditions.Add("pr.AuthorId = @userId");
            
            if (teamId.HasValue && teamId.Value > 0)
                conditions.Add("pr.AuthorId IN (SELECT UserId FROM TeamMembers WHERE TeamId = @teamId)");
            
            // Always exclude repositories from reporting
            conditions.Add("r.ExcludeFromReporting = 0");

            return conditions;
        }

        private decimal CalculateStandardDeviation(int[] values)
        {
            if (values.Length == 0) return 0;
            
            var mean = values.Average();
            var squaredDifferences = values.Select(v => Math.Pow(v - mean, 2)).ToArray();
            var variance = squaredDifferences.Average();
            return (decimal)Math.Sqrt(variance);
        }

        private decimal CalculateDistributionBalance(int[] values)
        {
            if (values.Length == 0) return 0;
            
            // Calculate coefficient of variation and convert to balance percentage
            var mean = values.Average();
            var stdDev = CalculateStandardDeviation(values);
            
            if (mean == 0) return 0;
            
            var coefficientOfVariation = (double)(stdDev / (decimal)mean);
            // Convert to balance percentage (lower variation = higher balance)
            var balance = Math.Max(0, 100 - (coefficientOfVariation * 100));
            return (decimal)Math.Min(100, balance);
        }
    }
}