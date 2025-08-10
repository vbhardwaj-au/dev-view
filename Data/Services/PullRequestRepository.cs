using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Data.Models;

namespace Data.Services
{
    public class PullRequestRepository : DatabaseService, IRepository<PullRequest>
    {
        public PullRequestRepository(IConfiguration configuration, ILogger<PullRequestRepository> logger) 
            : base(configuration, logger)
        {
        }

        public PullRequestRepository(string connectionString, ILogger<PullRequestRepository> logger) 
            : base(connectionString, logger)
        {
        }

        public async Task<PullRequest?> GetByIdAsync(int id)
        {
            const string sql = "SELECT * FROM PullRequests WHERE Id = @Id";
            return await QuerySingleOrDefaultAsync<PullRequest>(sql, new { Id = id });
        }

        public async Task<IEnumerable<PullRequest>> GetAllAsync()
        {
            const string sql = "SELECT * FROM PullRequests ORDER BY CreatedOn DESC";
            return await QueryAsync<PullRequest>(sql);
        }

        public async Task<int> InsertAsync(PullRequest entity)
        {
            const string sql = @"
                INSERT INTO PullRequests (BitbucketPrId, RepositoryId, AuthorId, Title, State, 
                                        CreatedOn, UpdatedOn, MergedOn, ClosedOn, IsRevert)
                VALUES (@BitbucketPrId, @RepositoryId, @AuthorId, @Title, @State,
                       @CreatedOn, @UpdatedOn, @MergedOn, @ClosedOn, @IsRevert);
                SELECT SCOPE_IDENTITY();";
            
            return await ExecuteScalarAsync<int>(sql, entity);
        }

        public async Task<bool> UpdateAsync(PullRequest entity)
        {
            const string sql = @"
                UPDATE PullRequests 
                SET BitbucketPrId = @BitbucketPrId, RepositoryId = @RepositoryId, 
                    AuthorId = @AuthorId, Title = @Title, State = @State,
                    CreatedOn = @CreatedOn, UpdatedOn = @UpdatedOn, 
                    MergedOn = @MergedOn, ClosedOn = @ClosedOn, IsRevert = @IsRevert
                WHERE Id = @Id";
            
            var rowsAffected = await ExecuteAsync(sql, entity);
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            const string sql = "DELETE FROM PullRequests WHERE Id = @Id";
            var rowsAffected = await ExecuteAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }

        public async Task<bool> ExistsAsync(int id)
        {
            const string sql = "SELECT COUNT(1) FROM PullRequests WHERE Id = @Id";
            var count = await ExecuteScalarAsync<int>(sql, new { Id = id });
            return count > 0;
        }

        public async Task<PullRequest?> GetByBitbucketIdAsync(string bitbucketPrId, int repositoryId)
        {
            const string sql = "SELECT * FROM PullRequests WHERE BitbucketPrId = @BitbucketPrId AND RepositoryId = @RepositoryId";
            return await QuerySingleOrDefaultAsync<PullRequest>(sql, new { BitbucketPrId = bitbucketPrId, RepositoryId = repositoryId });
        }

        public async Task<IEnumerable<PullRequest>> GetByRepositoryAsync(int repositoryId, DateTime? startDate = null, DateTime? endDate = null)
        {
            const string sql = @"
                SELECT * FROM PullRequests 
                WHERE RepositoryId = @RepositoryId
                  AND (@StartDate IS NULL OR CreatedOn >= @StartDate)
                  AND (@EndDate IS NULL OR CreatedOn <= @EndDate)
                ORDER BY CreatedOn DESC";
            
            return await QueryAsync<PullRequest>(sql, new { RepositoryId = repositoryId, StartDate = startDate, EndDate = endDate });
        }
    }
}