using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Data.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Data.Repositories
{
    public class AuthRoleRepository
    {
        private readonly string _connectionString;

        public AuthRoleRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("DefaultConnection not configured");
        }

        public async Task<IEnumerable<AuthRole>> GetAllRolesAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            var query = @"
                SELECT Id, Name, Description, CreatedOn
                FROM AuthRoles
                ORDER BY Name";
            
            return await connection.QueryAsync<AuthRole>(query);
        }

        public async Task<AuthRole?> GetRoleByIdAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            var query = @"
                SELECT Id, Name, Description, CreatedOn
                FROM AuthRoles
                WHERE Id = @Id";
            
            return await connection.QueryFirstOrDefaultAsync<AuthRole>(query, new { Id = id });
        }

        public async Task<AuthRole?> GetRoleByNameAsync(string name)
        {
            using var connection = new SqlConnection(_connectionString);
            var query = @"
                SELECT Id, Name, Description, CreatedOn
                FROM AuthRoles
                WHERE Name = @Name";
            
            return await connection.QueryFirstOrDefaultAsync<AuthRole>(query, new { Name = name });
        }

        public async Task<int> CreateRoleAsync(AuthRole role)
        {
            using var connection = new SqlConnection(_connectionString);
            var query = @"
                INSERT INTO AuthRoles (Name, Description, CreatedOn)
                VALUES (@Name, @Description, GETUTCDATE());
                SELECT SCOPE_IDENTITY();";
            
            return await connection.QuerySingleAsync<int>(query, new
            {
                role.Name,
                role.Description
            });
        }

        public async Task<bool> UpdateRoleAsync(AuthRole role)
        {
            using var connection = new SqlConnection(_connectionString);
            var query = @"
                UPDATE AuthRoles
                SET Name = @Name,
                    Description = @Description
                WHERE Id = @Id";
            
            var rowsAffected = await connection.ExecuteAsync(query, new
            {
                role.Id,
                role.Name,
                role.Description
            });
            
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteRoleAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            
            // Check if role is assigned to any users
            var usersWithRole = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM AuthUserRoles WHERE RoleId = @RoleId",
                new { RoleId = id });
            
            if (usersWithRole > 0)
            {
                return false; // Cannot delete role that is assigned to users
            }
            
            var query = "DELETE FROM AuthRoles WHERE Id = @Id";
            var rowsAffected = await connection.ExecuteAsync(query, new { Id = id });
            
            return rowsAffected > 0;
        }

        public async Task<IEnumerable<string>> GetUsersByRoleAsync(int roleId)
        {
            using var connection = new SqlConnection(_connectionString);
            var query = @"
                SELECT u.Username
                FROM AuthUsers u
                INNER JOIN AuthUserRoles ur ON ur.UserId = u.Id
                WHERE ur.RoleId = @RoleId
                ORDER BY u.Username";
            
            return await connection.QueryAsync<string>(query, new { RoleId = roleId });
        }

        public async Task<int> GetUserCountByRoleAsync(int roleId)
        {
            using var connection = new SqlConnection(_connectionString);
            var query = @"
                SELECT COUNT(*)
                FROM AuthUserRoles
                WHERE RoleId = @RoleId";
            
            return await connection.QuerySingleAsync<int>(query, new { RoleId = roleId });
        }
    }
}