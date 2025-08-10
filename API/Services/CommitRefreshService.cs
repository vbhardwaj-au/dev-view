using Data.Models;
using Integration.Common;
using Integration.Utils;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.Services
{
    public class CommitRefreshService
    {
        private readonly string _connectionString;
        private readonly FileClassificationService _fileClassifier;
        private readonly ILogger<CommitRefreshService> _logger;

        public CommitRefreshService(BitbucketConfig config, FileClassificationService fileClassifier, ILogger<CommitRefreshService> logger)
        {
            _connectionString = config.DbConnectionString;
            _fileClassifier = fileClassifier;
            _logger = logger;
        }

        public async Task<int> RefreshAllCommitLineCountsAsync()
        {
            _logger.LogInformation("Starting refresh of commit line counts using existing CommitFiles data.");
            int updatedFilesCount = 0;
            int updatedCommitsCount = 0;

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Step 1: Get all CommitFiles records
            _logger.LogInformation("Reading all CommitFiles records for re-classification...");
            var commitFiles = await connection.QueryAsync<CommitFileRecord>(
                "SELECT Id, CommitId, FilePath, FileType, LinesAdded, LinesRemoved FROM CommitFiles");

            _logger.LogInformation("Found {Count} commit files to process.", commitFiles.Count());

            // Step 2: Re-classify each file and update if needed
            foreach (var file in commitFiles)
            {
                try
                {
                    // Re-classify the file using current configuration
                    var newFileType = _fileClassifier.ClassifyFile(file.FilePath);
                    var newFileTypeString = newFileType.ToString().ToLower();

                    // Update if classification changed
                    if (!string.Equals(file.FileType, newFileTypeString, StringComparison.OrdinalIgnoreCase))
                    {
                        await connection.ExecuteAsync(
                            "UPDATE CommitFiles SET FileType = @NewFileType WHERE Id = @Id",
                            new { NewFileType = newFileTypeString, file.Id });
                        
                        updatedFilesCount++;
                        
                        _logger.LogDebug("Updated file {FilePath}: {OldType} → {NewType}", 
                            file.FilePath, file.FileType, newFileTypeString);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error re-classifying file {FilePath}: {Message}", file.FilePath, ex.Message);
                }
            }

            _logger.LogInformation("Re-classified {UpdatedCount} out of {TotalCount} files.", updatedFilesCount, commitFiles.Count());

            // Step 3: Re-aggregate line counts for all commits
            _logger.LogInformation("Re-aggregating line counts for all commits...");
            
            var commits = await connection.QueryAsync<int>("SELECT DISTINCT CommitId FROM CommitFiles");
            
            foreach (var commitId in commits)
            {
                try
                {
                    // Calculate aggregated line counts from CommitFiles
                    var aggregatedData = await connection.QuerySingleOrDefaultAsync<dynamic>(@"
                        SELECT 
                            SUM(CASE WHEN FileType = 'code' AND ExcludeFromReporting = 0 THEN LinesAdded ELSE 0 END) AS CodeLinesAdded,
                            SUM(CASE WHEN FileType = 'code' AND ExcludeFromReporting = 0 THEN LinesRemoved ELSE 0 END) AS CodeLinesRemoved,
                            SUM(CASE WHEN FileType = 'data' AND ExcludeFromReporting = 0 THEN LinesAdded ELSE 0 END) AS DataLinesAdded,
                            SUM(CASE WHEN FileType = 'data' AND ExcludeFromReporting = 0 THEN LinesRemoved ELSE 0 END) AS DataLinesRemoved,
                            SUM(CASE WHEN FileType = 'config' AND ExcludeFromReporting = 0 THEN LinesAdded ELSE 0 END) AS ConfigLinesAdded,
                            SUM(CASE WHEN FileType = 'config' AND ExcludeFromReporting = 0 THEN LinesRemoved ELSE 0 END) AS ConfigLinesRemoved,
                            SUM(CASE WHEN FileType = 'docs' AND ExcludeFromReporting = 0 THEN LinesAdded ELSE 0 END) AS DocsLinesAdded,
                            SUM(CASE WHEN FileType = 'docs' AND ExcludeFromReporting = 0 THEN LinesRemoved ELSE 0 END) AS DocsLinesRemoved
                        FROM CommitFiles 
                        WHERE CommitId = @CommitId", new { CommitId = commitId });

                    // Update the commit with re-calculated line counts
                    var updateSql = @"
                        UPDATE Commits
                        SET 
                            CodeLinesAdded = @CodeAdded,
                            CodeLinesRemoved = @CodeRemoved,
                            DataLinesAdded = @DataAdded,
                            DataLinesRemoved = @DataRemoved,
                            ConfigLinesAdded = @ConfigAdded,
                            ConfigLinesRemoved = @ConfigRemoved,
                            DocsLinesAdded = @DocsAdded,
                            DocsLinesRemoved = @DocsRemoved
                        WHERE Id = @CommitId";

                    await connection.ExecuteAsync(updateSql, new
                    {
                        CommitId = commitId,
                        CodeAdded = aggregatedData?.CodeLinesAdded ?? 0,
                        CodeRemoved = aggregatedData?.CodeLinesRemoved ?? 0,
                        DataAdded = aggregatedData?.DataLinesAdded ?? 0,
                        DataRemoved = aggregatedData?.DataLinesRemoved ?? 0,
                        ConfigAdded = aggregatedData?.ConfigLinesAdded ?? 0,
                        ConfigRemoved = aggregatedData?.ConfigLinesRemoved ?? 0,
                        DocsAdded = aggregatedData?.DocsLinesAdded ?? 0,
                        DocsRemoved = aggregatedData?.DocsLinesRemoved ?? 0
                    });

                    updatedCommitsCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating line counts for commit {CommitId}: {Message}", commitId, ex.Message);
                }
            }

            _logger.LogInformation("Finished refresh: {FilesUpdated} files re-classified, {CommitsUpdated} commits updated.", 
                updatedFilesCount, updatedCommitsCount);

            return updatedFilesCount;
        }

        private class CommitFileRecord
        {
            public int Id { get; set; }
            public int CommitId { get; set; }
            public string FilePath { get; set; } = string.Empty;
            public string FileType { get; set; } = string.Empty;
            public int LinesAdded { get; set; }
            public int LinesRemoved { get; set; }
        }
    }
} 