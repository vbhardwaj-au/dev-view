/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using Data.Models;
using Entities.DTOs.Analytics;
using Entities.DTOs.Teams;
using Integration.Common;
using Dapper;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Services
{
    public class AnalyticsService
    {
        private readonly string _connectionString;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(BitbucketConfig config, ILogger<AnalyticsService> logger)
        {
            _connectionString = config.DbConnectionString;
            _logger = logger;
        }

        /// <summary>
        /// Generates SQL subqueries for line counts that exclude files with ExcludeFromReporting = true
        /// </summary>
        private static string GetLineCountSubquery(string fileType, string lineType, bool showExcluded = false, string commitAlias = "c")
        {
            var excludeClause = showExcluded ? "" : " AND cf.ExcludeFromReporting = 0";
            return $"ISNULL((SELECT SUM(cf.{lineType}) FROM CommitFiles cf WHERE cf.CommitId = {commitAlias}.Id AND cf.FileType = '{fileType}'{excludeClause}), 0)";
        }

        /// <summary>
        /// Generates SQL subqueries for aggregated line counts that exclude files with ExcludeFromReporting = true
        /// </summary>
        private static string GetAggregatedLineCountSubquery(string fileType, string lineType, bool showExcluded = false, string commitAlias = "c")
        {
            var excludeClause = showExcluded ? "" : " AND cf.ExcludeFromReporting = 0";
            return $"SUM(ISNULL((SELECT SUM(cf.{lineType}) FROM CommitFiles cf WHERE cf.CommitId = {commitAlias}.Id AND cf.FileType = '{fileType}'{excludeClause}), 0))";
        }

        private static string GetFilterClause(bool includePR = true)
        {
            var prFilter = includePR ? "" : "c.IsPRMergeCommit = 0 AND ";
            return $@"
                WHERE 
                    {prFilter}(@RepoSlug IS NULL OR r.Slug = @RepoSlug)
                    AND (@Workspace IS NULL OR r.Workspace = @Workspace)
                    AND (@UserId IS NULL OR c.AuthorId = @UserId)
                    AND (@TeamId IS NULL OR c.AuthorId IN (SELECT tm.UserId FROM TeamMembers tm WHERE tm.TeamId = @TeamId))
                    AND (@StartDate IS NULL OR c.Date >= @StartDate)
                    AND (@EndDate IS NULL OR c.Date <= @EndDate)
                    AND c.IsRevert = 0
                    AND r.ExcludeFromReporting = 0
            ";
        }

        public async Task<IEnumerable<CommitActivityDto>> GetCommitActivityAsync(
            string? repoSlug, string? workspace, DateTime? startDate, DateTime? endDate, GroupingType groupBy, int? userId, int? teamId = null,
            bool includePR = true, bool includeData = true, bool includeConfig = true, bool showExcluded = false)
        {
            using var connection = new SqlConnection(_connectionString);
            
            string dateTrunc;
            switch (groupBy)
            {
                case GroupingType.Day:
                    dateTrunc = "day";
                    break;
                case GroupingType.Month:
                    dateTrunc = "month";
                    break;
                case GroupingType.Week:
                default:
                    dateTrunc = "week";
                    break;
            }

            var sql = $@"
                WITH CommitFiles_Aggregated AS (
                    SELECT 
                        c.Id AS CommitId,
                        FileType,
                        SUM(cf.LinesAdded) AS LinesAdded,
                        SUM(cf.LinesRemoved) AS LinesRemoved
                    FROM Commits c
                    JOIN CommitFiles cf ON c.Id = cf.CommitId
                    {(showExcluded ? "" : "WHERE cf.ExcludeFromReporting = 0")}
                    GROUP BY c.Id, FileType
                )
                SELECT 
                    CAST(DATEADD({dateTrunc}, DATEDIFF({dateTrunc}, 0, c.Date), 0) AS DATE) AS Date,
                    COUNT(c.Id) AS CommitCount,
                    SUM(
                        ISNULL(cf_code.LinesAdded, 0)
                        + CASE WHEN @includeData = 1 THEN ISNULL(cf_data.LinesAdded, 0) ELSE 0 END
                        + CASE WHEN @includeConfig = 1 THEN ISNULL(cf_config.LinesAdded, 0) ELSE 0 END
                        + ISNULL(cf_docs.LinesAdded, 0)
                    ) AS TotalLinesAdded,
                    SUM(
                        ISNULL(cf_code.LinesRemoved, 0)
                        + CASE WHEN @includeData = 1 THEN ISNULL(cf_data.LinesRemoved, 0) ELSE 0 END
                        + CASE WHEN @includeConfig = 1 THEN ISNULL(cf_config.LinesRemoved, 0) ELSE 0 END
                        + ISNULL(cf_docs.LinesRemoved, 0)
                    ) AS TotalLinesRemoved,
                    SUM(ISNULL(cf_code.LinesAdded, 0)) AS CodeLinesAdded,
                    SUM(ISNULL(cf_code.LinesRemoved, 0)) AS CodeLinesRemoved,
                    SUM(ISNULL(cf_data.LinesAdded, 0)) AS DataLinesAdded,
                    SUM(ISNULL(cf_data.LinesRemoved, 0)) AS DataLinesRemoved,
                    SUM(ISNULL(cf_config.LinesAdded, 0)) AS ConfigLinesAdded,
                    SUM(ISNULL(cf_config.LinesRemoved, 0)) AS ConfigLinesRemoved,
                    SUM(ISNULL(cf_docs.LinesAdded, 0)) AS DocsLinesAdded,
                    SUM(ISNULL(cf_docs.LinesRemoved, 0)) AS DocsLinesRemoved,
                    MAX(CAST(c.IsMerge AS INT)) AS IsMergeCommit
                FROM Commits c
                JOIN Repositories r ON c.RepositoryId = r.Id
                LEFT JOIN CommitFiles_Aggregated cf_code ON c.Id = cf_code.CommitId AND cf_code.FileType = 'code'
                LEFT JOIN CommitFiles_Aggregated cf_data ON c.Id = cf_data.CommitId AND cf_data.FileType = 'data'
                LEFT JOIN CommitFiles_Aggregated cf_config ON c.Id = cf_config.CommitId AND cf_config.FileType = 'config'
                LEFT JOIN CommitFiles_Aggregated cf_docs ON c.Id = cf_docs.CommitId AND cf_docs.FileType = 'docs'
                {GetFilterClause(includePR)}
                GROUP BY CAST(DATEADD({dateTrunc}, DATEDIFF({dateTrunc}, 0, c.Date), 0) AS DATE)
                ORDER BY Date;
            ";

            return await connection.QueryAsync<CommitActivityDto>(sql, new { repoSlug, workspace, startDate, endDate, userId, teamId, includeData, includeConfig });
        }

        public async Task<IEnumerable<ContributorActivityDto>> GetContributorActivityAsync(
            string? repoSlug, string? workspace, DateTime? startDate, DateTime? endDate, GroupingType groupBy, int? userId, int? teamId = null,
            bool includePR = true, bool includeData = true, bool includeConfig = true, bool showExcluded = false)
        {
            using var connection = new SqlConnection(_connectionString);

            string dateTrunc;
            switch (groupBy)
            {
                case GroupingType.Day:
                    dateTrunc = "day";
                    break;
                case GroupingType.Month:
                    dateTrunc = "month";
                    break;
                case GroupingType.Week:
                default:
                    dateTrunc = "week";
                    break;
            }

            var sql = $@"
                WITH CommitFiles_Aggregated AS (
                    SELECT 
                        c.Id AS CommitId,
                        c.AuthorId,
                        FileType,
                        SUM(cf.LinesAdded) AS LinesAdded,
                        SUM(cf.LinesRemoved) AS LinesRemoved
                    FROM Commits c
                    JOIN CommitFiles cf ON c.Id = cf.CommitId
                    {(showExcluded ? "" : "WHERE cf.ExcludeFromReporting = 0")}
                    GROUP BY c.Id, c.AuthorId, FileType
                )
                SELECT 
                    CAST(DATEADD({dateTrunc}, DATEDIFF({dateTrunc}, 0, c.Date), 0) AS DATE) AS Date,
                    u.Id AS UserId,
                    u.DisplayName,
                    u.AvatarUrl,
                    COUNT(c.Id) AS CommitCount,
                    SUM(
                        ISNULL(cf_code.LinesAdded, 0)
                        + CASE WHEN @includeData = 1 THEN ISNULL(cf_data.LinesAdded, 0) ELSE 0 END
                        + CASE WHEN @includeConfig = 1 THEN ISNULL(cf_config.LinesAdded, 0) ELSE 0 END
                        + ISNULL(cf_docs.LinesAdded, 0)
                    ) AS TotalLinesAdded,
                    SUM(
                        ISNULL(cf_code.LinesRemoved, 0)
                        + CASE WHEN @includeData = 1 THEN ISNULL(cf_data.LinesRemoved, 0) ELSE 0 END
                        + CASE WHEN @includeConfig = 1 THEN ISNULL(cf_config.LinesRemoved, 0) ELSE 0 END
                        + ISNULL(cf_docs.LinesRemoved, 0)
                    ) AS TotalLinesRemoved,
                    SUM(ISNULL(cf_code.LinesAdded, 0)) AS CodeLinesAdded,
                    SUM(ISNULL(cf_code.LinesRemoved, 0)) AS CodeLinesRemoved,
                    SUM(ISNULL(cf_data.LinesAdded, 0)) AS DataLinesAdded,
                    SUM(ISNULL(cf_data.LinesRemoved, 0)) AS DataLinesRemoved,
                    SUM(ISNULL(cf_config.LinesAdded, 0)) AS ConfigLinesAdded,
                    SUM(ISNULL(cf_config.LinesRemoved, 0)) AS ConfigLinesRemoved,
                    SUM(ISNULL(cf_docs.LinesAdded, 0)) AS DocsLinesAdded,
                    SUM(ISNULL(cf_docs.LinesRemoved, 0)) AS DocsLinesRemoved,
                    MAX(CAST(c.IsMerge AS INT)) AS IsMergeCommit
                FROM Commits c
                JOIN Repositories r ON c.RepositoryId = r.Id
                JOIN Users u ON c.AuthorId = u.Id
                LEFT JOIN CommitFiles_Aggregated cf_code ON c.Id = cf_code.CommitId AND cf_code.FileType = 'code'
                LEFT JOIN CommitFiles_Aggregated cf_data ON c.Id = cf_data.CommitId AND cf_data.FileType = 'data'
                LEFT JOIN CommitFiles_Aggregated cf_config ON c.Id = cf_config.CommitId AND cf_config.FileType = 'config'
                LEFT JOIN CommitFiles_Aggregated cf_docs ON c.Id = cf_docs.CommitId AND cf_docs.FileType = 'docs'
                {GetFilterClause(includePR)}
                GROUP BY CAST(DATEADD({dateTrunc}, DATEDIFF({dateTrunc}, 0, c.Date), 0) AS DATE), u.Id, u.DisplayName, u.AvatarUrl
                ORDER BY Date, DisplayName;
            ";

            return await connection.QueryAsync<ContributorActivityDto>(sql, new { repoSlug, workspace, startDate, endDate, userId, teamId, includeData, includeConfig });
        }

        public async Task<List<CommitPunchcardDto>> GetCommitPunchcardAsync(string? workspace = null, string? repoSlug = null, DateTime? startDate = null, DateTime? endDate = null, int? userId = null, int? teamId = null)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                SELECT
                    DATEPART(WEEKDAY, [Date]) - 1 AS DayOfWeek,
                    DATEPART(HOUR, [Date]) AS HourOfDay,
                    COUNT(*) AS CommitCount
                FROM Commits c
                INNER JOIN Repositories r ON c.RepositoryId = r.Id
                WHERE (@workspace IS NULL OR r.Workspace = @workspace)
                  AND (@repoSlug IS NULL OR r.Slug = @repoSlug)
                  AND (@startDate IS NULL OR c.Date >= @startDate)
                  AND (@endDate IS NULL OR c.Date <= @endDate)
                  AND (@userId IS NULL OR c.AuthorId = @userId)
                  AND (@teamId IS NULL OR @teamId <= 0 OR c.AuthorId IN (SELECT UserId FROM TeamMembers WHERE TeamId = @teamId))
                  AND c.IsRevert = 0
                  AND r.ExcludeFromReporting = 0
                GROUP BY DATEPART(WEEKDAY, [Date]) - 1, DATEPART(HOUR, [Date])
                ORDER BY DayOfWeek, HourOfDay;";

            var punchcard = await connection.QueryAsync<CommitPunchcardDto>(sql, new { workspace, repoSlug, startDate, endDate, userId, teamId });
            return punchcard.ToList();
        }

        public async Task<IEnumerable<RepositorySummaryDto>> GetRepositoriesAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            const string sql = @"
                SELECT 
                    r.Id,
                    r.Name, 
                    r.Slug, 
                    r.Workspace,
                    MIN(c.Date) AS OldestCommitDate,
                    r.LastDeltaSyncDate,
                    r.ExcludeFromSync,
                    r.ExcludeFromReporting
                FROM Repositories r
                LEFT JOIN Commits c ON r.Id = c.RepositoryId
                GROUP BY r.Id, r.Name, r.Slug, r.Workspace, r.LastDeltaSyncDate, r.ExcludeFromSync, r.ExcludeFromReporting
                ORDER BY r.Name;";
            return await connection.QueryAsync<RepositorySummaryDto>(sql);
        }

        public async Task<bool> UpdateRepositoryFlagsAsync(int id, bool? excludeFromSync, bool? excludeFromReporting)
        {
            if (excludeFromSync is null && excludeFromReporting is null)
            {
                return false;
            }

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var updates = new List<string>();
            var parameters = new DynamicParameters();
            parameters.Add("Id", id);
            if (excludeFromSync is not null)
            {
                updates.Add("ExcludeFromSync = @ExcludeFromSync");
                parameters.Add("ExcludeFromSync", excludeFromSync.Value);
            }
            if (excludeFromReporting is not null)
            {
                updates.Add("ExcludeFromReporting = @ExcludeFromReporting");
                parameters.Add("ExcludeFromReporting", excludeFromReporting.Value);
            }

            var sql = $"UPDATE Repositories SET {string.Join(", ", updates)} WHERE Id = @Id";
            var rows = await connection.ExecuteAsync(sql, parameters);
            return rows > 0;
        }

        public async Task<IEnumerable<UserDto>> GetUsersAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            const string sql = "SELECT Id, BitbucketUserId, DisplayName, AvatarUrl, CreatedOn, ExcludeFromReporting FROM Users WHERE ExcludeFromReporting = 0 ORDER BY DisplayName;";
            return await connection.QueryAsync<UserDto>(sql);
        }

        public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            const string sql = "SELECT Id, BitbucketUserId, DisplayName, AvatarUrl, CreatedOn, ExcludeFromReporting FROM Users ORDER BY DisplayName;";
            return await connection.QueryAsync<UserDto>(sql);
        }

        public async Task<IEnumerable<TeamSummaryDto>> GetTeamsAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            const string sql = @"
                SELECT t.Id, t.Name, t.Description, t.CreatedOn, t.IsActive,
                       COUNT(tm.UserId) as MemberCount
                FROM Teams t
                LEFT JOIN TeamMembers tm ON t.Id = tm.TeamId
                WHERE t.IsActive = 1
                GROUP BY t.Id, t.Name, t.Description, t.CreatedOn, t.IsActive
                ORDER BY t.Name;";
            return await connection.QueryAsync<TeamSummaryDto>(sql);
        }

        public async Task<IEnumerable<string>> GetWorkspacesAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            const string sql = "SELECT DISTINCT Workspace FROM Repositories ORDER BY Workspace;";
            return await connection.QueryAsync<string>(sql);
        }

        public async Task<IEnumerable<CommitDetailDto>> GetCommitDetailsAsync(
            string? repoSlug, string? workspace, int userId, DateTime date, DateTime? startDate, DateTime? endDate)
        {
            using var connection = new SqlConnection(_connectionString);

            const string sql = @"
                SELECT 
                    c.Id,
                    c.BitbucketCommitHash AS CommitHash,
                    c.Date,
                    c.Message,
                    u.DisplayName AS AuthorName,
                    r.Name AS RepositoryName,
                    r.Slug AS RepositorySlug,
                    c.LinesAdded,
                    c.LinesRemoved,
                    ISNULL((SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'code' AND cf.ExcludeFromReporting = 0), 0) AS CodeLinesAdded,
                    ISNULL((SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'code' AND cf.ExcludeFromReporting = 0), 0) AS CodeLinesRemoved,
                    ISNULL((SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'data' AND cf.ExcludeFromReporting = 0), 0) AS DataLinesAdded,
                    ISNULL((SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'data' AND cf.ExcludeFromReporting = 0), 0) AS DataLinesRemoved,
                    ISNULL((SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'config' AND cf.ExcludeFromReporting = 0), 0) AS ConfigLinesAdded,
                    ISNULL((SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'config' AND cf.ExcludeFromReporting = 0), 0) AS ConfigLinesRemoved,
                    ISNULL((SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'docs' AND cf.ExcludeFromReporting = 0), 0) AS DocsLinesAdded,
                    ISNULL((SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'docs' AND cf.ExcludeFromReporting = 0), 0) AS DocsLinesRemoved,
                    c.IsMerge
                FROM Commits c
                JOIN Users u ON c.AuthorId = u.Id
                JOIN Repositories r ON c.RepositoryId = r.Id
                WHERE c.AuthorId = @UserId 
                    AND CAST(c.Date AS DATE) = CAST(@Date AS DATE)
                    AND (@RepoSlug IS NULL OR r.Slug = @RepoSlug)
                    AND (@Workspace IS NULL OR r.Workspace = @Workspace)
                    AND (@StartDate IS NULL OR c.Date >= @StartDate)
                    AND (@EndDate IS NULL OR c.Date <= @EndDate)
                    AND c.IsRevert = 0
                ORDER BY c.Date DESC;
            ";

            return await connection.QueryAsync<CommitDetailDto>(sql, new { repoSlug, workspace, userId, date, startDate, endDate });
        }

        public async Task<FileClassificationSummaryDto> GetFileClassificationSummaryAsync(
            string? repoSlug, string? workspace, DateTime? startDate, DateTime? endDate, int? userId, bool includePR = true)
        {
            using var connection = new SqlConnection(_connectionString);

            var sql = $@"
                SELECT 
                    COUNT(c.Id) AS TotalCommits,
                    SUM(c.LinesAdded) AS TotalLinesAdded,
                    SUM(c.LinesRemoved) AS TotalLinesRemoved,
                    
                    {GetAggregatedLineCountSubquery("code", "LinesAdded")} AS CodeLinesAdded,
                    {GetAggregatedLineCountSubquery("code", "LinesRemoved")} AS CodeLinesRemoved,
                    COUNT(CASE WHEN (SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'code' AND cf.ExcludeFromReporting = 0) > 0 
                              OR (SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'code' AND cf.ExcludeFromReporting = 0) > 0 THEN 1 END) AS CodeCommits,
                    
                    {GetAggregatedLineCountSubquery("data", "LinesAdded")} AS DataLinesAdded,
                    {GetAggregatedLineCountSubquery("data", "LinesRemoved")} AS DataLinesRemoved,
                    COUNT(CASE WHEN (SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'data' AND cf.ExcludeFromReporting = 0) > 0 
                              OR (SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'data' AND cf.ExcludeFromReporting = 0) > 0 THEN 1 END) AS DataCommits,
                    
                    {GetAggregatedLineCountSubquery("config", "LinesAdded")} AS ConfigLinesAdded,
                    {GetAggregatedLineCountSubquery("config", "LinesRemoved")} AS ConfigLinesRemoved,
                    COUNT(CASE WHEN (SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'config' AND cf.ExcludeFromReporting = 0) > 0 
                              OR (SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'config' AND cf.ExcludeFromReporting = 0) > 0 THEN 1 END) AS ConfigCommits,
                    
                    {GetAggregatedLineCountSubquery("docs", "LinesAdded")} AS DocsLinesAdded,
                    {GetAggregatedLineCountSubquery("docs", "LinesRemoved")} AS DocsLinesRemoved,
                    COUNT(CASE WHEN (SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'docs' AND cf.ExcludeFromReporting = 0) > 0 
                              OR (SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'docs' AND cf.ExcludeFromReporting = 0) > 0 THEN 1 END) AS DocsCommits
                FROM Commits c
                JOIN Repositories r ON c.RepositoryId = r.Id
                {GetFilterClause(includePR)}
            ";

            var result = await connection.QuerySingleOrDefaultAsync<FileClassificationSummaryDto>(sql, 
                new { repoSlug, workspace, startDate, endDate, userId });
            
            return result ?? new FileClassificationSummaryDto();
        }

        public async Task<IEnumerable<FileTypeActivityDto>> GetFileTypeActivityAsync(
            string? repoSlug, string? workspace, DateTime? startDate, DateTime? endDate, GroupingType groupBy, int? userId, bool includePR = true, bool showExcluded = false)
        {
            using var connection = new SqlConnection(_connectionString);
            string dateTrunc;
            switch (groupBy)
            {
                case GroupingType.Day:
                    dateTrunc = "day";
                    break;
                case GroupingType.Month:
                    dateTrunc = "month";
                    break;
                case GroupingType.Week:
                default:
                    dateTrunc = "week";
                    break;
            }
            string exclude = showExcluded ? "" : "AND cf.ExcludeFromReporting = 0";
            var sql = $@"
                WITH FileTypeData AS (
                    SELECT 
                        CAST(DATEADD({dateTrunc}, DATEDIFF({dateTrunc}, 0, c.Date), 0) AS DATE) AS Date,
                        'code' AS FileType,
                        COUNT(CASE WHEN (SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'code' {exclude}) > 0 
                                  OR (SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'code' {exclude}) > 0 THEN 1 END) AS CommitCount,
                        {GetAggregatedLineCountSubquery("code", "LinesAdded", showExcluded: showExcluded)} AS LinesAdded,
                        {GetAggregatedLineCountSubquery("code", "LinesRemoved", showExcluded: showExcluded)} AS LinesRemoved
                    FROM Commits c
                    JOIN Repositories r ON c.RepositoryId = r.Id
                    {GetFilterClause(includePR)}
                    GROUP BY CAST(DATEADD({dateTrunc}, DATEDIFF({dateTrunc}, 0, c.Date), 0) AS DATE)
                    
                    UNION ALL
                    
                    SELECT 
                        CAST(DATEADD({dateTrunc}, DATEDIFF({dateTrunc}, 0, c.Date), 0) AS DATE) AS Date,
                        'data' AS FileType,
                        COUNT(CASE WHEN (SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'data' {exclude}) > 0 
                                  OR (SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'data' {exclude}) > 0 THEN 1 END) AS CommitCount,
                        {GetAggregatedLineCountSubquery("data", "LinesAdded", showExcluded: showExcluded)} AS LinesAdded,
                        {GetAggregatedLineCountSubquery("data", "LinesRemoved", showExcluded: showExcluded)} AS LinesRemoved
                    FROM Commits c
                    JOIN Repositories r ON c.RepositoryId = r.Id
                    {GetFilterClause(includePR)}
                    GROUP BY CAST(DATEADD({dateTrunc}, DATEDIFF({dateTrunc}, 0, c.Date), 0) AS DATE)
                    
                    UNION ALL
                    
                    SELECT 
                        CAST(DATEADD({dateTrunc}, DATEDIFF({dateTrunc}, 0, c.Date), 0) AS DATE) AS Date,
                        'config' AS FileType,
                        COUNT(CASE WHEN (SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'config' {exclude}) > 0 
                                  OR (SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'config' {exclude}) > 0 THEN 1 END) AS CommitCount,
                        {GetAggregatedLineCountSubquery("config", "LinesAdded", showExcluded: showExcluded)} AS LinesAdded,
                        {GetAggregatedLineCountSubquery("config", "LinesRemoved", showExcluded: showExcluded)} AS LinesRemoved
                    FROM Commits c
                    JOIN Repositories r ON c.RepositoryId = r.Id
                    {GetFilterClause(includePR)}
                    GROUP BY CAST(DATEADD({dateTrunc}, DATEDIFF({dateTrunc}, 0, c.Date), 0) AS DATE)
                    
                    UNION ALL
                    
                    SELECT 
                        CAST(DATEADD({dateTrunc}, DATEDIFF({dateTrunc}, 0, c.Date), 0) AS DATE) AS Date,
                        'docs' AS FileType,
                        COUNT(CASE WHEN (SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'docs' {exclude}) > 0 
                                  OR (SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'docs' {exclude}) > 0 THEN 1 END) AS CommitCount,
                        {GetAggregatedLineCountSubquery("docs", "LinesAdded", showExcluded: showExcluded)} AS LinesAdded,
                        {GetAggregatedLineCountSubquery("docs", "LinesRemoved", showExcluded: showExcluded)} AS LinesRemoved
                    FROM Commits c
                    JOIN Repositories r ON c.RepositoryId = r.Id
                    {GetFilterClause(includePR)}
                    GROUP BY CAST(DATEADD({dateTrunc}, DATEDIFF({dateTrunc}, 0, c.Date), 0) AS DATE)
                )
                SELECT 
                    Date,
                    FileType,
                    CommitCount,
                    LinesAdded,
                    LinesRemoved,
                    (LinesAdded - LinesRemoved) AS NetLinesChanged
                FROM FileTypeData
                WHERE CommitCount > 0
                ORDER BY Date, FileType;
            ";
            return await connection.QueryAsync<FileTypeActivityDto>(sql, new { repoSlug, workspace, startDate, endDate, userId });
        }

        public async Task<TopCommittersResponseDto> GetTopBottomCommittersAsync(
            string? repoSlug, string? workspace, DateTime? startDate, DateTime? endDate, GroupingType groupBy,
            int? userId = null, int? teamId = null,
            bool includePR = true, bool includeData = true, bool includeConfig = true,
            bool showExcluded = false, int topCount = 5, int bottomCount = 5)
        {
            using var connection = new SqlConnection(_connectionString);

            var whereClause = showExcluded ? "" : "WHERE cf.ExcludeFromReporting = 0";
            var query = $@"
                WITH CommitFiles_Aggregated AS (
                    SELECT 
                        cf.CommitId,
                        cf.FileType,
                        SUM(cf.LinesAdded) as LinesAdded,
                        SUM(cf.LinesRemoved) as LinesRemoved
                    FROM CommitFiles cf
                    {whereClause}
                    GROUP BY cf.CommitId, cf.FileType
                ),
                CommitterStats AS (
                    SELECT 
                        c.AuthorId as UserId,
                        u.DisplayName,
                        u.AvatarUrl,
                        COUNT(DISTINCT c.Id) as TotalCommits,
                        SUM(ISNULL(code.LinesAdded, 0)) as CodeLinesAdded,
                        SUM(ISNULL(code.LinesRemoved, 0)) as CodeLinesRemoved,
                        SUM(ISNULL(data.LinesAdded, 0)) as DataLinesAdded,
                        SUM(ISNULL(data.LinesRemoved, 0)) as DataLinesRemoved,
                        SUM(ISNULL(config.LinesAdded, 0)) as ConfigLinesAdded,
                        SUM(ISNULL(config.LinesRemoved, 0)) as ConfigLinesRemoved,
                        SUM(ISNULL(docs.LinesAdded, 0)) as DocsLinesAdded,
                        SUM(ISNULL(docs.LinesRemoved, 0)) as DocsLinesRemoved,
                        SUM(
                            ISNULL(code.LinesAdded, 0)
                            + CASE WHEN @includeData = 1 THEN ISNULL(data.LinesAdded, 0) ELSE 0 END
                            + CASE WHEN @includeConfig = 1 THEN ISNULL(config.LinesAdded, 0) ELSE 0 END
                            + ISNULL(docs.LinesAdded, 0)
                        ) as TotalLinesAdded,
                        SUM(
                            ISNULL(code.LinesRemoved, 0)
                            + CASE WHEN @includeData = 1 THEN ISNULL(data.LinesRemoved, 0) ELSE 0 END
                            + CASE WHEN @includeConfig = 1 THEN ISNULL(config.LinesRemoved, 0) ELSE 0 END
                            + ISNULL(docs.LinesRemoved, 0)
                        ) as TotalLinesRemoved
                    FROM Commits c
                    INNER JOIN Users u ON c.AuthorId = u.Id
                    INNER JOIN Repositories r ON c.RepositoryId = r.Id
                    LEFT JOIN CommitFiles_Aggregated code ON code.CommitId = c.Id AND code.FileType = 'code'
                    LEFT JOIN CommitFiles_Aggregated data ON data.CommitId = c.Id AND data.FileType = 'data'
                    LEFT JOIN CommitFiles_Aggregated config ON config.CommitId = c.Id AND config.FileType = 'config'
                    LEFT JOIN CommitFiles_Aggregated docs ON docs.CommitId = c.Id AND docs.FileType = 'docs'
                    WHERE (@repoSlug IS NULL OR r.Slug = @repoSlug)
                        AND r.Workspace = @workspace
                        AND (@startDate IS NULL OR c.Date >= @startDate)
                        AND (@endDate IS NULL OR c.Date <= @endDate)
                        AND (@includePR = 1 OR c.IsPRMergeCommit = 0)
                        AND c.IsRevert = 0
                        AND r.ExcludeFromReporting = 0
                        AND (@userId IS NULL OR c.AuthorId = @userId)
                        AND (@teamId IS NULL OR c.AuthorId IN (
                            SELECT tm.UserId 
                            FROM TeamMembers tm 
                            WHERE tm.TeamId = @teamId
                        ))
                    GROUP BY c.AuthorId, u.DisplayName, u.AvatarUrl
                ),
                ActivityData AS (
                    SELECT 
                        c.AuthorId as UserId,
                        DATEADD(DAY, DATEDIFF(DAY, 0, c.Date), 0) as Date,
                        COUNT(DISTINCT c.Id) as CommitCount,
                        SUM(ISNULL(code.LinesAdded, 0)) as CodeLinesAdded,
                        SUM(ISNULL(code.LinesRemoved, 0)) as CodeLinesRemoved,
                        SUM(ISNULL(data.LinesAdded, 0)) as DataLinesAdded,
                        SUM(ISNULL(data.LinesRemoved, 0)) as DataLinesRemoved,
                        SUM(ISNULL(config.LinesAdded, 0)) as ConfigLinesAdded,
                        SUM(ISNULL(config.LinesRemoved, 0)) as ConfigLinesRemoved,
                        SUM(ISNULL(docs.LinesAdded, 0)) as DocsLinesAdded,
                        SUM(ISNULL(docs.LinesRemoved, 0)) as DocsLinesRemoved,
                        c.IsMerge AS IsMergeCommit
                    FROM Commits c
                    INNER JOIN Repositories r ON c.RepositoryId = r.Id
                    LEFT JOIN CommitFiles_Aggregated code ON code.CommitId = c.Id AND code.FileType = 'code'
                    LEFT JOIN CommitFiles_Aggregated data ON data.CommitId = c.Id AND data.FileType = 'data'
                    LEFT JOIN CommitFiles_Aggregated config ON config.CommitId = c.Id AND config.FileType = 'config'
                    LEFT JOIN CommitFiles_Aggregated docs ON docs.CommitId = c.Id AND docs.FileType = 'docs'
                    WHERE (@repoSlug IS NULL OR r.Slug = @repoSlug)
                        AND r.Workspace = @workspace
                        AND (@startDate IS NULL OR c.Date >= @startDate)
                        AND (@endDate IS NULL OR c.Date <= @endDate)
                        AND (@includePR = 1 OR c.IsPRMergeCommit = 0)
                        AND c.IsRevert = 0
                        AND r.ExcludeFromReporting = 0
                        AND (@userId IS NULL OR c.AuthorId = @userId)
                        AND (@teamId IS NULL OR c.AuthorId IN (
                            SELECT tm.UserId 
                            FROM TeamMembers tm 
                            WHERE tm.TeamId = @teamId
                        ))
                    GROUP BY c.AuthorId, DATEADD(DAY, DATEDIFF(DAY, 0, c.Date), 0), c.IsMerge
                ),
                TopCommitters AS (
                    SELECT TOP (@topCount) *
                    FROM CommitterStats
                    ORDER BY TotalLinesAdded DESC
                ),
                BottomCommitters AS (
                    SELECT TOP (@bottomCount) *
                    FROM CommitterStats cs
                    WHERE cs.UserId NOT IN (SELECT UserId FROM TopCommitters)
                    ORDER BY TotalLinesAdded ASC
                )
                SELECT 
                    cs.*,
                    'top' as CommitterType,
                    (
                        SELECT CAST(
                            (
                                SELECT 
                                    ad.Date,
                                    ad.CommitCount,
                                    ad.CodeLinesAdded,
                                    ad.CodeLinesRemoved,
                                    ad.DataLinesAdded,
                                    ad.DataLinesRemoved,
                                    ad.ConfigLinesAdded,
                                    ad.ConfigLinesRemoved,
                                    ad.DocsLinesAdded,
                                    ad.DocsLinesRemoved,
                                    ad.IsMergeCommit
                                FROM ActivityData ad
                                WHERE ad.UserId = cs.UserId
                                ORDER BY ad.Date
                                FOR JSON PATH
                            ) as nvarchar(max)
                        )
                    ) as ActivityData
                FROM TopCommitters cs
                UNION ALL
                SELECT 
                    cs.*,
                    'bottom' as CommitterType,
                    (
                        SELECT CAST(
                            (
                                SELECT 
                                    ad.Date,
                                    ad.CommitCount,
                                    ad.CodeLinesAdded,
                                    ad.CodeLinesRemoved,
                                    ad.DataLinesAdded,
                                    ad.DataLinesRemoved,
                                    ad.ConfigLinesAdded,
                                    ad.ConfigLinesRemoved,
                                    ad.DocsLinesAdded,
                                    ad.DocsLinesRemoved,
                                    ad.IsMergeCommit
                                FROM ActivityData ad
                                WHERE ad.UserId = cs.UserId
                                ORDER BY ad.Date
                                FOR JSON PATH
                            ) as nvarchar(max)
                        )
                    ) as ActivityData
                FROM BottomCommitters cs;";

            var results = (await connection.QueryAsync<TopCommittersDto>(query, new
            {
                repoSlug,
                workspace,
                startDate,
                endDate,
                userId,
                teamId,
                includePR,
                includeData,
                includeConfig,
                topCount,
                bottomCount,
                showExcluded
            })).ToList();

            return new TopCommittersResponseDto
            {
                TopCommitters = results.Where(x => x.CommitterType == "top").ToList(),
                BottomCommitters = results.Where(x => x.CommitterType == "bottom").ToList()
            };
        }

        public async Task<IEnumerable<PullRequestAnalysisDto>> GetPullRequestAnalysisAsync(
            string? repoSlug, string? workspace, DateTime? startDate, DateTime? endDate, string? state = null)
        {
            using var connection = new SqlConnection(_connectionString);

            var sql = @"
                SELECT 
                    pr.Id,
                    pr.Title,
                    pr.State,
                    pr.CreatedOn,
                    pr.UpdatedOn,
                    pr.MergedOn,
                    pr.ClosedOn,
                    u.Id AS AuthorId,
                    u.BitbucketUserId AS AuthorUuid,
                    u.DisplayName AS AuthorDisplayName,
                    u.AvatarUrl AS AuthorAvatarUrl,
                    u.CreatedOn AS AuthorCreatedOn,
                    a.UserId AS ApproverUserId,
                    au.BitbucketUserId AS ApproverUuid,
                    au.DisplayName AS ApproverDisplayName,
                    au.AvatarUrl AS ApproverAvatarUrl,
                    a.Role AS ApproverRole,
                    a.IsApproved,
                    a.ApprovedOn
                FROM PullRequests pr
                JOIN Repositories r ON pr.RepositoryId = r.Id
                JOIN Users u ON pr.AuthorId = u.Id
                LEFT JOIN PullRequestApprovals a ON pr.Id = a.PullRequestId
                LEFT JOIN Users au ON a.UserId = au.Id
                WHERE 
                    (@RepoSlug IS NULL OR r.Slug = @RepoSlug)
                    AND (@Workspace IS NULL OR r.Workspace = @Workspace)
                    AND (@StartDate IS NULL OR pr.CreatedOn >= @StartDate)
                    AND (@EndDate IS NULL OR pr.CreatedOn <= @EndDate)
                    AND (@State IS NULL OR pr.State = @State)
                ORDER BY pr.CreatedOn DESC;";

            var pullRequestDict = new Dictionary<long, PullRequestAnalysisDto>();

            await connection.QueryAsync<PullRequestAnalysisDto, UserDto, PullRequestApproverDto, PullRequestAnalysisDto>(
                sql,
                (pr, author, approver) =>
                {
                    if (!pullRequestDict.TryGetValue(pr.Id, out var existingPr))
                    {
                        existingPr = pr;
                        existingPr.Author = author;
                        existingPr.TimeToMerge = pr.MergedOn.HasValue ? pr.MergedOn.Value - pr.CreatedOn : null;
                        pullRequestDict.Add(pr.Id, existingPr);
                    }

                    if (approver != null)
                    {
                        existingPr.Approvers.Add(approver);
                    }

                    return existingPr;
                },
                new { repoSlug, workspace, startDate, endDate, state },
                splitOn: "AuthorId,ApproverUserId"
            );

            return pullRequestDict.Values;
        }

        public async Task<IEnumerable<RepositorySummaryDto>> GetTopOpenPullRequestsAsync(
            string? repoSlug, string? workspace, DateTime? startDate, DateTime? endDate)
        {
            using var connection = new SqlConnection(_connectionString);

            var sql = @"
                SELECT TOP 5
                    r.Name,
                    r.Slug,
                    r.Workspace,
                    COUNT(pr.Id) AS OpenPullRequestCount
                FROM Repositories r
                JOIN PullRequests pr ON r.Id = pr.RepositoryId
                WHERE pr.State = 'OPEN'
                    AND r.ExcludeFromReporting = 0
                    AND (@RepoSlug IS NULL OR r.Slug = @RepoSlug)
                    AND (@Workspace IS NULL OR r.Workspace = @Workspace)
                    AND (@StartDate IS NULL OR pr.CreatedOn >= @StartDate)
                    AND (@EndDate IS NULL OR pr.CreatedOn <= @EndDate)
                GROUP BY r.Name, r.Slug, r.Workspace
                ORDER BY OpenPullRequestCount DESC;";

            return await connection.QueryAsync<RepositorySummaryDto>(sql, new { repoSlug, workspace, startDate, endDate });
        }

        public async Task<IEnumerable<RepositorySummaryDto>> GetTopOldestOpenPullRequestsAsync(
            string? repoSlug, string? workspace, DateTime? startDate, DateTime? endDate)
        {
            using var connection = new SqlConnection(_connectionString);

            var sql = @"
                SELECT TOP 5
                    r.Name,
                    r.Slug,
                    r.Workspace,
                    MIN(pr.CreatedOn) AS OldestOpenPullRequestDate
                FROM Repositories r
                JOIN PullRequests pr ON r.Id = pr.RepositoryId
                WHERE pr.State = 'OPEN'
                    AND r.ExcludeFromReporting = 0
                    AND (@RepoSlug IS NULL OR r.Slug = @RepoSlug)
                    AND (@Workspace IS NULL OR r.Workspace = @Workspace)
                    AND (@StartDate IS NULL OR pr.CreatedOn >= @StartDate)
                    AND (@EndDate IS NULL OR pr.CreatedOn <= @EndDate)
                GROUP BY r.Name, r.Slug, r.Workspace
                ORDER BY OldestOpenPullRequestDate ASC;";

            return await connection.QueryAsync<RepositorySummaryDto>(sql, new { repoSlug, workspace, startDate, endDate });
        }

        public async Task<IEnumerable<RepositorySummaryDto>> GetTopUnapprovedPullRequestsAsync(
            string? repoSlug,
            string? workspace,
            DateTime? startDate,
            DateTime? endDate)
        {
            var sql = @"SELECT
                            r.Name,
                            COUNT(pr.Id) AS PRsMissingApprovalCount
                        FROM
                            PullRequests pr
                        JOIN
                            Repositories r ON pr.RepositoryId = r.Id
                        LEFT JOIN
                            PullRequestApprovals pa ON pr.Id = pa.PullRequestId AND pa.Approved = 1
                        WHERE
                            r.Workspace = @workspace
                            AND r.ExcludeFromReporting = 0
                            AND (@repoSlug IS NULL OR r.Slug = @repoSlug)
                            AND (@startDate IS NULL OR pr.CreatedOn >= @startDate)
                            AND (@endDate IS NULL OR pr.CreatedOn <= @endDate)
                            AND pr.State = 'OPEN'
                        GROUP BY
                            r.Name
                        HAVING
                            COUNT(pa.Id) = 0
                        ORDER BY
                            PRsMissingApprovalCount DESC
                        OFFSET 0 ROWS FETCH NEXT 5 ROWS ONLY;";

            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<RepositorySummaryDto>(sql, new { repoSlug, workspace, startDate, endDate });
        }

        public async Task<IEnumerable<PrAgeBubbleDto>> GetPrAgeBubbleDataAsync(
            string? repoSlug, string? workspace, DateTime? startDate, DateTime? endDate)
        {
            using var connection = new SqlConnection(_connectionString);

            var sql = @"
                SELECT
                    DATEDIFF(day, pr.CreatedOn, GETUTCDATE()) AS AgeInDays,
                    COUNT(pr.Id) AS NumberOfPRs,
                    r.Name AS RepositoryName,
                    r.Slug AS RepositorySlug,
                    r.Workspace
                FROM PullRequests pr
                JOIN Repositories r ON pr.RepositoryId = r.Id
                WHERE pr.State = 'OPEN'
                    AND r.ExcludeFromReporting = 0
                    AND (@RepoSlug IS NULL OR r.Slug = @RepoSlug)
                    AND (@Workspace IS NULL OR r.Workspace = @Workspace)
                    AND (@StartDate IS NULL OR pr.CreatedOn >= @StartDate)
                    AND (@EndDate IS NULL OR pr.CreatedOn <= @EndDate)
                    AND DATEDIFF(day, pr.CreatedOn, GETUTCDATE()) BETWEEN 1 AND 20 -- PR age between 1 and 20 days
                GROUP BY DATEDIFF(day, pr.CreatedOn, GETUTCDATE()), r.Name, r.Slug, r.Workspace
                ORDER BY AgeInDays ASC;";

            return await connection.QueryAsync<PrAgeBubbleDto>(sql, new { repoSlug, workspace, startDate, endDate });
        }

        public async Task<List<CommitFileDto>> GetCommitFilesForCommitAsync(string commitHash)
        {
            using var connection = new SqlConnection(_connectionString);

            var sql = @"
                SELECT
                    Id,
                    FilePath,
                    FileType,
                    LinesAdded,
                    LinesRemoved,
                    ExcludeFromReporting
                FROM CommitFiles
                WHERE CommitId = (SELECT Id FROM Commits WHERE BitbucketCommitHash = @commitHash);";

            var commitFiles = await connection.QueryAsync<CommitFileDto>(sql, new { commitHash });

            return commitFiles.ToList();
        }

        public async Task UpdateCommitFileAsync(CommitFileUpdateDto updateDto)
        {
            using var connection = new SqlConnection(_connectionString);

            // Basic validation to prevent SQL injection for property name
            var allowedProperties = new List<string> { "ExcludeFromReporting", "FileType" };
            if (!allowedProperties.Contains(updateDto.PropertyName))
            {
                throw new ArgumentException($"Invalid property name: {updateDto.PropertyName}");
            }
            
            // First, handle JsonElement conversion
            object? rawValue = updateDto.Value;
            if (updateDto.Value is System.Text.Json.JsonElement jsonElement)
            {
                // Convert JsonElement to appropriate type
                if (updateDto.PropertyName == "ExcludeFromReporting")
                {
                    if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.True)
                    {
                        rawValue = true;
                    }
                    else if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.False)
                    {
                        rawValue = false;
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid boolean JsonElement for ExcludeFromReporting: {jsonElement}");
                    }
                }
                else if (updateDto.PropertyName == "FileType")
                {
                    rawValue = jsonElement.GetString();
                }
            }
            
            object? valueToUpdate = rawValue;
            
            // Handle different property types
            if (updateDto.PropertyName == "FileType")
            {
                var allowedFileTypes = new List<string> { "code", "data", "config", "docs", "other" };
                var fileTypeValue = rawValue?.ToString()?.ToLower();
                if (string.IsNullOrEmpty(fileTypeValue) || !allowedFileTypes.Contains(fileTypeValue))
                {
                    throw new ArgumentException($"Invalid file type: {fileTypeValue}. Allowed values: {string.Join(", ", allowedFileTypes)}");
                }
                valueToUpdate = fileTypeValue;
            }
            else if (updateDto.PropertyName == "ExcludeFromReporting")
            {
                // Ensure boolean conversion for ExcludeFromReporting
                if (rawValue != null)
                {
                    bool booleanValue;
                    
                    if (rawValue is bool directBool)
                    {
                        booleanValue = directBool;
                    }
                    else if (bool.TryParse(rawValue.ToString(), out var parsedBool))
                    {
                        booleanValue = parsedBool;
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid boolean value for ExcludeFromReporting: {rawValue}");
                    }
                    
                    valueToUpdate = booleanValue ? 1 : 0;  // Convert to SQL bit value
                }
            }

            var sql = $"UPDATE CommitFiles SET {updateDto.PropertyName} = @Value WHERE Id = @FileId;";
            await connection.ExecuteAsync(sql, new { FileId = updateDto.FileId, Value = valueToUpdate });
        }

        private string GetCommitterRankingQuery(bool includePR, bool includeData, bool includeConfig)
        {
            // Build the ranking criteria based on filters using subqueries
            var codeAdded = GetAggregatedLineCountSubquery("code", "LinesAdded");
            var dataAdded = GetAggregatedLineCountSubquery("data", "LinesAdded");
            var configAdded = GetAggregatedLineCountSubquery("config", "LinesAdded");
            
            var rankingCriteria = $"SUM(c.LinesAdded) + ({codeAdded})"; // Default: Total + Code
            
            if (includeData && includeConfig)
            {
                rankingCriteria = $"SUM(c.LinesAdded) + ({codeAdded}) + ({dataAdded}) + ({configAdded})";
            }
            else if (includeData)
            {
                rankingCriteria = $"SUM(c.LinesAdded) + ({codeAdded}) + ({dataAdded})";
            }
            else if (includeConfig)
            {
                rankingCriteria = $"SUM(c.LinesAdded) + ({codeAdded}) + ({configAdded})";
            }

            // Create a custom filter clause without the userId parameter for ranking
            var prFilter = includePR ? "" : "c.IsPRMergeCommit = 0 AND ";
            var filterClause = $@"
                WHERE 
                    {prFilter}(@RepoSlug IS NULL OR r.Slug = @RepoSlug)
                    AND (@Workspace IS NULL OR r.Workspace = @Workspace)
                    AND (@StartDate IS NULL OR c.Date >= @StartDate)
                    AND (@EndDate IS NULL OR c.Date <= @EndDate)
                    AND c.IsRevert = 0
            ";

            return $@"
                SELECT 
                    u.Id AS UserId,
                    u.DisplayName,
                    u.AvatarUrl,
                    COUNT(c.Id) AS TotalCommits,
                    SUM(c.LinesAdded) AS TotalLinesAdded,
                    SUM(c.LinesRemoved) AS TotalLinesRemoved,
                    {GetAggregatedLineCountSubquery("code", "LinesAdded")} AS CodeLinesAdded,
                    {GetAggregatedLineCountSubquery("code", "LinesRemoved")} AS CodeLinesRemoved,
                    {GetAggregatedLineCountSubquery("data", "LinesAdded")} AS DataLinesAdded,
                    {GetAggregatedLineCountSubquery("data", "LinesRemoved")} AS DataLinesRemoved,
                    {GetAggregatedLineCountSubquery("config", "LinesAdded")} AS ConfigLinesAdded,
                    {GetAggregatedLineCountSubquery("config", "LinesRemoved")} AS ConfigLinesRemoved,
                    {GetAggregatedLineCountSubquery("docs", "LinesAdded")} AS DocsLinesAdded,
                    {GetAggregatedLineCountSubquery("docs", "LinesRemoved")} AS DocsLinesRemoved,
                    {rankingCriteria} AS RankingScore
                FROM Commits c
                JOIN Repositories r ON c.RepositoryId = r.Id
                JOIN Users u ON c.AuthorId = u.Id
                {filterClause}
                GROUP BY u.Id, u.DisplayName, u.AvatarUrl
                HAVING COUNT(c.Id) > 0
                ORDER BY RankingScore DESC;
            ";
        }

        private class CommitterRankingDto
        {
            public int UserId { get; set; }
            public string DisplayName { get; set; } = string.Empty;
            public string AvatarUrl { get; set; } = string.Empty;
            public int TotalCommits { get; set; }
            public int TotalLinesAdded { get; set; }
            public int TotalLinesRemoved { get; set; }
            public int CodeLinesAdded { get; set; }
            public int CodeLinesRemoved { get; set; }
            public int DataLinesAdded { get; set; }
            public int DataLinesRemoved { get; set; }
            public int ConfigLinesAdded { get; set; }
            public int ConfigLinesRemoved { get; set; }
            public int DocsLinesAdded { get; set; }
            public int DocsLinesRemoved { get; set; }
            public int RankingScore { get; set; }
        }

        public async Task UpdateUserExcludeFromReportingAsync(int userId, bool excludeFromReporting)
        {
            using var connection = new SqlConnection(_connectionString);
            const string sql = @"
                UPDATE Users 
                SET ExcludeFromReporting = @excludeFromReporting 
                WHERE Id = @userId";
            
            await connection.ExecuteAsync(sql, new { userId, excludeFromReporting });
        }
    }
} 