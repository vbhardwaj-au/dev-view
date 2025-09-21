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
using Data.Repositories; // Added for GitConnectionRepository
using Integration.Users; // Added this line
using System.Text.Json; // For JSON serialization

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

            // Initialize SettingsRepository to read settings from database
            var settingsRepo = new SettingsRepository(config);
            Console.WriteLine("\n=== Reading Settings from Database ===");
            Console.WriteLine($"Connection String: {connectionString.Substring(0, 50)}...");

            // Get Git connection from database instead of appsettings.json
            var gitConnectionRepo = new GitConnectionRepository(config);
            var gitConnection = await gitConnectionRepo.GetActiveBitbucketConnectionAsync();

            if (gitConnection == null)
            {
                Console.WriteLine("ERROR: No active Bitbucket connection found in database. Please configure a connection in Admin > Git Connections.");
                return;
            }

            Console.WriteLine($"Using Git connection: {gitConnection.Name} [{gitConnection.GitServerType}]");

            // Create BitbucketConfig from database connection
            var bitbucketConfig = new BitbucketConfig
            {
                DbConnectionString = connectionString,
                ApiBaseUrl = gitConnection.ApiBaseUrl,
                ConsumerKey = gitConnection.ConsumerKey,
                ConsumerSecret = gitConnection.ConsumerSecret
            };

            // Read AutoSyncBatchDays from database
            int batchDays = 10; // Default
            try
            {
                var batchDaysStr = await settingsRepo.GetValueAsync("AutoSync", "AutoSyncBatchDays");
                if (!string.IsNullOrEmpty(batchDaysStr))
                {
                    batchDays = int.Parse(batchDaysStr);
                    Console.WriteLine($"✓ AutoSyncBatchDays from DB: {batchDays}");
                }
                else
                {
                    Console.WriteLine($"⚠ AutoSyncBatchDays not found in DB, using default: {batchDays}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Error reading AutoSyncBatchDays from DB: {ex.Message}, using default: {batchDays}");
            }

            // Read SyncSettings from database
            var syncSettings = new SyncSettings(); // Default
            try
            {
                var syncSettingsJson = await settingsRepo.GetValueAsync("AutoSync", "SyncSettings");
                if (!string.IsNullOrEmpty(syncSettingsJson))
                {
                    syncSettings = JsonSerializer.Deserialize<SyncSettings>(syncSettingsJson) ?? new SyncSettings();
                    Console.WriteLine($"✓ SyncSettings from DB:");
                    Console.WriteLine($"  - Mode: {syncSettings.Mode}");
                    Console.WriteLine($"  - DeltaSyncDays: {syncSettings.DeltaSyncDays}");
                    Console.WriteLine($"  - Overwrite: {syncSettings.Overwrite}");
                    Console.WriteLine($"  - SyncTargets:");
                    Console.WriteLine($"    • Commits: {syncSettings.SyncTargets.Commits}");
                    Console.WriteLine($"    • PullRequests: {syncSettings.SyncTargets.PullRequests}");
                    Console.WriteLine($"    • Repositories: {syncSettings.SyncTargets.Repositories}");
                    Console.WriteLine($"    • Users: {syncSettings.SyncTargets.Users}");
                }
                else
                {
                    Console.WriteLine($"⚠ SyncSettings not found in DB, using defaults");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Error reading SyncSettings from DB: {ex.Message}, using defaults");
            }

            // Read additional AutoSync settings from database
            try
            {
                var pollingInterval = await settingsRepo.GetValueAsync("AutoSync", "PollingInterval");
                if (!string.IsNullOrEmpty(pollingInterval))
                    Console.WriteLine($"✓ PollingInterval from DB: {pollingInterval} seconds");

                var maxRetries = await settingsRepo.GetValueAsync("AutoSync", "MaxRetries");
                if (!string.IsNullOrEmpty(maxRetries))
                    Console.WriteLine($"✓ MaxRetries from DB: {maxRetries}");

                var retryDelay = await settingsRepo.GetValueAsync("AutoSync", "RetryDelaySeconds");
                if (!string.IsNullOrEmpty(retryDelay))
                    Console.WriteLine($"✓ RetryDelaySeconds from DB: {retryDelay} seconds");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Error reading additional settings: {ex.Message}");
            }

            // SyncTargets in SyncSettings controls enabled data types

            Console.WriteLine($"\n=== AutoSync Configuration Summary ===");
            Console.WriteLine($"Batch Size: {batchDays} days");
            Console.WriteLine($"Sync Mode: {syncSettings.Mode}, Overwrite: {syncSettings.Overwrite}");
            Console.WriteLine($"Targets → Commits: {syncSettings.SyncTargets.Commits}, PullRequests: {syncSettings.SyncTargets.PullRequests}, Repositories: {syncSettings.SyncTargets.Repositories}, Users: {syncSettings.SyncTargets.Users}");
            Console.WriteLine("======================================\n");

            // Minimal logger for Integration services
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var commitLogger = loggerFactory.CreateLogger<BitbucketCommitsService>();
            var prLogger = loggerFactory.CreateLogger<BitbucketPullRequestsService>();
            var diffParserLogger = loggerFactory.CreateLogger<DiffParserService>();
            var fileClassificationLogger = loggerFactory.CreateLogger<FileClassificationService>(); // Added logger for FileClassificationService
            var dbFileClassificationLogger = loggerFactory.CreateLogger<Integration.Utils.DatabaseFileClassificationService>(); // Added logger for DatabaseFileClassificationService
            var userLogger = loggerFactory.CreateLogger<BitbucketUsersService>(); // Added logger for BitbucketUsersService

            // Check if file classification settings exist in database
            try
            {
                var fileClassConfig = await settingsRepo.GetFileClassificationConfigAsync();
                if (fileClassConfig != null)
                {
                    Console.WriteLine($"\n✓ File Classification loaded from database");
                    Console.WriteLine($"  - Data Files Extensions: {fileClassConfig.DataFiles.Extensions.Count} items");
                    Console.WriteLine($"  - Config Files Extensions: {fileClassConfig.ConfigFiles.Extensions.Count} items");
                    Console.WriteLine($"  - Code Files Extensions: {fileClassConfig.CodeFiles.Extensions.Count} items");
                    Console.WriteLine($"  - Test Files Extensions: {fileClassConfig.TestFiles.Extensions.Count} items");
                    Console.WriteLine($"  - Documentation Files Extensions: {fileClassConfig.DocumentationFiles.Extensions.Count} items");
                    Console.WriteLine($"  - Default Type: {fileClassConfig.Rules.DefaultType}");
                    Console.WriteLine($"  - Case Sensitive: {fileClassConfig.Rules.CaseSensitive}");
                    Console.WriteLine($"  - Enable Logging: {fileClassConfig.Rules.EnableLogging}");
                }
                else
                {
                    Console.WriteLine($"\n⚠ File Classification not found in DB, using appsettings.json");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n⚠ Error loading File Classification from DB: {ex.Message}");
            }

            // For now, use appsettings.json for file classification (DB support is available via API)
            // Use DatabaseFileClassificationService to read from database
            var fileClassificationService = new Integration.Utils.DatabaseFileClassificationService(settingsRepo, config, dbFileClassificationLogger);
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

