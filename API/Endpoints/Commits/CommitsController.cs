using Data.Models;
using Microsoft.AspNetCore.Mvc;
using Entities.DTOs.Commits;
using Entities.DTOs.Analytics;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;

namespace API.Endpoints.Commits
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommitsController : ControllerBase
    {
        private readonly string _connectionString;
        private const int DefaultPageSize = 25;

        public CommitsController(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection connection string not found.");
        }

        [HttpGet("{repoSlug}")]
        public async Task<IActionResult> GetCommits(
            string repoSlug,
            int page = 1,
            int pageSize = DefaultPageSize,
            bool includePR = true,
            bool includeData = true,
            bool includeConfig = true,
            int? userId = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            bool showExcluded = false)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = DefaultPageSize;

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Handle "all" repositories case
            int? repoId = null;
            if (!string.Equals(repoSlug, "all", StringComparison.OrdinalIgnoreCase))
            {
                // Get specific repo ID
                repoId = await connection.QuerySingleOrDefaultAsync<int?>(
                    "SELECT Id FROM Repositories WHERE Slug = @repoSlug", new { repoSlug });
                if (repoId == null)
                    return NotFound($"Repository '{repoSlug}' not found.");
            }

            // Build WHERE clause
            var where = "WHERE 1=1";
            if (repoId.HasValue)
                where += " AND c.RepositoryId = @repoId";
            if (!includePR)
                where += " AND c.IsPRMergeCommit = 0";
            if (userId.HasValue)
                where += " AND c.AuthorId = @userId";
            if (startDate.HasValue)
                where += " AND c.Date >= @startDate";
            if (endDate.HasValue)
                where += " AND c.Date <= @endDate";
            where += " AND c.IsRevert = 0";

            // Count total commits
            var countSql = $"SELECT COUNT(*) FROM Commits c JOIN Repositories r ON c.RepositoryId = r.Id {where}";
            var totalCount = await connection.QuerySingleAsync<int>(countSql, new { repoId, userId, startDate, endDate });
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // Calculate aggregated line counts
            var aggregatedLinesSql = $@"
                SELECT 
                    SUM(TotalLinesAdded) AS TotalLinesAdded,
                    SUM(TotalLinesRemoved) AS TotalLinesRemoved,
                    SUM(CodeLinesAdded) AS CodeLinesAdded,
                    SUM(CodeLinesRemoved) AS CodeLinesRemoved,
                    SUM(DataLinesAdded) AS DataLinesAdded,
                    SUM(DataLinesRemoved) AS DataLinesRemoved,
                    SUM(ConfigLinesAdded) AS ConfigLinesAdded,
                    SUM(ConfigLinesRemoved) AS ConfigLinesRemoved
                FROM (
                    SELECT 
                        c.LinesAdded AS TotalLinesAdded,
                        c.LinesRemoved AS TotalLinesRemoved,
                        ISNULL((SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'code' AND cf.ExcludeFromReporting = 0), 0) AS CodeLinesAdded,
                        ISNULL((SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'code' AND cf.ExcludeFromReporting = 0), 0) AS CodeLinesRemoved,
                        ISNULL((SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'data' AND cf.ExcludeFromReporting = 0), 0) AS DataLinesAdded,
                        ISNULL((SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'data' AND cf.ExcludeFromReporting = 0), 0) AS DataLinesRemoved,
                        ISNULL((SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'config' AND cf.ExcludeFromReporting = 0), 0) AS ConfigLinesAdded,
                        ISNULL((SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'config' AND cf.ExcludeFromReporting = 0), 0) AS ConfigLinesRemoved
                    FROM Commits c
                    JOIN Repositories r ON c.RepositoryId = r.Id
                    {where}
                ) AS SubqueryAlias";
            
            var aggregatedData = await connection.QuerySingleOrDefaultAsync<dynamic>(aggregatedLinesSql, new { repoId, userId, startDate, endDate });

            // Query paginated commits with author info and repository info
            var sql = $@"
                SELECT c.Id, c.BitbucketCommitHash AS Hash, c.Message, u.DisplayName AS AuthorName, c.Date, c.IsMerge, c.IsPRMergeCommit, c.IsRevert,
                       c.LinesAdded, c.LinesRemoved,
                       ISNULL((SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'code' AND cf.ExcludeFromReporting = 0), 0) AS CodeLinesAdded,
                       ISNULL((SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'code' AND cf.ExcludeFromReporting = 0), 0) AS CodeLinesRemoved,
                       ISNULL((SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'data' AND cf.ExcludeFromReporting = 0), 0) AS DataLinesAdded,
                       ISNULL((SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'data' AND cf.ExcludeFromReporting = 0), 0) AS DataLinesRemoved,
                       ISNULL((SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'config' AND cf.ExcludeFromReporting = 0), 0) AS ConfigLinesAdded,
                       ISNULL((SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'config' AND cf.ExcludeFromReporting = 0), 0) AS ConfigLinesRemoved,
                       ISNULL((SELECT SUM(cf.LinesAdded) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'docs' AND cf.ExcludeFromReporting = 0), 0) AS DocsLinesAdded,
                       ISNULL((SELECT SUM(cf.LinesRemoved) FROM CommitFiles cf WHERE cf.CommitId = c.Id AND cf.FileType = 'docs' AND cf.ExcludeFromReporting = 0), 0) AS DocsLinesRemoved,
                       r.Name AS RepositoryName, r.Slug AS RepositorySlug
                FROM Commits c
                JOIN Users u ON c.AuthorId = u.Id
                JOIN Repositories r ON c.RepositoryId = r.Id
                {where}
                ORDER BY c.Date DESC
                OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY
            ";
            var commitList = (await connection.QueryAsync<CommitListItemDto>(sql, new
            {
                repoId,
                userId,
                startDate,
                endDate,
                offset = (page - 1) * pageSize,
                pageSize
            })).ToList();

            if (showExcluded && commitList.Any())
            {
                // For each commit, get excluded files and add their lines to the correct fields
                var commitIds = commitList.Select(c => c.Hash).ToList();
                var fileSql = @"SELECT c.BitbucketCommitHash AS Hash, cf.FileType, cf.LinesAdded, cf.LinesRemoved FROM CommitFiles cf JOIN Commits c ON cf.CommitId = c.Id WHERE c.BitbucketCommitHash IN @commitIds AND cf.ExcludeFromReporting = 1";
                var excludedFiles = (await connection.QueryAsync<(string Hash, string FileType, int LinesAdded, int LinesRemoved)>(fileSql, new { commitIds })).ToList();
                foreach (var commit in commitList)
                {
                    var files = excludedFiles.Where(f => f.Hash == commit.Hash);
                    foreach (var file in files)
                    {
                        switch (file.FileType)
                        {
                            case "code":
                                commit.CodeLinesAdded += file.LinesAdded;
                                commit.CodeLinesRemoved += file.LinesRemoved;
                                break;
                            case "data":
                                commit.DataLinesAdded += file.LinesAdded;
                                commit.DataLinesRemoved += file.LinesRemoved;
                                break;
                            case "config":
                                commit.ConfigLinesAdded += file.LinesAdded;
                                commit.ConfigLinesRemoved += file.LinesRemoved;
                                break;
                            case "docs":
                                commit.DocsLinesAdded += file.LinesAdded;
                                commit.DocsLinesRemoved += file.LinesRemoved;
                                break;
                        }
                    }
                    // Adjust total lines
                    commit.LinesAdded = commit.CodeLinesAdded + commit.DataLinesAdded + commit.ConfigLinesAdded + commit.DocsLinesAdded;
                    commit.LinesRemoved = commit.CodeLinesRemoved + commit.DataLinesRemoved + commit.ConfigLinesRemoved + commit.DocsLinesRemoved;
                }
            }

            var response = new PaginatedCommitsResponse
            {
                Commits = commitList,
                TotalPages = totalPages,
                TotalCommitsCount = totalCount,
                AggregatedLinesAdded = aggregatedData?.TotalLinesAdded ?? 0,
                AggregatedLinesRemoved = aggregatedData?.TotalLinesRemoved ?? 0,
                AggregatedCodeLinesAdded = aggregatedData?.CodeLinesAdded ?? 0,
                AggregatedCodeLinesRemoved = aggregatedData?.CodeLinesRemoved ?? 0,
                AggregatedDataLinesAdded = aggregatedData?.DataLinesAdded ?? 0,
                AggregatedDataLinesRemoved = aggregatedData?.DataLinesRemoved ?? 0,
                AggregatedConfigLinesAdded = aggregatedData?.ConfigLinesAdded ?? 0,
                AggregatedConfigLinesRemoved = aggregatedData?.ConfigLinesRemoved ?? 0
            };
            return Ok(response);
        }
    }
}
