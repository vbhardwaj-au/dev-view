using Integration.Common;
using Integration.Users; // Reusing PaginatedResponseDto
using Integration.Utils;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Integration.Commits; // Add this if not present

namespace Integration.Commits
{
    public class BitbucketCommitsService
    {
        private readonly BitbucketApiClient _apiClient;
        private readonly BitbucketConfig _config;
        private readonly DiffParserService _diffParser;
        private readonly string _connectionString;
        private readonly ILogger<BitbucketCommitsService> _logger;

        public BitbucketCommitsService(BitbucketConfig config, BitbucketApiClient apiClient, DiffParserService diffParser, ILogger<BitbucketCommitsService> logger)
        {
            _config = config;
            _apiClient = apiClient;
            _diffParser = diffParser;
            _connectionString = config.DbConnectionString;
            _logger = logger;
        }

        public async Task<(bool HasMoreHistory, int CommitCount)> SyncCommitsAsync(string workspace, string repoSlug, DateTime startDate, DateTime endDate)
        {
            _logger.LogInformation("Starting commit sync for {Workspace}/{RepoSlug} from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}", workspace, repoSlug, startDate, endDate);
            
            bool hitStartDateBoundary = false; // Indicates if we found commits older than startDate
            int totalCommitsSynced = 0; // Track total commits synced

            // Check if we're currently rate limited
            if (BitbucketApiClient.IsRateLimited())
            {
                var waitTime = BitbucketApiClient.GetRateLimitWaitTime();
                _logger.LogWarning("API is currently rate limited. Sync will wait {WaitTime} seconds before starting.", waitTime?.TotalSeconds ?? 0);
            }
            
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            
            // Get the internal ID for the repository
            var repoId = await connection.QuerySingleOrDefaultAsync<int?>(
                "SELECT Id FROM Repositories WHERE Name = @repoSlug", new { repoSlug });

            if (repoId == null)
            {
                _logger.LogWarning("Repository '{RepoSlug}' not found. Sync repositories first.", repoSlug);
                return (false, 0); // Indicate no more history to fetch and 0 commits synced
            }

            string nextPageUrl = null;
            var keepFetching = true;

            try
            {
                while (keepFetching)
                {
                    // Check for rate limiting before each API call
                    if (BitbucketApiClient.IsRateLimited())
                    {
                        var waitTime = BitbucketApiClient.GetRateLimitWaitTime();
                        _logger.LogInformation("Waiting for rate limit to reset ({WaitTime} seconds) before fetching commits...", waitTime?.TotalSeconds ?? 0);
                    }
                    
                    var commitsJson = await _apiClient.GetCommitsAsync(workspace, repoSlug, nextPageUrl);
                    var pagedResponse = JsonSerializer.Deserialize<PaginatedResponseDto<CommitDto>>(commitsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (pagedResponse?.Values == null || !pagedResponse.Values.Any()) {
                        keepFetching = false;
                        break;
                    }

                    foreach (var commit in pagedResponse.Values)
                    {
                        // Stop if we've gone past the start date of our desired range
                        if (commit.Date < startDate)
                        {
                            hitStartDateBoundary = true;
                            keepFetching = false; // Stop fetching more pages for this specific repo within this batch
                            break; // Exit foreach loop
                        }

                        // Skip if the commit is outside our desired date range
                        if (commit.Date > endDate) continue;

                        // Check if the commit already exists
                        var existingCommit = await connection.QuerySingleOrDefaultAsync<(int Id, int? CodeLinesAdded, bool? IsMerge, bool? IsPRMergeCommit)>(
                            "SELECT Id, CodeLinesAdded, IsMerge, IsPRMergeCommit FROM Commits WHERE BitbucketCommitHash = @Hash", new { commit.Hash });
                        
                        // Determine if this is a merge commit using the new parents logic
                        bool isMergeCommit = commit.Parents != null && commit.Parents.Count >= 2;
                        // Most merge commits are PR merge commits, so set both flags to true for merge commits
                        bool isPRMergeCommit = isMergeCommit;
                        
                        if (existingCommit.Id > 0 && existingCommit.CodeLinesAdded.HasValue)
                        {
                            // Commit exists and is complete, skip further processing
                            continue;
                        }

                        // Check for rate limiting before diff API call
                        if (BitbucketApiClient.IsRateLimited())
                        {
                            var waitTime = BitbucketApiClient.GetRateLimitWaitTime();
                            _logger.LogInformation("Waiting for rate limit to reset ({WaitTime} seconds) before fetching diff for commit {CommitHash}...", waitTime?.TotalSeconds ?? 0, commit.Hash);
                        }
                        
                        // Fetch raw diff and parse it with file classification
                        var diffContent = await _apiClient.GetCommitDiffAsync(workspace, repoSlug, commit.Hash);
                        var diffSummary = _diffParser.ParseDiffWithClassification(diffContent);
                        
                        // Extract values for backward compatibility
                        var (totalAdded, totalRemoved, codeAdded, codeRemoved) = 
                            (diffSummary.TotalAdded, diffSummary.TotalRemoved, diffSummary.CodeAdded, diffSummary.CodeRemoved);

                        // Example: Detect revert commit
                        if (commit.Message != null && commit.Message.IndexOf("Revert \"", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            commit.IsRevert = true;
                        }

                        int commitId = await CommitCrudHelper.UpsertCommitAndFilesAsync(
                            connection,
                            commit,
                            repoId.Value,
                            workspace,
                            repoSlug,
                            _apiClient,
                            _diffParser,
                            _logger
                        );
                        if (commitId < 0) continue;
                        
                        totalCommitsSynced++; // Increment counter for successfully synced commit
                    }

                    // Prepare for the next page
                    nextPageUrl = pagedResponse.NextPageUrl;
                    if (string.IsNullOrEmpty(nextPageUrl)) keepFetching = false;
                }

                _logger.LogInformation("Commit sync finished for {Workspace}/{RepoSlug}. {CommitCount} commits synced.", workspace, repoSlug, totalCommitsSynced);
                
                // Update repository's last sync date
                await UpdateRepositoryLastSyncDateAsync(connection, repoSlug);
                
                return (hitStartDateBoundary, totalCommitsSynced); // Return true if we hit the boundary, meaning there's more history to fetch
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during commit sync for {Workspace}/{RepoSlug}", workspace, repoSlug);
                throw; // Re-throw the exception to be handled by the caller
            }
        }
        
        private async Task UpdateRepositoryLastSyncDateAsync(SqlConnection connection, string repoSlug)
        {
            const string sql = @"
                UPDATE Repositories 
                SET LastDeltaSyncDate = GETUTCDATE() 
                WHERE Slug = @RepoSlug";
            await connection.ExecuteAsync(sql, new { RepoSlug = repoSlug });
        }
    }
}
