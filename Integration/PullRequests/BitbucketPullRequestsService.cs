using Integration.Common;
using Integration.Commits; // Reusing CommitDto
using Integration.Users;   // Reusing PaginatedResponseDto
using Integration.Utils;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic; // Added for List
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Integration.PullRequests;

namespace Integration.PullRequests
{
    public class BitbucketPullRequestsService
    {
        private readonly BitbucketApiClient _apiClient;
        private readonly string _connectionString;
        private readonly ILogger<BitbucketPullRequestsService> _logger;
        private readonly DiffParserService _diffParser;

        public BitbucketPullRequestsService(BitbucketApiClient apiClient, BitbucketConfig config, ILogger<BitbucketPullRequestsService> logger, DiffParserService diffParser)
        {
            _apiClient = apiClient;
            _connectionString = config.DbConnectionString;
            _logger = logger;
            _diffParser = diffParser;
        }

        public async Task<(bool HasMoreHistory, int CommitCount)> SyncPullRequestsAsync(string workspace, string repoSlug, DateTime? startDate, DateTime? endDate)
        {
            _logger.LogInformation("Starting PR sync for {Workspace}/{RepoSlug} from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}", workspace, repoSlug, startDate, endDate);
            
            bool currentBatchHitStartDateBoundary = false; // Indicates if we found PRs older than startDate
            int totalCommitsSynced = 0; // Track total commits synced through PR sync

            // Check if we're currently rate limited
            if (BitbucketApiClient.IsRateLimited())
            {
                var waitTime = BitbucketApiClient.GetRateLimitWaitTime();
                _logger.LogWarning("API is currently rate limited. PR sync will wait {WaitTime} seconds before starting.", waitTime?.TotalSeconds ?? 0);
            }
            
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var repoId = await connection.QuerySingleOrDefaultAsync<int?>(
                "SELECT Id FROM Repositories WHERE Name = @repoSlug", new { repoSlug });

            if (repoId == null)
            {
                _logger.LogWarning("Repository '{RepoSlug}' not found. Sync repositories first.", repoSlug);
                return (false, 0);
            }

            string nextPageUrl = null;
            var keepFetching = true;
            try
            {
                while(keepFetching)
                {
                    // Check for rate limiting before each API call
                    if (BitbucketApiClient.IsRateLimited())
                    {
                        var waitTime = BitbucketApiClient.GetRateLimitWaitTime();
                        _logger.LogInformation("Waiting for rate limit to reset ({WaitTime} seconds) before fetching pull requests...", waitTime?.TotalSeconds ?? 0);
                    }
                    
                    var prsJson = await _apiClient.GetPullRequestsAsync(workspace, repoSlug, startDate, endDate, nextPageUrl);
                    //_logger.LogInformation("Raw PRs JSON: {Json}", prsJson);
                    var prPagedResponse = JsonSerializer.Deserialize<PaginatedResponseDto<PullRequestDto>>(prsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (prPagedResponse?.Values == null || !prPagedResponse.Values.Any()) {
                        keepFetching = false;
                        break;
                    }

                    foreach (var pr in prPagedResponse.Values)
                    {
                        var effectiveMergedDate = pr.State == "MERGED" ? (pr.MergeCommit?.Date != DateTime.MinValue ? pr.MergeCommit?.Date : pr.UpdatedOn).SafeDateTime() : null;
                        var effectiveClosedDate = (pr.State == "DECLINED" || pr.State == "SUPERSEDED") ? pr.ClosedOn.SafeDateTime() : null;

                        if (pr.CreatedOn < startDate)
                        {
                            currentBatchHitStartDateBoundary = true;
                        }

                        // if (pr.CreatedOn > endDate) continue; // REMOVED: All PRs returned on the page will now be considered for UPSERT.

                        if (pr.Author?.Uuid == null)
                        {
                            _logger.LogWarning("PR '{PrId}' has no author or author UUID. Skipping.", pr.Id);
                            continue;
                        }

                        // Find the author's internal ID
                        var authorId = await connection.QuerySingleOrDefaultAsync<int?>(
                            "SELECT Id FROM Users WHERE BitbucketUserId = @Uuid", new { Uuid = pr.Author.Uuid });

                        if (authorId == null)
                        {
                            _logger.LogWarning("Author with UUID '{AuthorUuid}' not found for PR '{PrId}'. Sync users first.", pr.Author.Uuid, pr.Id);
                            continue;
                        }

                        // Check if this PR is a revert
                        bool isRevert = IsRevertPullRequest(pr.Title);

                        // Insert or update the pull request
                        const string prSql = @"
                            MERGE INTO PullRequests AS Target
                            USING (SELECT @BitbucketPrId AS BitbucketPrId, @RepoId AS RepositoryId) AS Source
                            ON Target.BitbucketPrId = Source.BitbucketPrId AND Target.RepositoryId = Source.RepositoryId
                            WHEN MATCHED THEN
                                UPDATE SET Title = @Title, State = @State, UpdatedOn = @UpdatedOn, MergedOn = @MergedOn, ClosedOn = @ClosedOn, IsRevert = @IsRevert
                            WHEN NOT MATCHED BY TARGET THEN
                                INSERT (BitbucketPrId, RepositoryId, AuthorId, Title, State, CreatedOn, UpdatedOn, MergedOn, ClosedOn, IsRevert)
                                VALUES (@BitbucketPrId, @RepoId, @AuthorId, @Title, @State, @CreatedOn, @UpdatedOn, @MergedOn, @ClosedOn, @IsRevert);
                            SELECT Id FROM PullRequests WHERE BitbucketPrId = @BitbucketPrId AND RepositoryId = @RepoId;
                        ";
                        var prDbId = await connection.QuerySingleAsync<int>(prSql, new
                        {
                            BitbucketPrId = pr.Id.ToString(),
                            RepoId = repoId.Value,
                            AuthorId = authorId.Value,
                            pr.Title,
                            pr.State,
                            CreatedOn = pr.CreatedOn.SafeDateTime(),
                            UpdatedOn = pr.UpdatedOn.SafeDateTime(),
                            MergedOn = pr.State == "MERGED" ? (pr.MergeCommit?.Date != DateTime.MinValue ? pr.MergeCommit?.Date : pr.UpdatedOn).SafeDateTime() : null,
                            ClosedOn = (pr.State == "DECLINED" || pr.State == "SUPERSEDED") ? pr.ClosedOn.SafeDateTime() : null,
                            IsRevert = isRevert
                        });

                        // Log PR insertion/update with revert status
                        _logger.LogInformation("PR {PrId} ({PrTitle}) in {Workspace}/{RepoSlug} was inserted/updated. DB ID: {PrDbId}, IsRevert: {IsRevert}", 
                            pr.Id, pr.Title, workspace, repoSlug, prDbId, isRevert);

                        // After inserting/updating the pull request and before syncing commits, fetch PR activity and extract approvals
                        var activityJson = await _apiClient.GetPullRequestActivityAsync(workspace, repoSlug, pr.Id);
                        var activityResponse = System.Text.Json.JsonSerializer.Deserialize<BitbucketPullRequestActivityResponse>(activityJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        var approvalParticipants = new List<BitbucketPullRequestParticipantDto>();
                        if (activityResponse?.Values != null)
                        {
                            foreach (var activity in activityResponse.Values)
                            {
                                if (activity.Approval != null && activity.Approval.User != null)
                                {
                                    approvalParticipants.Add(new BitbucketPullRequestParticipantDto
                                    {
                                        User = activity.Approval.User,
                                        Approved = true,
                                        State = "approved",
                                        Role = "REVIEWER",
                                        ParticipatedOn = activity.Approval.Date
                                    });
                                }
                            }
                        }
                        if (approvalParticipants.Any())
                        {
                            _logger.LogInformation("Found {Count} approval events in activity for PR {PrId}", approvalParticipants.Count, pr.Id);
                            await SyncPullRequestApprovalsAsync(connection, prDbId, approvalParticipants);
                        }

                        // Now, sync commits for this PR
                        var (commitsSyncedInThisBatch, _) = await SyncCommitsForPullRequest(connection, workspace, repoSlug, pr.Id, prDbId);
                        totalCommitsSynced += commitsSyncedInThisBatch;
                    }
                    // Determine if we should keep fetching more pages
                    if (currentBatchHitStartDateBoundary)
                    {
                        // If we hit the historical startDate boundary in this batch, stop fetching more pages
                        keepFetching = false;
                    }
                    else if (string.IsNullOrEmpty(nextPageUrl))
                    {
                        // No more pages from Bitbucket API, so stop fetching
                        keepFetching = false;
                    }
                } 
                
                _logger.LogInformation("PR sync finished for {Workspace}/{RepoSlug}. {CommitCount} commits synced through PR sync.", workspace, repoSlug, totalCommitsSynced);
                
                // Update repository's last sync date
                await UpdateRepositoryLastSyncDateAsync(connection, repoSlug);
                
                return (currentBatchHitStartDateBoundary, totalCommitsSynced); // Return true if we hit the boundary, meaning there's more history to fetch
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during PR sync for {Workspace}/{RepoSlug}", workspace, repoSlug);
                throw;
            }
        }

        /// <summary>
        /// Determines if a pull request is a revert based on its title
        /// </summary>
        /// <param name="title">The PR title to analyze</param>
        /// <returns>True if the PR appears to be a revert, false otherwise</returns>
        private bool IsRevertPullRequest(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return false;

            return title.IndexOf("revert", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        
        private async Task<(int CommitCount, int PullRequestCount)> SyncCommitsForPullRequest(SqlConnection connection, string workspace, string repoSlug, int bitbucketPrId, int prDbId)
        {
            int commitsSyncedInThisBatch = 0;
            try
            {
                string commitNextPageUrl = null;
                do
                {
                    // Check for rate limiting before each API call
                    if (BitbucketApiClient.IsRateLimited())
                    {
                        var waitTime = BitbucketApiClient.GetRateLimitWaitTime();
                    }
                    
                    var commitsJson = await _apiClient.GetPullRequestCommitsAsync(workspace, repoSlug, bitbucketPrId, commitNextPageUrl);
                    var commitPagedResponse = JsonSerializer.Deserialize<PaginatedResponseDto<CommitDto>>(commitsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (commitPagedResponse?.Values == null || !commitPagedResponse.Values.Any()) 
                    {
                        if (string.IsNullOrEmpty(commitNextPageUrl))
                        {
                        }
                        break;
                    }
                
                    foreach (var commit in commitPagedResponse.Values)
                    {
                        // Find the repository ID (if not already available)
                        var repoId = await connection.QuerySingleOrDefaultAsync<int?>(
                            "SELECT Id FROM Repositories WHERE Slug = @RepoSlug", new { RepoSlug = repoSlug });
                        if (repoId == null) {
                            _logger.LogWarning("Repository '{RepoSlug}' not found when inserting commit '{CommitHash}'.", repoSlug, commit.Hash);
                            continue;
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

                        _logger.LogInformation("Commit {CommitHash} for PR {PrId} in {Workspace}/{RepoSlug} was inserted/updated.", commit.Hash, bitbucketPrId, workspace, repoSlug);

                        // Always upsert the PR-commit mapping, even if the commit already exists
                        const string joinSql = @"
                            MERGE INTO PullRequestCommits AS Target
                            USING (SELECT @PrDbId AS PullRequestId, @CommitId AS CommitId) AS Source
                            ON Target.PullRequestId = Source.PullRequestId AND Target.CommitId = Source.CommitId
                            WHEN NOT MATCHED BY TARGET THEN
                                INSERT (PullRequestId, CommitId) VALUES (@PrDbId, @CommitId);
                            -- No update needed, mapping is simple
                        ";
                        await connection.ExecuteAsync(joinSql, new { PrDbId = prDbId, CommitId = commitId });
                        commitsSyncedInThisBatch++;
                    }
                    commitNextPageUrl = commitPagedResponse.NextPageUrl;
                } while (!string.IsNullOrEmpty(commitNextPageUrl));
            }
            catch (HttpRequestException ex) when (ex.Data.Contains("StatusCode") && ex.Data["StatusCode"].Equals(System.Net.HttpStatusCode.NotFound))
            {
                _logger.LogWarning("PR {PrId} in {Workspace}/{RepoSlug} has no accessible commits (404 error). This is normal for empty PRs, draft PRs, or PRs with deleted branches. Skipping commit sync for this PR.", bitbucketPrId, workspace, repoSlug);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
            {
                _logger.LogWarning("PR {PrId} in {Workspace}/{RepoSlug} has no accessible commits (404 error). This is normal for empty PRs, draft PRs, or PRs with deleted branches. Skipping commit sync for this PR.", bitbucketPrId, workspace, repoSlug);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync commits for PR {PrId} in {Workspace}/{RepoSlug}. This PR will be skipped.", bitbucketPrId, workspace, repoSlug);
            }
            return (commitsSyncedInThisBatch, 1); // Return 1 for the PR itself
        }

        private async Task SyncPullRequestApprovalsAsync(SqlConnection connection, int prDbId, System.Collections.Generic.List<BitbucketPullRequestParticipantDto> participants)
        {
            foreach (var participant in participants)
            {
                if (participant.User?.Uuid == null)
                {
                    _logger.LogWarning("Participant has no user UUID. Skipping approval sync for this participant in PR {PrDbId}.", prDbId);
                    continue;
                }

                // Find the user's internal ID
                var userId = await connection.QuerySingleOrDefaultAsync<int?>(
                    "SELECT Id FROM Users WHERE BitbucketUserId = @Uuid", new { Uuid = participant.User.Uuid });

                if (userId == null)
                {
                    _logger.LogWarning("User with UUID '{UserUuid}' not found for PR {PrDbId} approval. Skipping approval for this participant.", participant.User.Uuid, prDbId);
                    continue;
                }

                // Determine ApprovedOn timestamp based on approval state
                DateTime? approvedOn = null;
                if (participant.Approved)
                {
                    approvedOn = DateTime.UtcNow; // Or use participant.ParticipatedOn if available and more accurate
                }

                const string approvalSql = @"
                    MERGE INTO PullRequestApprovals AS Target
                    USING (SELECT @PullRequestId AS PullRequestId, @UserUuid AS UserUuid) AS Source
                    ON Target.PullRequestId = Source.PullRequestId AND Target.UserUuid = Source.UserUuid
                    WHEN MATCHED THEN
                        UPDATE SET DisplayName = @DisplayName, Role = @Role, Approved = @Approved, State = @State, ApprovedOn = @ApprovedOn
                    WHEN NOT MATCHED BY TARGET THEN
                        INSERT (PullRequestId, UserUuid, DisplayName, Role, Approved, State, ApprovedOn)
                        VALUES (@PullRequestId, @UserUuid, @DisplayName, @Role, @Approved, @State, @ApprovedOn);
                ";

                await connection.ExecuteAsync(approvalSql, new
                {
                    PullRequestId = prDbId,
                    UserUuid = participant.User.Uuid,
                    DisplayName = participant.User.DisplayName,
                    participant.Role,
                    participant.Approved,
                    participant.State,
                    ApprovedOn = approvedOn
                });
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

public class BitbucketPullRequestActivityResponse
{
    public List<BitbucketPullRequestActivityItem> Values { get; set; }
}
public class BitbucketPullRequestActivityItem
{
    public BitbucketApprovalEvent Approval { get; set; }
}
public class BitbucketApprovalEvent
{
    public UserDto User { get; set; }
    public DateTime Date { get; set; }
}
