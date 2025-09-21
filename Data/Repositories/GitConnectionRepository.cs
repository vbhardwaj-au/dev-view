using Dapper;
using Data.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace Data.Repositories
{
    public class GitConnectionRepository
    {
        private readonly string _connectionString;

        public GitConnectionRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<IEnumerable<GitConnection>> GetAllAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            const string sql = @"
                SELECT Id, Name, GitServerType, ApiBaseUrl, ConsumerKey, ConsumerSecret,
                       AccessToken, Username, Password, PersonalAccessToken, IsActive,
                       Priority, Workspace, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy,
                       AdditionalSettings
                FROM GitConnections
                ORDER BY Priority, Name";

            return await connection.QueryAsync<GitConnection>(sql);
        }

        public async Task<GitConnection> GetByIdAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            const string sql = @"
                SELECT Id, Name, GitServerType, ApiBaseUrl, ConsumerKey, ConsumerSecret,
                       AccessToken, Username, Password, PersonalAccessToken, IsActive,
                       Priority, Workspace, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy,
                       AdditionalSettings
                FROM GitConnections
                WHERE Id = @Id";

            return await connection.QueryFirstOrDefaultAsync<GitConnection>(sql, new { Id = id });
        }

        public async Task<GitConnection> GetActiveConnectionAsync(string gitServerType)
        {
            using var connection = new SqlConnection(_connectionString);
            const string sql = @"
                SELECT TOP 1 Id, Name, GitServerType, ApiBaseUrl, ConsumerKey, ConsumerSecret,
                       AccessToken, Username, Password, PersonalAccessToken, IsActive,
                       Priority, Workspace, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy,
                       AdditionalSettings
                FROM GitConnections
                WHERE GitServerType = @GitServerType AND IsActive = 1
                ORDER BY Priority";

            return await connection.QueryFirstOrDefaultAsync<GitConnection>(sql, new { GitServerType = gitServerType });
        }

        public async Task<GitConnection> GetActiveBitbucketConnectionAsync()
        {
            return await GetActiveConnectionAsync(GitServerTypes.BitbucketCloud);
        }

        public async Task<int> CreateAsync(GitConnection connection)
        {
            using var conn = new SqlConnection(_connectionString);
            const string sql = @"
                INSERT INTO GitConnections (
                    Name, GitServerType, ApiBaseUrl, ConsumerKey, ConsumerSecret,
                    AccessToken, Username, Password, PersonalAccessToken, IsActive,
                    Priority, Workspace, CreatedBy, UpdatedBy, AdditionalSettings
                ) VALUES (
                    @Name, @GitServerType, @ApiBaseUrl, @ConsumerKey, @ConsumerSecret,
                    @AccessToken, @Username, @Password, @PersonalAccessToken, @IsActive,
                    @Priority, @Workspace, @CreatedBy, @UpdatedBy, @AdditionalSettings
                );
                SELECT CAST(SCOPE_IDENTITY() as int)";

            return await conn.QuerySingleAsync<int>(sql, connection);
        }

        public async Task<bool> UpdateAsync(GitConnection connection)
        {
            using var conn = new SqlConnection(_connectionString);
            const string sql = @"
                UPDATE GitConnections
                SET Name = @Name,
                    GitServerType = @GitServerType,
                    ApiBaseUrl = @ApiBaseUrl,
                    ConsumerKey = @ConsumerKey,
                    ConsumerSecret = @ConsumerSecret,
                    AccessToken = @AccessToken,
                    Username = @Username,
                    Password = @Password,
                    PersonalAccessToken = @PersonalAccessToken,
                    IsActive = @IsActive,
                    Priority = @Priority,
                    Workspace = @Workspace,
                    UpdatedBy = @UpdatedBy,
                    AdditionalSettings = @AdditionalSettings
                WHERE Id = @Id";

            var rowsAffected = await conn.ExecuteAsync(sql, connection);
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            const string sql = "DELETE FROM GitConnections WHERE Id = @Id";

            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
            return rowsAffected > 0;
        }

        public async Task<bool> SetActiveStatusAsync(int id, bool isActive, string updatedBy)
        {
            using var connection = new SqlConnection(_connectionString);
            const string sql = @"
                UPDATE GitConnections
                SET IsActive = @IsActive,
                    UpdatedBy = @UpdatedBy
                WHERE Id = @Id";

            var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id, IsActive = isActive, UpdatedBy = updatedBy });
            return rowsAffected > 0;
        }
    }
}