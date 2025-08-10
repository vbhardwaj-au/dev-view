using Data.Models;
using Integration.Commits;
using Integration.PullRequests;
using Integration.Repositories;
using Integration.Users;
using API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Dapper;
using System;
using System.Threading.Tasks;

namespace API.Endpoints.Sync
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncController : ControllerBase
    {
        private readonly BitbucketUsersService _usersService;
        private readonly BitbucketRepositoriesService _reposService;
        private readonly BitbucketCommitsService _commitsService;
        private readonly BitbucketPullRequestsService _pullRequestsService;
        private readonly CommitRefreshService _commitRefreshService;
        private readonly IConfiguration _configuration;

        public SyncController(
            BitbucketUsersService usersService, 
            BitbucketRepositoriesService reposService, 
            BitbucketCommitsService commitsService,
            BitbucketPullRequestsService pullRequestsService,
            CommitRefreshService commitRefreshService,
            IConfiguration configuration)
        {
            _usersService = usersService;
            _reposService = reposService;
            _commitsService = commitsService;
            _pullRequestsService = pullRequestsService;
            _commitRefreshService = commitRefreshService;
            _configuration = configuration;
        }

        [HttpPost("users/{workspace}")]
        public async Task<IActionResult> SyncUsers(string workspace)
        {
            try
            {
                await _usersService.SyncUsersAsync(workspace);
                return Ok("User synchronization completed successfully.");
            }
            catch (Exception ex)
            {
                // In a real app, you would log the exception
                return StatusCode(500, $"An error occurred during synchronization: {ex.Message}");
            }
        }

        [HttpPost("repositories/{workspace}")]
        public async Task<IActionResult> SyncRepositories(string workspace)
        {
            try
            {
                await _reposService.SyncRepositoriesAsync(workspace);
                return Ok("Repository synchronization completed successfully.");
            }
            catch (Exception ex)
            {
                // In a real app, you would log the exception
                return StatusCode(500, $"An error occurred during synchronization: {ex.Message}");
            }
        }

        [HttpPost("commits/{workspace}/{repoSlug}")]
        public async Task<IActionResult> SyncCommits(string workspace, string repoSlug, [FromBody] DateRangeDto dateRange)
        {
            try
            {
                await _commitsService.SyncCommitsAsync(workspace, repoSlug, dateRange.StartDate, dateRange.EndDate);
                return Ok("Commit synchronization completed successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred during synchronization: {ex.Message}");
            }
        }

        [HttpPost("pullrequests/{workspace}/{repoSlug}")]
        public async Task<IActionResult> SyncPullRequests(string workspace, string repoSlug, [FromBody] DateRangeDto dateRange)
        {
            try
            {
                await _pullRequestsService.SyncPullRequestsAsync(workspace, repoSlug, dateRange.StartDate, dateRange.EndDate);
                return Ok("Pull Request synchronization completed successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred during synchronization: {ex.Message}");
            }
        }

        [HttpPost("fix-pr-merge-flags/{repoSlug?}")]
        public async Task<IActionResult> FixPRMergeFlags(string? repoSlug = null)
        {
            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();
                
                string whereClause = "";
                object parameters = new { };
                
                if (!string.IsNullOrEmpty(repoSlug))
                {
                    whereClause = @"
                        AND c.RepositoryId = (SELECT Id FROM Repositories WHERE Slug = @repoSlug)";
                    parameters = new { repoSlug };
                }
                
                var updateSql = $@"
                    UPDATE c
                    SET IsPRMergeCommit = 1
                    FROM Commits c
                    WHERE c.IsMerge = 1 
                      AND c.IsPRMergeCommit = 0
                      AND c.Id IN (
                          SELECT DISTINCT prc.CommitId 
                          FROM PullRequestCommits prc
                          INNER JOIN PullRequests pr ON prc.PullRequestId = pr.Id
                          INNER JOIN Commits c2 ON prc.CommitId = c2.Id
                          WHERE 1=1 {whereClause}
                      )";
                
                var updatedCount = await connection.ExecuteAsync(updateSql, parameters);
                
                return Ok($"Fixed IsPRMergeCommit flags for {updatedCount} commits{(string.IsNullOrEmpty(repoSlug) ? "" : $" in repository '{repoSlug}'")}.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while fixing PR merge flags: {ex.Message}");
            }
        }

        [HttpPost("refresh-commit-line-counts")]
        public async Task<IActionResult> RefreshCommitLineCounts()
        {
            try
            {
                var updatedCount = await _commitRefreshService.RefreshAllCommitLineCountsAsync();
                return Ok($"Refreshed line counts for {updatedCount} commits.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while refreshing commit line counts: {ex.Message}");
            }
        }

        [HttpPost("identify-revert-commits/{workspace}")]
        public async Task<IActionResult> IdentifyAndMarkRevertCommits(string workspace)
        {
            try
            {
                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await connection.OpenAsync();
                // Find all commits in the workspace that are not already marked as revert
                var selectSql = @"
                    SELECT c.Id, c.Message
                    FROM Commits c
                    INNER JOIN Repositories r ON c.RepositoryId = r.Id
                    WHERE r.Workspace = @workspace AND c.IsRevert = 0
                ";
                var commits = await connection.QueryAsync<(long Id, string Message)>(selectSql, new { workspace });
                int marked = 0;
                foreach (var commit in commits)
                {
                    if (!string.IsNullOrEmpty(commit.Message) && commit.Message.IndexOf("Revert \"", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        await connection.ExecuteAsync("UPDATE Commits SET IsRevert = 1 WHERE Id = @Id", new { commit.Id });
                        marked++;
                    }
                }
                return Ok($"Marked {marked} revert commits in workspace '{workspace}'.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while identifying revert commits: {ex.Message}");
            }
        }
    }

    public class DateRangeDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
} 