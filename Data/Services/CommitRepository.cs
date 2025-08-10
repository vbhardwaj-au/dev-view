using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Data.Models;

namespace Data.Services
{
    public class CommitRepository : DatabaseService, IRepository<Commit>
    {
        public CommitRepository(IConfiguration configuration, ILogger<CommitRepository> logger) 
            : base(configuration, logger)
        {
        }

        public CommitRepository(string connectionString, ILogger<CommitRepository> logger) 
            : base(connectionString, logger)
        {
        }

        public async Task<Commit?> GetByIdAsync(int id)
        {
            const string sql = "SELECT * FROM Commits WHERE Id = @Id";
            return await QuerySingleOrDefaultAsync<Commit>(sql, new { Id = id });
        }

        public async Task<IEnumerable<Commit>> GetAllAsync()
        {
            const string sql = "SELECT * FROM Commits ORDER BY Date DESC";
            return await QueryAsync<Commit>(sql);
        }

        public async Task<int> InsertAsync(Commit entity)
        {
            const string sql = @"
                INSERT INTO Commits (BitbucketCommitHash, RepositoryId, AuthorId, Date, Message, 
                                   LinesAdded, LinesRemoved, IsMerge, IsRevert, CodeLinesAdded, CodeLinesRemoved,
                                   DataLinesAdded, DataLinesRemoved, ConfigLinesAdded, ConfigLinesRemoved,
                                   DocsLinesAdded, DocsLinesRemoved, IsPRMergeCommit)
                VALUES (@BitbucketCommitHash, @RepositoryId, @AuthorId, @Date, @Message,
                       @LinesAdded, @LinesRemoved, @IsMerge, @IsRevert, @CodeLinesAdded, @CodeLinesRemoved,
                       @DataLinesAdded, @DataLinesRemoved, @ConfigLinesAdded, @ConfigLinesRemoved,
                       @DocsLinesAdded, @DocsLinesRemoved, @IsPRMergeCommit);
                SELECT SCOPE_IDENTITY();";
            
            return await ExecuteScalarAsync<int>(sql, entity);
        }

        public async Task<bool> UpdateAsync(Commit entity)
        {
            const string sql = @"
                UPDATE Commits 
                SET BitbucketCommitHash = @BitbucketCommitHash, RepositoryId = @RepositoryId, 
                    AuthorId = @AuthorId, Date = @Date, Message = @Message,
                    LinesAdded = @LinesAdded, LinesRemoved = @LinesRemoved, 
                    IsMerge = @IsMerge, IsRevert = @IsRevert,
                    CodeLinesAdded = @CodeLinesAdded, CodeLinesRemoved = @CodeLinesRemoved,
                    DataLinesAdded = @DataLinesAdded, DataLinesRemoved = @DataLinesRemoved,
                    ConfigLinesAdded = @ConfigLinesAdded, ConfigLinesRemoved = @ConfigLinesRemoved,
                    DocsLinesAdded = @DocsLinesAdded, DocsLinesRemoved = @DocsLinesRemoved,
                    IsPRMergeCommit = @IsPRMergeCommit
                WHERE Id = @Id";
            
            var rowsAffected = await ExecuteAsync(sql, entity);
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            const string sql = "DELETE FROM Commits WHERE Id = @Id";
            var rowsAffected = await ExecuteAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            const string sql = "SELECT COUNT(1) FROM Commits WHERE Id = @Id";
            var count = await ExecuteScalarAsync<int>(sql, new { Id = id });
            return count > 0;
        }

        public async Task<Commit?> GetByHashAsync(string hash, int repositoryId)
        {
            const string sql = "SELECT * FROM Commits WHERE BitbucketCommitHash = @Hash AND RepositoryId = @RepositoryId";
            return await QuerySingleOrDefaultAsync<Commit>(sql, new { Hash = hash, RepositoryId = repositoryId });
        }

        public async Task<IEnumerable<Commit>> GetByRepositoryAsync(int repositoryId, DateTime? startDate = null, DateTime? endDate = null)
        {
            const string sql = @"
                SELECT * FROM Commits 
                WHERE RepositoryId = @RepositoryId
                  AND (@StartDate IS NULL OR Date >= @StartDate)
                  AND (@EndDate IS NULL OR Date <= @EndDate)
                ORDER BY Date DESC";
            
            return await QueryAsync<Commit>(sql, new { RepositoryId = repositoryId, StartDate = startDate, EndDate = endDate });
        }
    }
}