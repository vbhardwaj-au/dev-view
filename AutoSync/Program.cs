/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Integration.Common;
using Integration.Repositories;
using Integration.Commits;
using Integration.PullRequests;
using Integration.Utils;
using Dapper;
using Microsoft.Extensions.Logging;
using System.Collections.Generic; // Added for Dictionary
using System.Linq; // Added for ToDictionary and Min
using Data.Models;
using Integration.Users; // Added this line

namespace tVar.AutoSync
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Load configuration from API/appsettings.json
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory) // Changed to AppContext.BaseDirectory
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var connectionString = config.GetConnectionString("DefaultConnection");
            var bitbucketSection = config.GetSection("Bitbucket");
            var bitbucketConfig = bitbucketSection.Get<BitbucketConfig>() ?? new BitbucketConfig();
            bitbucketConfig.DbConnectionString = config.GetConnectionString("DefaultConnection"); // Explicitly set DbConnectionString
            int batchDays = config.GetValue<int?>("AutoSyncBatchDays") ?? 10;
            var syncSettings = config.GetSection("SyncSettings").Get<SyncSettings>() ?? new SyncSettings(); // Load SyncSettings

            // Optional AutoSync overrides to enable/disable processing types without changing shared SyncSettings
            var autoSyncOverrides = config.GetSection("AutoSync");
            bool? processCommitsOverride = autoSyncOverrides.GetValue<bool?>("ProcessCommits");
            bool? processPullRequestsOverride = autoSyncOverrides.GetValue<bool?>("ProcessPullRequests");
            if (processCommitsOverride.HasValue)
            {
                syncSettings.SyncTargets.Commits = processCommitsOverride.Value;
            }
            if (processPullRequestsOverride.HasValue)
            {
                syncSettings.SyncTargets.PullRequests = processPullRequestsOverride.Value;
            }

            Console.WriteLine($"Starting tVar.AutoSync with batch size: {batchDays} days");
            Console.WriteLine($"Sync Mode: {syncSettings.Mode}, Overwrite: {syncSettings.Overwrite}");
            Console.WriteLine($"Targets → Commits: {syncSettings.SyncTargets.Commits}, PullRequests: {syncSettings.SyncTargets.PullRequests}, Repositories: {syncSettings.SyncTargets.Repositories}, Users: {syncSettings.SyncTargets.Users}");

            // Minimal logger for Integration services
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var commitLogger = loggerFactory.CreateLogger<BitbucketCommitsService>();
            var prLogger = loggerFactory.CreateLogger<BitbucketPullRequestsService>();
            var diffParserLogger = loggerFactory.CreateLogger<DiffParserService>();
            var fileClassificationLogger = loggerFactory.CreateLogger<FileClassificationService>(); // Added logger for FileClassificationService
            var userLogger = loggerFactory.CreateLogger<BitbucketUsersService>(); // Added logger for BitbucketUsersService

            var fileClassificationService = new FileClassificationService(config, fileClassificationLogger); // Passed config and logger
            var diffParser = new DiffParserService(fileClassificationService, diffParserLogger);

            var apiClient = new BitbucketApiClient(bitbucketConfig);
            var repoService = new BitbucketRepositoriesService(bitbucketConfig, apiClient);
            var commitService = new BitbucketCommitsService(bitbucketConfig, apiClient, diffParser, commitLogger);
            var prService = new BitbucketPullRequestsService(apiClient, bitbucketConfig, prLogger, diffParser);
            var userService = new BitbucketUsersService(bitbucketConfig, apiClient, userLogger); // Instantiated BitbucketUsersService

            // Cancellation support for graceful termination (SIGTERM/CTRL+C)
            var cts = new System.Threading.CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
            AppDomain.CurrentDomain.ProcessExit += (_, __) => cts.Cancel();

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // 1. Get all repositories from DB (moved here to be available for user/repo sync checks)
            var repos = await connection.QueryAsync<(int Id, string Slug, string Workspace)>(
                "SELECT Id, Slug, Workspace FROM Repositories WHERE ExcludeFromSync = 0");

            // Optional: Sync Users and Repositories first if enabled
            if (syncSettings.SyncTargets.Users)
            {
                Console.WriteLine("\nSyncing users...");
                var workspaces = repos.Select(r => r.Workspace).Distinct(); // Get distinct workspaces
                foreach (var workspace in workspaces)
                {
                    Console.WriteLine($"Syncing users for workspace: {workspace}");
                    cts.Token.ThrowIfCancellationRequested();
                    await userService.SyncUsersAsync(workspace);
                }
            }

            if (syncSettings.SyncTargets.Repositories)
            {
                Console.WriteLine("Syncing repositories...");
                var workspaces = repos.Select(r => r.Workspace).Distinct(); // Get distinct workspaces
                foreach (var workspace in workspaces)
                {
                    Console.WriteLine($"Syncing repositories for workspace: {workspace}");
                    cts.Token.ThrowIfCancellationRequested();
                    await repoService.SyncRepositoriesAsync(workspace);
                }
            }

            if (!repos.Any())
            {
                Console.WriteLine("No repositories found in the database. Please ensure repositories are synced first.");
                Console.WriteLine("tVar.AutoSync complete.");
                return;
            }

            // Track the current end date for each repository's sync
            // Set to end of today (23:59:59) to include all commits from today
            var repoCurrentEndDates = repos.ToDictionary(r => r.Slug, r => DateTime.UtcNow.Date.AddDays(1).AddSeconds(-1));
            var completedRepos = new HashSet<string>(); // Keep track of completed repositories for Full sync mode

            bool overallMoreHistory = true;

            if (syncSettings.Mode == "Delta")
            {
                Console.WriteLine($"Running in DELTA mode: syncing last {syncSettings.DeltaSyncDays} days.");
                overallMoreHistory = false; // Delta mode runs only once

                foreach (var repo in repos)
                {
                    if (!syncSettings.SyncTargets.Commits && !syncSettings.SyncTargets.PullRequests) continue;

                    // Set endDate to end of today (23:59:59) to include all commits from today
                    DateTime endDate = DateTime.UtcNow.Date.AddDays(1).AddSeconds(-1);
                    DateTime startDate = DateTime.UtcNow.Date.AddDays(-syncSettings.DeltaSyncDays);

                    Console.WriteLine($"\n[Repo: {repo.Slug}] Processing DELTA from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

                    // Log sync start
                    var logId = await connection.ExecuteScalarAsync<int>(@"
                        INSERT INTO RepositorySyncLog (RepositoryId, StartDate, EndDate, Status, SyncedAt)
                        VALUES (@RepositoryId, @StartDate, @EndDate, @Status, GETUTCDATE());
                        SELECT SCOPE_IDENTITY();",
                        new { RepositoryId = repo.Id, StartDate = startDate, EndDate = endDate, Status = "Started" });

                    try
                    {
                        int totalCommitsSynced = 0;
                        
                        if (syncSettings.SyncTargets.Commits)
                        {
                            Console.WriteLine($"[Repo: {repo.Slug}] Syncing commits...");
                            var (hasMoreCommits, commitCount) = await commitService.SyncCommitsAsync(repo.Workspace, repo.Slug, startDate, endDate, cts.Token);
                            totalCommitsSynced += commitCount;
                        }
                        if (syncSettings.SyncTargets.PullRequests)
                        {
                            Console.WriteLine($"[Repo: {repo.Slug}] Syncing pull requests...");
                            var (hasMorePRs, prCommitCount) = await prService.SyncPullRequestsAsync(repo.Workspace, repo.Slug, startDate, endDate, cts.Token);
                            totalCommitsSynced += prCommitCount;
                        }
                        Console.WriteLine($"[Repo: {repo.Slug}] DELTA Batch complete. {totalCommitsSynced} commits synced.");

                        // Log sync complete with commit count
                        await connection.ExecuteAsync(@"
                            UPDATE RepositorySyncLog SET Status = @Status, Message = @Message, CommitCount = @CommitCount WHERE Id = @Id",
                            new { Id = logId, Status = "Completed", Message = "", CommitCount = totalCommitsSynced });
                    }
                    catch (Exception ex)
                    {
                        // Log sync failure
                        await connection.ExecuteAsync(@"
                            UPDATE RepositorySyncLog SET Status = @Status, Message = @Message WHERE Id = @Id",
                            new { Id = logId, Status = "Failed", Message = ex.Message });
                        Console.WriteLine($"[Repo: {repo.Slug}] Error: {ex.Message}");
                    }
                }
            }
            else // Full Sync Mode
            {
                Console.WriteLine("Running in FULL mode: syncing all history in 10-day blocks.");

                while (overallMoreHistory)
                {
                    overallMoreHistory = false; // Assume no more history until proven otherwise in this iteration
                    Console.WriteLine($"\n--- Starting new overall batch. Completed Repos: {string.Join(", ", completedRepos)} ---");

                    foreach (var repo in repos)
                    {
                        Console.WriteLine($"[DEBUG] Checking repo: {repo.Slug}. Is in completedRepos: {completedRepos.Contains(repo.Slug)}");
                        if (completedRepos.Contains(repo.Slug))
                        {
                            Console.WriteLine($"[DEBUG] Skipping already completed repo: {repo.Slug}");
                            continue; // Skip already completed repositories
                        }

                        DateTime endDate = repoCurrentEndDates[repo.Slug];
                        DateTime startDate = endDate.AddDays(-batchDays);

                        // Check if this date range has already been successfully synced if Overwrite is false
                        bool alreadySynced = false;
                        if (!syncSettings.Overwrite)
                        {
                            var completedLog = await connection.QuerySingleOrDefaultAsync<int?>(
                                "SELECT Id FROM RepositorySyncLog WHERE RepositoryId = @RepositoryId AND StartDate = @StartDate AND EndDate = @EndDate AND Status = 'Completed'",
                                new { RepositoryId = repo.Id, StartDate = startDate, EndDate = endDate });
                            if (completedLog != null)
                            {
                                alreadySynced = true;
                                Console.WriteLine($"[Repo: {repo.Slug}] Skipping already synced batch from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd} (Overwrite is false).");
                            }
                        }
                        
                        if (alreadySynced) {
                            // If already synced, and not overwriting, we mark this repo's current end date as this start date
                            // to ensure it moves to the next older batch in the next overall iteration.
                            repoCurrentEndDates[repo.Slug] = startDate;
                            overallMoreHistory = true; // Still more history to check for other repos or older dates of this repo
                            continue; 
                        }

                        Console.WriteLine($"\n[Repo: {repo.Slug}] Processing from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

                        // Log sync start
                        var logId = await connection.ExecuteScalarAsync<int>(@"
                            INSERT INTO RepositorySyncLog (RepositoryId, StartDate, EndDate, Status, SyncedAt)
                            VALUES (@RepositoryId, @StartDate, @EndDate, @Status, GETUTCDATE());
                            SELECT SCOPE_IDENTITY();",
                            new { RepositoryId = repo.Id, StartDate = startDate, EndDate = endDate, Status = "Started" });

                        try
                        {
                            bool hasMoreCommits = false;
                            bool hasMorePRs = false;
                            int totalCommitsSynced = 0;

                            if (syncSettings.SyncTargets.Commits)
                            {
                                Console.WriteLine($"[Repo: {repo.Slug}] Syncing commits...");
                                cts.Token.ThrowIfCancellationRequested();
                                var (moreCommits, commitCount) = await commitService.SyncCommitsAsync(repo.Workspace, repo.Slug, startDate, endDate, cts.Token);
                                hasMoreCommits = moreCommits;
                                totalCommitsSynced += commitCount;
                            }
                            if (syncSettings.SyncTargets.PullRequests)
                            {
                                Console.WriteLine($"[Repo: {repo.Slug}] Syncing pull requests...");
                                var (morePRs, prCommitCount) = await prService.SyncPullRequestsAsync(repo.Workspace, repo.Slug, startDate, endDate, cts.Token);
                                hasMorePRs = morePRs;
                                totalCommitsSynced += prCommitCount;
                            }
                            Console.WriteLine($"[Repo: {repo.Slug}] Batch complete. {totalCommitsSynced} commits synced.");

                            // Log sync complete with commit count
                            await connection.ExecuteAsync(@"
                                UPDATE RepositorySyncLog SET Status = @Status, Message = @Message, CommitCount = @CommitCount WHERE Id = @Id",
                                new { Id = logId, Status = "Completed", Message = "", CommitCount = totalCommitsSynced });

                            // Update the end date for the next iteration for this specific repo
                            if (hasMoreCommits || hasMorePRs)
                            {
                                repoCurrentEndDates[repo.Slug] = startDate;
                            }
                            else
                            {
                                // If no more history for this repo, add it to completedRepos
                                completedRepos.Add(repo.Slug);
                                Console.WriteLine($"[Repo: {repo.Slug}] No more history found. Marking as complete.");
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log sync failure
                            await connection.ExecuteAsync(@"
                                UPDATE RepositorySyncLog SET Status = @Status, Message = @Message WHERE Id = @Id",
                                new { Id = logId, Status = "Failed", Message = ex.Message });
                            Console.WriteLine($"[Repo: {repo.Slug}] Error: {ex.Message}");
                        }
                    }
                    // Determine if there's any repository still needing more history
                    if (completedRepos.Count < repos.Count())
                    {
                        overallMoreHistory = true;
                    }
                }
            }

            Console.WriteLine("\ntVar.AutoSync complete.");
        }
    }
}

