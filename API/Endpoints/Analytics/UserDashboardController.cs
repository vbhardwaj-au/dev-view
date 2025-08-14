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
    public class UserDashboardController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly ILogger<UserDashboardController> _logger;

        public UserDashboardController(IConfiguration config, ILogger<UserDashboardController> logger)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ??
                                throw new InvalidOperationException("DefaultConnection connection string not found.");
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserDashboard(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? repoSlug = null,
            string? workspace = null,
            int? userId = null,
            int? teamId = null,
            bool includePR = false,
            bool showExcluded = false)
        {
            try
            {
            // Handle "All Time" when both dates are null - get full date range from data  
            if (!startDate.HasValue && !endDate.HasValue)
            {
                using var tempConnection = new SqlConnection(_connectionString);
                await tempConnection.OpenAsync();
                
                // Get the earliest commit date for true "All Time"
                startDate = await tempConnection.QuerySingleOrDefaultAsync<DateTime?>(
                    "SELECT MIN(Date) FROM Commits c JOIN Repositories r ON c.RepositoryId = r.Id WHERE (@workspace IS NULL OR r.Workspace = @workspace)",
                    new { workspace }) ?? DateTime.Today.AddYears(-10);
                    
                endDate = DateTime.Today;
            }
            // If only one date is null, fill in the missing one  
            else if (!startDate.HasValue || !endDate.HasValue)
            {
                using var tempConnection = new SqlConnection(_connectionString);
                await tempConnection.OpenAsync();
                
                if (!startDate.HasValue)
                {
                    startDate = await tempConnection.QuerySingleOrDefaultAsync<DateTime?>(
                        "SELECT MIN(Date) FROM Commits c JOIN Repositories r ON c.RepositoryId = r.Id WHERE (@workspace IS NULL OR r.Workspace = @workspace)",
                        new { workspace }) ?? DateTime.Today.AddYears(-10);
                }
                
                if (!endDate.HasValue)
                {
                    endDate = DateTime.Today;
                }
            }

            var currentPeriodLength = (endDate.Value - startDate.Value).Days + 1;
            var previousPeriodStartDate = startDate.Value.AddDays(-currentPeriodLength);
            var previousPeriodEndDate = startDate.Value.AddDays(-1);

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            PeriodStats currentPeriodStats, previousPeriodStats;
            PrAgeGraph prAgeGraphData;
            List<ContributorStats> topContributors;
            int usersWithNoActivity;
            int previousPeriodUsersWithNoActivity;
            List<ApproverStats> topApprovers;
            PrsMergedByWeekdayData prsMergedByWeekdayData;

            currentPeriodStats = await GetPeriodStats(connection, startDate.Value, endDate.Value, repoSlug, workspace, userId, teamId, includePR, showExcluded);
            previousPeriodStats = await GetPeriodStats(connection, previousPeriodStartDate, previousPeriodEndDate, repoSlug, workspace, userId, teamId, includePR, showExcluded);
            prAgeGraphData = await GetPrAgeGraphData(connection, startDate.Value, endDate.Value, repoSlug, workspace);
            topContributors = await GetTopContributors(connection, startDate.Value, endDate.Value, repoSlug, workspace, userId, teamId, includePR, showExcluded);
            usersWithNoActivity = await GetUsersWithNoActivity(connection, startDate.Value, endDate.Value, repoSlug, workspace, userId, teamId, includePR, showExcluded);
            previousPeriodUsersWithNoActivity = await GetUsersWithNoActivity(connection, previousPeriodStartDate, previousPeriodEndDate, repoSlug, workspace, userId, teamId, includePR, showExcluded);
            topApprovers = await GetTopApprovers(connection, startDate.Value, endDate.Value, repoSlug, workspace, teamId);
            prsMergedByWeekdayData = await GetPrsMergedByWeekdayData(connection, startDate.Value, endDate.Value, repoSlug, workspace);

            var response = new UserDashboardResponseDto
            {
                CurrentPeriod = currentPeriodStats,
                PreviousPeriod = previousPeriodStats,
                PrAgeGraphData = prAgeGraphData,
                TopContributors = topContributors,
                UsersWithNoActivity = usersWithNoActivity,
                PreviousPeriodUsersWithNoActivity = previousPeriodUsersWithNoActivity,
                TopApprovers = topApprovers,
                PrsMergedByWeekdayData = prsMergedByWeekdayData
            };

            return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetUserDashboard with teamId={teamId}, userId={userId}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private async Task<PeriodStats> GetPeriodStats(SqlConnection connection, DateTime periodStartDate, DateTime periodEndDate, string? repoSlug, string? workspace, int? userId, int? teamId, bool includePR, bool showExcluded)
        {
            // Build WHERE clause similar to Commits API
            var whereConditions = new List<string> { "c.IsRevert = 0" };
            
            if (!string.IsNullOrEmpty(workspace))
                whereConditions.Add("r.Workspace = @workspace");
            if (!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase))
                whereConditions.Add("r.Slug = @repoSlug");
            if (userId.HasValue)
                whereConditions.Add("c.AuthorId = @userId");
            if (teamId.HasValue && teamId.Value > 0)  // Changed from != -1 to > 0
                whereConditions.Add("c.AuthorId IN (SELECT UserId FROM TeamMembers WHERE TeamId = @teamId)");
            if (!includePR)
                whereConditions.Add("c.IsPRMergeCommit = 0");
            
            whereConditions.Add("c.Date >= @periodStartDate");
            whereConditions.Add("c.Date <= @periodEndDate");
            
            var whereClause = "WHERE " + string.Join(" AND ", whereConditions);

            // Total Commits
            var totalCommits = await connection.QuerySingleOrDefaultAsync<int>(
                $"SELECT COUNT(*) FROM Commits c JOIN Repositories r ON c.RepositoryId = r.Id {whereClause}", 
                new { periodStartDate, periodEndDate, repoSlug, workspace, userId, teamId });

            // Repositories Updated
            var repositoriesUpdated = await connection.QuerySingleOrDefaultAsync<int>(
                $"SELECT COUNT(DISTINCT c.RepositoryId) FROM Commits c JOIN Repositories r ON c.RepositoryId = r.Id {whereClause}", 
                new { periodStartDate, periodEndDate, repoSlug, workspace, userId, teamId });

            // Active Contributing Users
            int activeContributingUsers;
            if (teamId.HasValue && teamId.Value > 0)
            {
                // Simplified query when filtering by team - just count team members with any activity
                activeContributingUsers = await connection.QuerySingleOrDefaultAsync<int>($@"
                    SELECT COUNT(DISTINCT c.AuthorId)
                    FROM Commits c 
                    JOIN Repositories r ON c.RepositoryId = r.Id
                    WHERE c.AuthorId IN (SELECT UserId FROM TeamMembers WHERE TeamId = @teamId)
                      AND c.Date >= @periodStartDate 
                      AND c.Date <= @periodEndDate
                      AND c.IsRevert = 0
                      {(!string.IsNullOrEmpty(workspace) ? " AND r.Workspace = @workspace" : "")}
                      {(!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase) ? " AND r.Slug = @repoSlug" : "")}
                ", new { periodStartDate, periodEndDate, repoSlug, workspace, teamId });
            }
            else
            {
                // Original complex query for all users
                activeContributingUsers = await connection.QuerySingleOrDefaultAsync<int>($@"
                    SELECT COUNT(DISTINCT UserId)
                    FROM (
                        -- Commits
                        SELECT c.AuthorId AS UserId FROM Commits c JOIN Repositories r ON c.RepositoryId = r.Id {whereClause}
                        UNION
                        -- PRs Created
                        SELECT pr.AuthorId AS UserId 
                        FROM PullRequests pr 
                        JOIN Repositories r ON pr.RepositoryId = r.Id
                        WHERE pr.CreatedOn >= @periodStartDate AND pr.CreatedOn <= @periodEndDate
                        {(!string.IsNullOrEmpty(workspace) ? " AND r.Workspace = @workspace" : "")}
                        {(!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase) ? " AND r.Slug = @repoSlug" : "")}
                        {(userId.HasValue ? " AND pr.AuthorId = @userId" : "")}
                        UNION
                        -- PRs Approved
                        SELECT u.Id AS UserId
                        FROM PullRequestApprovals pra
                        JOIN PullRequests pr ON pra.PullRequestId = pr.Id
                        JOIN Users u ON pra.UserUuid = u.BitbucketUserId
                        JOIN Repositories r ON pr.RepositoryId = r.Id
                        WHERE pra.ApprovedOn >= @periodStartDate AND pra.ApprovedOn <= @periodEndDate AND pra.Approved = 1
                        {(!string.IsNullOrEmpty(workspace) ? " AND r.Workspace = @workspace" : "")}
                        {(!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase) ? " AND r.Slug = @repoSlug" : "")}
                        {(userId.HasValue ? " AND u.Id = @userId" : "")}
                    ) AS UserActivity
                ", new { periodStartDate, periodEndDate, repoSlug, workspace, userId });
            }

            // Total Licensed Users - If filtering by team, count team members, otherwise all users
            var totalLicensedUsers = (teamId.HasValue && teamId.Value > 0)
                ? await connection.QuerySingleOrDefaultAsync<int>(
                    "SELECT COUNT(*) FROM TeamMembers WHERE TeamId = @teamId", new { teamId })
                : await connection.QuerySingleOrDefaultAsync<int>(
                    "SELECT COUNT(*) FROM Users WHERE CreatedOn <= @periodEndDate", new { periodEndDate });

            // PRs Not Approved and Merged - Build PR where clause
            var prWhereConditions = new List<string>();
            if (!string.IsNullOrEmpty(workspace))
                prWhereConditions.Add("r.Workspace = @workspace");
            if (!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase))
                prWhereConditions.Add("r.Slug = @repoSlug");
            
            prWhereConditions.Add("pr.CreatedOn >= @periodStartDate");
            prWhereConditions.Add("pr.CreatedOn <= @periodEndDate");
            prWhereConditions.Add("pr.State != 'MERGED'");
            prWhereConditions.Add("pr.State != 'DECLINED'");
            
            var prWhereClause = "WHERE " + string.Join(" AND ", prWhereConditions);
            
            var prsNotApprovedAndMerged = await connection.QuerySingleOrDefaultAsync<int>(
                $"SELECT COUNT(*) FROM PullRequests pr JOIN Repositories r ON pr.RepositoryId = r.Id {prWhereClause}", 
                new { periodStartDate, periodEndDate, repoSlug, workspace });

            // Total Merged PRs
            var mergedPrWhereConditions = new List<string>();
            if (!string.IsNullOrEmpty(workspace))
                mergedPrWhereConditions.Add("r.Workspace = @workspace");
            if (!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase))
                mergedPrWhereConditions.Add("r.Slug = @repoSlug");
            if (userId.HasValue)
                mergedPrWhereConditions.Add("pr.AuthorId = @userId");
            if (teamId.HasValue && teamId.Value > 0)
                mergedPrWhereConditions.Add("pr.AuthorId IN (SELECT UserId FROM TeamMembers WHERE TeamId = @teamId)");
            
            mergedPrWhereConditions.Add("pr.MergedOn >= @periodStartDate");
            mergedPrWhereConditions.Add("pr.MergedOn <= @periodEndDate");
            mergedPrWhereConditions.Add("pr.State = 'MERGED'");
            
            var mergedPrWhereClause = "WHERE " + string.Join(" AND ", mergedPrWhereConditions);
            
            var totalMergedPrs = await connection.QuerySingleOrDefaultAsync<int>(
                $"SELECT COUNT(*) FROM PullRequests pr JOIN Repositories r ON pr.RepositoryId = r.Id {mergedPrWhereClause}",
                new { periodStartDate, periodEndDate, repoSlug, workspace, userId, teamId });

            // PRs Approved - Count unique PRs that were approved AND merged in this period
            // This ensures we count meaningful approvals that led to actual PR completion
            var prsApprovedSql = $@"
                SELECT COUNT(DISTINCT pr.Id)
                FROM PullRequests pr
                JOIN Repositories r ON pr.RepositoryId = r.Id
                WHERE pr.State = 'MERGED'
                    AND pr.MergedOn >= @periodStartDate 
                    AND pr.MergedOn <= @periodEndDate
                    AND EXISTS (
                        SELECT 1 FROM PullRequestApprovals pra 
                        WHERE pra.PullRequestId = pr.Id 
                        AND pra.Approved = 1
                    )
                    {(!string.IsNullOrEmpty(workspace) ? " AND r.Workspace = @workspace" : "")}
                    {(!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase) ? " AND r.Slug = @repoSlug" : "")}";
            
            // Add user/team filtering for PRs approved
            if (userId.HasValue)
            {
                prsApprovedSql += @" AND EXISTS (
                    SELECT 1 FROM PullRequestApprovals pra2
                    JOIN Users u ON pra2.UserUuid = u.BitbucketUserId 
                    WHERE pra2.PullRequestId = pr.Id 
                    AND pra2.Approved = 1
                    AND u.Id = @userId)";
            }
            else if (teamId.HasValue && teamId.Value > 0)
            {
                prsApprovedSql += @" AND EXISTS (
                    SELECT 1 FROM PullRequestApprovals pra2
                    JOIN Users u ON pra2.UserUuid = u.BitbucketUserId 
                    JOIN TeamMembers tm ON u.Id = tm.UserId 
                    WHERE pra2.PullRequestId = pr.Id 
                    AND pra2.Approved = 1
                    AND tm.TeamId = @teamId)";
            }
            
            var prsApproved = await connection.QuerySingleOrDefaultAsync<int>(
                prsApprovedSql, new { periodStartDate, periodEndDate, repoSlug, workspace, userId, teamId });

            // Calculate line metrics
            var excludeCondition = showExcluded ? "" : " AND cf.ExcludeFromReporting = 0";
            
            // Total lines added/removed
            var totalLinesSql = $@"
                SELECT 
                    ISNULL(SUM(c.LinesAdded), 0) AS TotalLinesAdded,
                    ISNULL(SUM(c.LinesRemoved), 0) AS TotalLinesRemoved
                FROM Commits c
                JOIN Repositories r ON c.RepositoryId = r.Id
                {whereClause}";
            
            var totalLines = await connection.QuerySingleOrDefaultAsync<(int TotalLinesAdded, int TotalLinesRemoved)>(
                totalLinesSql, new { periodStartDate, periodEndDate, repoSlug, workspace, userId, teamId });
            
            // Code lines added/removed
            var codeLinesSql = $@"
                SELECT 
                    ISNULL(SUM(cf.LinesAdded), 0) AS CodeLinesAdded,
                    ISNULL(SUM(cf.LinesRemoved), 0) AS CodeLinesRemoved
                FROM Commits c
                JOIN Repositories r ON c.RepositoryId = r.Id
                LEFT JOIN CommitFiles cf ON c.Id = cf.CommitId AND cf.FileType = 'code' {excludeCondition}
                {whereClause}";
            
            var codeLines = await connection.QuerySingleOrDefaultAsync<(int CodeLinesAdded, int CodeLinesRemoved)>(
                codeLinesSql, new { periodStartDate, periodEndDate, repoSlug, workspace, userId, teamId });

            return new PeriodStats
            {
                StartDate = periodStartDate,
                EndDate = periodEndDate,
                TotalCommits = totalCommits,
                RepositoriesUpdated = repositoriesUpdated,
                ActiveContributingUsers = activeContributingUsers,
                TotalLicensedUsers = totalLicensedUsers, 
                PrsNotApprovedAndMerged = prsNotApprovedAndMerged,
                TotalMergedPrs = totalMergedPrs,
                PrsApproved = prsApproved,
                TotalLinesAdded = totalLines.TotalLinesAdded,
                TotalLinesRemoved = totalLines.TotalLinesRemoved,
                CodeLinesAdded = codeLines.CodeLinesAdded,
                CodeLinesRemoved = codeLines.CodeLinesRemoved
            };
        }

        private async Task<PrAgeGraph> GetPrAgeGraphData(SqlConnection connection, DateTime periodStartDate, DateTime periodEndDate, string? repoSlug, string? workspace)
        {
            var openPrAgeData = new List<PrAgeDataPoint>();
            var mergedPrAgeData = new List<PrAgeDataPoint>();

            var repoWhereClause = string.IsNullOrEmpty(repoSlug) ? "" : " AND RepositoryId = (SELECT Id FROM Repositories WHERE Slug = @repoSlug)";

            // Open PRs Age
            var openPrs = await connection.QueryAsync<DateTime>($@"
                SELECT pr.CreatedOn 
                FROM PullRequests pr 
                JOIN Repositories r ON pr.RepositoryId = r.Id
                WHERE pr.CreatedOn >= CAST(@periodStartDate AS DATETIME2) AND pr.CreatedOn <= CAST(@periodEndDate AS DATETIME2) AND pr.State = 'OPEN'
                {(!string.IsNullOrEmpty(workspace) ? " AND r.Workspace = @workspace" : "")}
                {(!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase) ? " AND r.Slug = @repoSlug" : "")}",
                new { periodStartDate, periodEndDate, repoSlug, workspace });


            foreach (var createdOn in openPrs)
            {
                var ageInDays = (int)(DateTime.Today - createdOn).TotalDays;
                var existingPoint = openPrAgeData.FirstOrDefault(p => p.Days == ageInDays);
                if (existingPoint == null)
                {
                    openPrAgeData.Add(new PrAgeDataPoint { Days = ageInDays, PrCount = 1 });
                }
                else
                {
                    existingPoint.PrCount++;
                }
            }
            openPrAgeData = openPrAgeData.OrderBy(p => p.Days).ToList();

            // Merged PRs Age
            var mergedPrs = await connection.QueryAsync<(DateTime CreatedOn, DateTime MergedOn)>($@"
                SELECT pr.CreatedOn, pr.MergedOn 
                FROM PullRequests pr 
                JOIN Repositories r ON pr.RepositoryId = r.Id
                WHERE pr.MergedOn >= CAST(@periodStartDate AS DATETIME2) AND pr.MergedOn <= CAST(@periodEndDate AS DATETIME2) AND pr.State = 'MERGED'
                {(!string.IsNullOrEmpty(workspace) ? " AND r.Workspace = @workspace" : "")}
                {(!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase) ? " AND r.Slug = @repoSlug" : "")}",
                new { periodStartDate, periodEndDate, repoSlug, workspace });

            foreach (var pr in mergedPrs)
            {
                var ageInDays = (int)(pr.MergedOn - pr.CreatedOn).TotalDays;
                var existingPoint = mergedPrAgeData.FirstOrDefault(p => p.Days == ageInDays);
                if (existingPoint == null)
                {
                    mergedPrAgeData.Add(new PrAgeDataPoint { Days = ageInDays, PrCount = 1 });
                }
                else
                {
                    existingPoint.PrCount++;
                }
            }
            mergedPrAgeData = mergedPrAgeData.OrderBy(p => p.Days).ToList();

            return new PrAgeGraph
            {
                OpenPrAge = openPrAgeData,
                MergedPrAge = mergedPrAgeData
            };
        }

        private async Task<List<ContributorStats>> GetTopContributors(SqlConnection connection, DateTime periodStartDate, DateTime periodEndDate, string? repoSlug, string? workspace, int? userId, int? teamId, bool includePR, bool showExcluded)
        {
            // Build WHERE clause similar to Commits API
            var whereConditions = new List<string> { "c.IsRevert = 0" };
            
            if (!string.IsNullOrEmpty(workspace))
                whereConditions.Add("r.Workspace = @workspace");
            if (!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase))
                whereConditions.Add("r.Slug = @repoSlug");
            if (userId.HasValue)
                whereConditions.Add("c.AuthorId = @userId");
            if (teamId.HasValue && teamId.Value > 0)  // Changed from != -1 to > 0
                whereConditions.Add("c.AuthorId IN (SELECT UserId FROM TeamMembers WHERE TeamId = @teamId)");
            if (!includePR)
                whereConditions.Add("c.IsPRMergeCommit = 0");
            
            whereConditions.Add("c.Date >= @periodStartDate");
            whereConditions.Add("c.Date <= @periodEndDate");
            
            var whereClause = "WHERE " + string.Join(" AND ", whereConditions);
            
            var topContributors = await connection.QueryAsync<ContributorStats>($@"
                SELECT u.DisplayName AS UserName, COUNT(c.Id) AS Commits,
                       ISNULL(SUM(cf.LinesAdded), 0) AS CodeLinesAdded,
                       ISNULL(SUM(cf.LinesRemoved), 0) AS CodeLinesRemoved
                FROM Commits c
                JOIN Users u ON c.AuthorId = u.Id
                JOIN Repositories r ON c.RepositoryId = r.Id
                LEFT JOIN CommitFiles cf ON c.Id = cf.CommitId AND cf.FileType = 'code' AND cf.ExcludeFromReporting = 0
                {whereClause}
                GROUP BY u.DisplayName
                ORDER BY Commits DESC, CodeLinesAdded DESC
            ", new { periodStartDate, periodEndDate, repoSlug, workspace, userId, teamId });

            return topContributors.ToList();
        }

        private async Task<int> GetUsersWithNoActivity(SqlConnection connection, DateTime periodStartDate, DateTime periodEndDate, string? repoSlug, string? workspace, int? userId, int? teamId, bool includePR, bool showExcluded)
        {
            // If filtering by team, we need to check within the team members only
            var teamFilter = (teamId.HasValue && teamId.Value > 0) ? " AND c.AuthorId IN (SELECT UserId FROM TeamMembers WHERE TeamId = @teamId)" : "";
            var teamFilterPr = (teamId.HasValue && teamId.Value > 0) ? " AND pr.AuthorId IN (SELECT UserId FROM TeamMembers WHERE TeamId = @teamId)" : "";
            var teamFilterApproval = (teamId.HasValue && teamId.Value > 0) ? " AND u.Id IN (SELECT UserId FROM TeamMembers WHERE TeamId = @teamId)" : "";

            // Get the count of distinct users who had activity in the period
            var activeUserCount = await connection.QuerySingleAsync<int>($@"
                SELECT COUNT(DISTINCT UserId)
                FROM (
                    -- Commits
                    SELECT c.AuthorId AS UserId 
                    FROM Commits c 
                    JOIN Repositories r ON c.RepositoryId = r.Id
                    WHERE c.Date >= @periodStartDate AND c.Date <= @periodEndDate AND c.IsRevert = 0
                    {(!string.IsNullOrEmpty(workspace) ? " AND r.Workspace = @workspace" : "")}
                    {(!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase) ? " AND r.Slug = @repoSlug" : "")}
                    {teamFilter}
                    UNION
                    -- PRs Created
                    SELECT pr.AuthorId AS UserId 
                    FROM PullRequests pr 
                    JOIN Repositories r ON pr.RepositoryId = r.Id
                    WHERE pr.CreatedOn >= @periodStartDate AND pr.CreatedOn <= @periodEndDate
                    {(!string.IsNullOrEmpty(workspace) ? " AND r.Workspace = @workspace" : "")}
                    {(!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase) ? " AND r.Slug = @repoSlug" : "")}
                    {teamFilterPr}
                    UNION
                    -- PRs Approved
                    SELECT u.Id AS UserId
                    FROM PullRequestApprovals pra
                    JOIN PullRequests pr ON pra.PullRequestId = pr.Id
                    JOIN Users u ON pra.UserUuid = u.BitbucketUserId
                    JOIN Repositories r ON pr.RepositoryId = r.Id
                    WHERE pra.ApprovedOn >= @periodStartDate AND pra.ApprovedOn <= @periodEndDate AND pra.Approved = 1
                    {(!string.IsNullOrEmpty(workspace) ? " AND r.Workspace = @workspace" : "")}
                    {(!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase) ? " AND r.Slug = @repoSlug" : "")}
                    {teamFilterApproval}
                ) AS UserActivity
            ", new { periodStartDate, periodEndDate, repoSlug, workspace, teamId });

            // Get the total number of users (or team members if filtering by team)
            var totalUserCount = (teamId.HasValue && teamId.Value > 0) 
                ? await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM TeamMembers WHERE TeamId = @teamId", new { teamId })
                : await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM Users");

            // Inactive users = Total users - Active users
            return totalUserCount - activeUserCount;
        }

        private async Task<List<ApproverStats>> GetTopApprovers(SqlConnection connection, DateTime periodStartDate, DateTime periodEndDate, string? repoSlug, string? workspace, int? teamId)
        {
            var repoWhereClause = string.IsNullOrEmpty(repoSlug) ? "" : " AND pr.RepositoryId = (SELECT Id FROM Repositories WHERE Slug = @repoSlug)";
            var topApprovers = await connection.QueryAsync<ApproverStats>($@"
                SELECT u.DisplayName AS UserName, COUNT(pra.Id) AS PrApprovalCount
                FROM PullRequestApprovals pra
                JOIN PullRequests pr ON pra.PullRequestId = pr.Id
                JOIN Users u ON pra.UserUuid = u.BitbucketUserId
                JOIN Repositories r ON pr.RepositoryId = r.Id
                WHERE pr.CreatedOn >= @periodStartDate AND pr.CreatedOn <= @periodEndDate
                {(!string.IsNullOrEmpty(workspace) ? " AND r.Workspace = @workspace" : "")}
                {(!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase) ? " AND r.Slug = @repoSlug" : "")}
                {(teamId.HasValue && teamId.Value > 0 ? " AND u.Id IN (SELECT UserId FROM TeamMembers WHERE TeamId = @teamId)" : "")}
                GROUP BY u.DisplayName
                ORDER BY PrApprovalCount DESC
            ", new { periodStartDate, periodEndDate, repoSlug, workspace, teamId });

            return topApprovers.ToList();
        }

        private async Task<PrsMergedByWeekdayData> GetPrsMergedByWeekdayData(SqlConnection connection, DateTime periodStartDate, DateTime periodEndDate, string? repoSlug, string? workspace)
        {
            var repoWhereClause = string.IsNullOrEmpty(repoSlug) ? "" : " AND RepositoryId = (SELECT Id FROM Repositories WHERE Slug = @repoSlug)";
            var query = $@"
                SELECT DATENAME(dw, MergedOn) AS DayOfWeek, COUNT(*) AS PrCount
                FROM PullRequests pr
                JOIN Repositories r ON pr.RepositoryId = r.Id
                WHERE pr.MergedOn >= CAST(@periodStartDate AS DATETIME2) AND pr.MergedOn <= CAST(@periodEndDate AS DATETIME2) AND pr.State = 'MERGED'
                {(!string.IsNullOrEmpty(workspace) ? " AND r.Workspace = @workspace" : "")}
                {(!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase) ? " AND r.Slug = @repoSlug" : "")}
                GROUP BY DATENAME(dw, MergedOn), DATEPART(dw, MergedOn)
                ORDER BY DATEPART(dw, MergedOn);
            ";

            var mergedPrs = await connection.QueryAsync<WeekdayPrCount>(query, new { periodStartDate, periodEndDate, repoSlug, workspace });

            return new PrsMergedByWeekdayData
            {
                MergedPrsByWeekday = mergedPrs.ToList()
            };
        }

        [HttpGet("users-with-no-activity-details")]
        public async Task<IActionResult> GetUsersWithNoActivityDetails(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? repoSlug = null,
            string? workspace = null,
            int? userId = null,
            int? teamId = null,
            bool includePR = false,
            bool showExcluded = false)
        {
            try
            {
                // Handle date ranges (same logic as main endpoint)
                if (!startDate.HasValue && !endDate.HasValue)
                {
                    using var tempConnection = new SqlConnection(_connectionString);
                    await tempConnection.OpenAsync();
                    
                    startDate = await tempConnection.QuerySingleOrDefaultAsync<DateTime?>(
                        "SELECT MIN(Date) FROM Commits c JOIN Repositories r ON c.RepositoryId = r.Id WHERE (@workspace IS NULL OR r.Workspace = @workspace)",
                        new { workspace }) ?? DateTime.Today.AddYears(-10);
                        
                    endDate = DateTime.Today;
                }
                else if (!startDate.HasValue || !endDate.HasValue)
                {
                    using var tempConnection = new SqlConnection(_connectionString);
                    await tempConnection.OpenAsync();
                    
                    if (!startDate.HasValue)
                    {
                        startDate = await tempConnection.QuerySingleOrDefaultAsync<DateTime?>(
                            "SELECT MIN(Date) FROM Commits c JOIN Repositories r ON c.RepositoryId = r.Id WHERE (@workspace IS NULL OR r.Workspace = @workspace)",
                            new { workspace }) ?? DateTime.Today.AddYears(-10);
                    }
                    
                    if (!endDate.HasValue)
                    {
                        endDate = DateTime.Today;
                    }
                }

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Get users with no activity in the period
                var usersWithNoActivity = await GetUsersWithNoActivityDetails(connection, startDate.Value, endDate.Value, repoSlug, workspace, userId, teamId, includePR, showExcluded);

                return Ok(usersWithNoActivity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users with no activity details");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private async Task<List<UserDetailsDto>> GetUsersWithNoActivityDetails(SqlConnection connection, DateTime periodStartDate, DateTime periodEndDate, string? repoSlug, string? workspace, int? userId, int? teamId, bool includePR, bool showExcluded)
        {
            // If filtering by team, we need to get team members only
            var teamFilter = (teamId.HasValue && teamId.Value > 0) ? " AND u.Id IN (SELECT UserId FROM TeamMembers WHERE TeamId = @teamId)" : "";
            var teamFilterCommits = (teamId.HasValue && teamId.Value > 0) ? " AND c.AuthorId IN (SELECT UserId FROM TeamMembers WHERE TeamId = @teamId)" : "";
            var teamFilterPr = (teamId.HasValue && teamId.Value > 0) ? " AND pr.AuthorId IN (SELECT UserId FROM TeamMembers WHERE TeamId = @teamId)" : "";
            var teamFilterApproval = (teamId.HasValue && teamId.Value > 0) ? " AND u.Id IN (SELECT UserId FROM TeamMembers WHERE TeamId = @teamId)" : "";

            // Get all users (or team members if filtering by team) with their last activity
            var sql = $@"
                WITH UserActivity AS (
                    -- Commits
                    SELECT c.AuthorId AS UserId, 
                           c.Date AS ActivityDate,
                           'commit' AS ActivityType
                    FROM Commits c 
                    JOIN Repositories r ON c.RepositoryId = r.Id
                    WHERE c.IsRevert = 0
                    {(!string.IsNullOrEmpty(workspace) ? " AND r.Workspace = @workspace" : "")}
                    {(!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase) ? " AND r.Slug = @repoSlug" : "")}
                    {teamFilterCommits}
                    
                    UNION ALL
                    
                    -- PRs Created
                    SELECT pr.AuthorId AS UserId, 
                           pr.CreatedOn AS ActivityDate,
                           'pr_created' AS ActivityType
                    FROM PullRequests pr 
                    JOIN Repositories r ON pr.RepositoryId = r.Id
                    WHERE 1=1
                    {(!string.IsNullOrEmpty(workspace) ? " AND r.Workspace = @workspace" : "")}
                    {(!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase) ? " AND r.Slug = @repoSlug" : "")}
                    {teamFilterPr}
                    
                    UNION ALL
                    
                    -- PRs Approved
                    SELECT u.Id AS UserId,
                           pra.ApprovedOn AS ActivityDate,
                           'pr_approved' AS ActivityType
                    FROM PullRequestApprovals pra
                    JOIN PullRequests pr ON pra.PullRequestId = pr.Id
                    JOIN Users u ON pra.UserUuid = u.BitbucketUserId
                    JOIN Repositories r ON pr.RepositoryId = r.Id
                    WHERE pra.Approved = 1 AND pra.ApprovedOn IS NOT NULL
                    {(!string.IsNullOrEmpty(workspace) ? " AND r.Workspace = @workspace" : "")}
                    {(!string.IsNullOrEmpty(repoSlug) && !string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase) ? " AND r.Slug = @repoSlug" : "")}
                    {teamFilterApproval}
                ),
                UserLastActivity AS (
                    SELECT UserId,
                           ActivityDate,
                           ActivityType,
                           ROW_NUMBER() OVER (PARTITION BY UserId ORDER BY ActivityDate DESC) AS rn
                    FROM UserActivity
                )
                SELECT u.Id, 
                       u.DisplayName, 
                       u.AvatarUrl,
                       COALESCE(ula.ActivityDate, u.CreatedOn) AS LastActivityDate,
                       COALESCE(ula.ActivityType, 'none') AS ActivityType,
                       DATEDIFF(DAY, COALESCE(ula.ActivityDate, u.CreatedOn), GETUTCDATE()) AS DaysSinceLastActivity
                FROM Users u
                LEFT JOIN UserLastActivity ula ON u.Id = ula.UserId AND ula.rn = 1
                WHERE (ula.ActivityDate IS NULL OR ula.ActivityDate < @periodStartDate)
                {teamFilter}
                ORDER BY DaysSinceLastActivity DESC, u.DisplayName";

            var results = await connection.QueryAsync<UserDetailsDto>(sql, new { 
                periodStartDate, periodEndDate, repoSlug, workspace, teamId 
            });

            return results.ToList();
        }
    }
} 