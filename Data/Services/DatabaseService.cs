using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Dapper;

namespace Data.Services
{
    public class DatabaseService
    {
        protected readonly string _connectionString;
        protected readonly ILogger _logger;

        public DatabaseService(IConfiguration configuration, ILogger logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("DefaultConnection string is not configured.");
            _logger = logger;
        }

        public DatabaseService(string connectionString, ILogger logger)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _logger = logger;
        }

        protected async Task<SqlConnection> GetConnectionAsync()
        {
            var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }

        protected async Task<T> ExecuteScalarAsync<T>(string sql, object? parameters = null)
        {
            try
            {
                using var connection = await GetConnectionAsync();
                return await connection.ExecuteScalarAsync<T>(sql, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing scalar query: {Query}", sql);
                throw;
            }
        }

        protected async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null)
        {
            try
            {
                using var connection = await GetConnectionAsync();
                return await connection.QueryAsync<T>(sql, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing query: {Query}", sql);
                throw;
            }
        }

        protected async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null)
        {
            try
            {
                using var connection = await GetConnectionAsync();
                return await connection.QuerySingleOrDefaultAsync<T>(sql, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing single query: {Query}", sql);
                throw;
            }
        }

        protected async Task<int> ExecuteAsync(string sql, object? parameters = null)
        {
            try
            {
                using var connection = await GetConnectionAsync();
                return await connection.ExecuteAsync(sql, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command: {Query}", sql);
                throw;
            }
        }
    }
}