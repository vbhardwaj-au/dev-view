using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Integration.Common;
using Integration.Utils;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Integration.Commits
{
    public static class CommitCrudHelper
    {
        // Insert or update a commit and its files, returning the commit ID
        public static async Task<int> UpsertCommitAndFilesAsync(
            SqlConnection connection,
            CommitDto commit,
            int repoId,
            string workspace,
            string repoSlug,
            BitbucketApiClient apiClient,
            DiffParserService diffParser,
            ILogger logger)
        {
            // Fetch raw diff and parse it with file classification
            var diffContent = await apiClient.GetCommitDiffAsync(workspace, repoSlug, commit.Hash);
            var diffSummary = diffParser.ParseDiffWithClassification(diffContent);
            var (totalAdded, totalRemoved, codeAdded, codeRemoved) =
                (diffSummary.TotalAdded, diffSummary.TotalRemoved, diffSummary.CodeAdded, diffSummary.CodeRemoved);

            // Check if the commit already exists
            var existingCommit = await connection.QuerySingleOrDefaultAsync<(int Id, int? CodeLinesAdded, bool? IsMerge, bool? IsPRMergeCommit)>(
                "SELECT Id, CodeLinesAdded, IsMerge, IsPRMergeCommit FROM Commits WHERE BitbucketCommitHash = @Hash", new { commit.Hash });

            // Determine if this is a merge commit
            bool isMergeCommit = commit.Parents != null && commit.Parents.Count >= 2;
            bool isPRMergeCommit = isMergeCommit;

            int commitId;
            if (existingCommit.Id > 0 && existingCommit.CodeLinesAdded.HasValue)
            {
                // Commit exists and is complete, skip further processing
                return existingCommit.Id;
            }
            else if (existingCommit.Id > 0)
            {
                // UPDATE the existing, incomplete commit
                const string updateSql = @"
                    UPDATE Commits 
                    SET LinesAdded = @LinesAdded, LinesRemoved = @LinesRemoved, 
                        CodeLinesAdded = @CodeLinesAdded, CodeLinesRemoved = @CodeLinesRemoved,
                        DataLinesAdded = @DataLinesAdded, DataLinesRemoved = @DataLinesRemoved,
                        ConfigLinesAdded = @ConfigLinesAdded, ConfigLinesRemoved = @ConfigLinesRemoved,
                        DocsLinesAdded = @DocsLinesAdded, DocsLinesRemoved = @DocsLinesRemoved,
                        IsMerge = @IsMerge, IsPRMergeCommit = @IsPRMergeCommit
                    WHERE Id = @Id;
                ";
                await connection.ExecuteAsync(updateSql, new
                {
                    Id = existingCommit.Id,
                    LinesAdded = totalAdded,
                    LinesRemoved = totalRemoved,
                    CodeLinesAdded = codeAdded,
                    CodeLinesRemoved = codeRemoved,
                    DataLinesAdded = diffSummary.DataAdded,
                    DataLinesRemoved = diffSummary.DataRemoved,
                    ConfigLinesAdded = diffSummary.ConfigAdded,
                    ConfigLinesRemoved = diffSummary.ConfigRemoved,
                    DocsLinesAdded = diffSummary.DocsAdded,
                    DocsLinesRemoved = diffSummary.DocsRemoved,
                    IsMerge = isMergeCommit,
                    IsPRMergeCommit = isPRMergeCommit
                });
                commitId = existingCommit.Id;
                logger?.LogInformation("Updated commit: {CommitHash} (IsMerge: {IsMerge}, IsPRMergeCommit: {IsPRMergeCommit})", commit.Hash, isMergeCommit, isPRMergeCommit);
            }
            else
            {
                // INSERT the new commit
                // Find or insert the author's internal ID
                int? authorId = null;
                string displayName = null, email = null, bitbucketUserId = null;
                if (commit.Author?.User?.Uuid != null)
                {
                    bitbucketUserId = commit.Author.User.Uuid;
                    authorId = await connection.QuerySingleOrDefaultAsync<int?>(
                        "SELECT Id FROM Users WHERE BitbucketUserId = @Uuid", new { Uuid = bitbucketUserId });
                }
                if (authorId == null)
                {
                    // Try to parse from raw
                    var raw = commit.Author?.Raw;
                    if (!string.IsNullOrEmpty(raw))
                    {
                        var match = Regex.Match(raw, @"^(.*?)\s*<(.+?)>$");
                        if (match.Success)
                        {
                            displayName = match.Groups[1].Value;
                            email = match.Groups[2].Value;
                        }
                        else
                        {
                            displayName = raw;
                        }
                    }
                    if (string.IsNullOrEmpty(bitbucketUserId) && !string.IsNullOrEmpty(email))
                    {
                        bitbucketUserId = $"synthetic:{email}";
                    }
                    if (string.IsNullOrEmpty(bitbucketUserId))
                    {
                        // Fallback: use commit hash as synthetic user ID
                        bitbucketUserId = $"synthetic:unknown:{commit.Hash}";
                    }
                    if (string.IsNullOrEmpty(displayName))
                    {
                        displayName = "Unknown";
                    }
                    if (!string.IsNullOrEmpty(bitbucketUserId))
                    {
                        // Insert user if not exists
                        const string insertUserSql = @"
                            IF NOT EXISTS (SELECT 1 FROM Users WHERE BitbucketUserId = @BitbucketUserId)
                            BEGIN
                                INSERT INTO Users (BitbucketUserId, DisplayName, AvatarUrl, CreatedOn)
                                VALUES (@BitbucketUserId, @DisplayName, NULL, @CreatedOn);
                            END
                            SELECT Id FROM Users WHERE BitbucketUserId = @BitbucketUserId;
                        ";
                        authorId = await connection.QuerySingleOrDefaultAsync<int?>(insertUserSql, new
                        {
                            BitbucketUserId = bitbucketUserId,
                            DisplayName = displayName,
                            CreatedOn = commit.Date
                        });
                    }
                }
                if (authorId == null)
                {
                    logger?.LogWarning(
                        "Author for commit '{CommitHash}' not found and could not be created. Raw: '{Raw}', User UUID: '{Uuid}', DisplayName: '{DisplayName}', Email: '{Email}', BitbucketUserId: '{BitbucketUserId}'. Skipping commit insert.",
                        commit.Hash, commit.Author?.Raw, commit.Author?.User?.Uuid, displayName, email, bitbucketUserId);
                    return -1;
                }
                // Use the pre-calculated merge flag and file classification data
                const string insertSql = @"
                    INSERT INTO Commits (BitbucketCommitHash, RepositoryId, AuthorId, Date, Message, 
                                       LinesAdded, LinesRemoved, IsMerge, 
                                       CodeLinesAdded, CodeLinesRemoved,
                                       DataLinesAdded, DataLinesRemoved,
                                       ConfigLinesAdded, ConfigLinesRemoved,
                                       DocsLinesAdded, DocsLinesRemoved,
                                       IsPRMergeCommit)
                    OUTPUT INSERTED.Id
                    VALUES (@Hash, @RepoId, @AuthorId, @Date, @Message, 
                            @LinesAdded, @LinesRemoved, @IsMerge, 
                            @CodeLinesAdded, @CodeLinesRemoved,
                            @DataLinesAdded, @DataLinesRemoved,
                            @ConfigLinesAdded, @ConfigLinesRemoved,
                            @DocsLinesAdded, @DocsLinesRemoved,
                            @IsPRMergeCommit);
                ";
                commitId = await connection.QuerySingleAsync<int>(insertSql, new
                {
                    commit.Hash,
                    RepoId = repoId,
                    AuthorId = authorId.Value,
                    commit.Date,
                    commit.Message,
                    LinesAdded = totalAdded,
                    LinesRemoved = totalRemoved,
                    IsMerge = isMergeCommit,
                    CodeLinesAdded = codeAdded,
                    CodeLinesRemoved = codeRemoved,
                    DataLinesAdded = diffSummary.DataAdded,
                    DataLinesRemoved = diffSummary.DataRemoved,
                    ConfigLinesAdded = diffSummary.ConfigAdded,
                    ConfigLinesRemoved = diffSummary.ConfigRemoved,
                    DocsLinesAdded = diffSummary.DocsAdded,
                    DocsLinesRemoved = diffSummary.DocsRemoved,
                    IsPRMergeCommit = isPRMergeCommit
                });
                logger?.LogInformation("Added commit: {CommitHash} (IsMerge: {IsMerge}, IsPRMergeCommit: {IsPRMergeCommit})", commit.Hash, isMergeCommit, isPRMergeCommit);
            }

            // Example: Detect revert commit
            if (commit.Message != null && commit.Message.IndexOf("Revert \"", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                commit.IsRevert = true;
            }

            // Insert file-level details
            await InsertCommitFilesAsync(connection, commitId, diffSummary.FileChanges, logger);
            return commitId;
        }

        public static async Task InsertCommitFilesAsync(SqlConnection connection, int commitId, List<FileChangeDetail> fileChanges, ILogger logger = null)
        {
            if (fileChanges == null || !fileChanges.Any()) return;

            const string insertFilesSql = @"
                INSERT INTO CommitFiles (CommitId, FilePath, FileType, ChangeStatus, LinesAdded, LinesRemoved, FileExtension, CreatedOn, ExcludeFromReporting)
                VALUES (@CommitId, @FilePath, @FileType, @ChangeStatus, @LinesAdded, @LinesRemoved, @FileExtension, @CreatedOn, @ExcludeFromReporting);
            ";

            var fileRecords = fileChanges.Select(fc => new
            {
                CommitId = commitId,
                FilePath = fc.FilePath,
                FileType = fc.FileType.ToString().ToLower(),
                ChangeStatus = fc.ChangeStatus,
                LinesAdded = fc.LinesAdded,
                LinesRemoved = fc.LinesRemoved,
                FileExtension = fc.FileExtension,
                CreatedOn = DateTime.UtcNow,
                ExcludeFromReporting = false // Default to false for new files
            }).ToArray();

            await connection.ExecuteAsync(insertFilesSql, fileRecords);
            logger?.LogDebug("Inserted {FileCount} file change records for commit {CommitId}", fileChanges.Count, commitId);
        }
    }
} 