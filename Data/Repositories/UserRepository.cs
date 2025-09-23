using Data.Models;
using Dapper;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace Data.Repositories
{
    public class UserRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(string connectionString, ILogger<UserRepository> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public async Task<User?> GetByIdAsync(int id)
        {
            const string sql = @"
                SELECT * FROM Users
                WHERE Id = @Id";

            using var connection = new SqlConnection(_connectionString);
            var user = await connection.QuerySingleOrDefaultAsync<User>(sql, new { Id = id });
            return user;
        }

        public async Task<List<User>> FindByEmailAsync(string email)
        {
            // Note: Users table doesn't have email column, so we search in display name
            // This is a limitation of the current schema
            const string sql = @"
                SELECT TOP 5 * FROM Users
                WHERE DisplayName LIKE @EmailPattern
                AND ExcludeFromReporting = 0
                ORDER BY DisplayName";

            using var connection = new SqlConnection(_connectionString);
            var users = await connection.QueryAsync<User>(sql,
                new { EmailPattern = $"%{email}%" });
            return users.ToList();
        }

        public async Task<List<User>> FindByNameAsync(string name)
        {
            const string sql = @"
                SELECT TOP 5 * FROM Users
                WHERE DisplayName LIKE @NamePattern
                AND ExcludeFromReporting = 0
                ORDER BY DisplayName";

            using var connection = new SqlConnection(_connectionString);
            var users = await connection.QueryAsync<User>(sql,
                new { NamePattern = $"%{name}%" });
            return users.ToList();
        }

        public async Task<List<User>> GetAllActiveUsersAsync()
        {
            const string sql = @"
                SELECT * FROM Users
                WHERE ExcludeFromReporting = 0
                ORDER BY DisplayName";

            using var connection = new SqlConnection(_connectionString);
            var users = await connection.QueryAsync<User>(sql);
            return users.ToList();
        }
    }
}