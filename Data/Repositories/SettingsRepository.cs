using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Data.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Data.Repositories
{
    public class SettingsRepository
    {
        private readonly string _connectionString;

        public SettingsRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<IEnumerable<Setting>> GetAllAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                SELECT Id, Category, [Key], [Value], ValueType, Description,
                       IsActive, IsSystem, DisplayOrder, CreatedAt, UpdatedAt,
                       CreatedBy, UpdatedBy
                FROM Settings
                WHERE IsActive = 1
                ORDER BY Category, DisplayOrder, [Key]";

            return await connection.QueryAsync<Setting>(sql);
        }

        public async Task<IEnumerable<Setting>> GetByCategoryAsync(string category)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                SELECT Id, Category, [Key], [Value], ValueType, Description,
                       IsActive, IsSystem, DisplayOrder, CreatedAt, UpdatedAt,
                       CreatedBy, UpdatedBy
                FROM Settings
                WHERE Category LIKE @Category + '%'
                  AND IsActive = 1
                ORDER BY Category, DisplayOrder, [Key]";

            return await connection.QueryAsync<Setting>(sql, new { Category = category });
        }

        public async Task<Setting> GetByIdAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                SELECT Id, Category, [Key], [Value], ValueType, Description,
                       IsActive, IsSystem, DisplayOrder, CreatedAt, UpdatedAt,
                       CreatedBy, UpdatedBy
                FROM Settings
                WHERE Id = @Id";

            return await connection.QuerySingleOrDefaultAsync<Setting>(sql, new { Id = id });
        }

        public async Task<Setting> GetSettingAsync(string category, string key)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                SELECT Id, Category, [Key], [Value], ValueType, Description,
                       IsActive, IsSystem, DisplayOrder, CreatedAt, UpdatedAt,
                       CreatedBy, UpdatedBy
                FROM Settings
                WHERE Category = @Category AND [Key] = @Key AND IsActive = 1";

            return await connection.QuerySingleOrDefaultAsync<Setting>(sql, new { Category = category, Key = key });
        }

        public async Task<string> GetValueAsync(string category, string key)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = "SELECT [Value] FROM Settings WHERE Category = @Category AND [Key] = @Key AND IsActive = 1";
            return await connection.QuerySingleOrDefaultAsync<string>(sql, new { Category = category, Key = key });
        }

        public async Task<T> GetValueAsync<T>(string category, string key, T defaultValue = default)
        {
            var setting = await GetSettingAsync(category, key);
            if (setting == null)
                return defaultValue;

            try
            {
                switch (setting.ValueType)
                {
                    case SettingValueTypes.JSON:
                    case SettingValueTypes.Array:
                        return JsonSerializer.Deserialize<T>(setting.Value);

                    case SettingValueTypes.Boolean:
                        if (typeof(T) == typeof(bool))
                            return (T)(object)bool.Parse(setting.Value);
                        break;

                    case SettingValueTypes.Number:
                        if (typeof(T) == typeof(int))
                            return (T)(object)int.Parse(setting.Value);
                        if (typeof(T) == typeof(long))
                            return (T)(object)long.Parse(setting.Value);
                        if (typeof(T) == typeof(decimal))
                            return (T)(object)decimal.Parse(setting.Value);
                        if (typeof(T) == typeof(double))
                            return (T)(object)double.Parse(setting.Value);
                        break;

                    case SettingValueTypes.String:
                    default:
                        if (typeof(T) == typeof(string))
                            return (T)(object)setting.Value;
                        break;
                }

                // Try generic conversion
                return (T)Convert.ChangeType(setting.Value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        public async Task<Dictionary<string, object>> GetCategorySettingsAsync(string category)
        {
            var settings = await GetByCategoryAsync(category);
            var result = new Dictionary<string, object>();

            foreach (var setting in settings)
            {
                object value = setting.Value;

                try
                {
                    switch (setting.ValueType)
                    {
                        case SettingValueTypes.JSON:
                            value = JsonSerializer.Deserialize<Dictionary<string, object>>(setting.Value);
                            break;
                        case SettingValueTypes.Array:
                            value = JsonSerializer.Deserialize<List<string>>(setting.Value);
                            break;
                        case SettingValueTypes.Boolean:
                            value = bool.Parse(setting.Value);
                            break;
                        case SettingValueTypes.Number:
                            if (int.TryParse(setting.Value, out var intValue))
                                value = intValue;
                            else if (decimal.TryParse(setting.Value, out var decimalValue))
                                value = decimalValue;
                            break;
                    }
                }
                catch
                {
                    // Keep original string value if parsing fails
                }

                result[setting.Key] = value;
            }

            return result;
        }

        public async Task<int> CreateAsync(Setting setting)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                INSERT INTO Settings (Category, [Key], [Value], ValueType, Description,
                                    IsActive, IsSystem, DisplayOrder, CreatedAt, UpdatedAt,
                                    CreatedBy, UpdatedBy)
                VALUES (@Category, @Key, @Value, @ValueType, @Description,
                        @IsActive, @IsSystem, @DisplayOrder, @CreatedAt, @UpdatedAt,
                        @CreatedBy, @UpdatedBy);
                SELECT CAST(SCOPE_IDENTITY() as int)";

            return await connection.QuerySingleAsync<int>(sql, setting);
        }

        public async Task<bool> UpdateAsync(Setting setting)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                UPDATE Settings
                SET Category = @Category,
                    [Key] = @Key,
                    [Value] = @Value,
                    ValueType = @ValueType,
                    Description = @Description,
                    IsActive = @IsActive,
                    IsSystem = @IsSystem,
                    DisplayOrder = @DisplayOrder,
                    UpdatedAt = GETUTCDATE(),
                    UpdatedBy = @UpdatedBy
                WHERE Id = @Id";

            var affected = await connection.ExecuteAsync(sql, setting);
            return affected > 0;
        }

        public async Task<bool> UpdateValueAsync(string category, string key, string value, string updatedBy = "System")
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                UPDATE Settings
                SET [Value] = @Value,
                    UpdatedAt = GETUTCDATE(),
                    UpdatedBy = @UpdatedBy
                WHERE Category = @Category AND [Key] = @Key";

            var affected = await connection.ExecuteAsync(sql, new
            {
                Category = category,
                Key = key,
                Value = value,
                UpdatedBy = updatedBy
            });

            return affected > 0;
        }

        public async Task<bool> UpsertAsync(Setting setting)
        {
            using var connection = new SqlConnection(_connectionString);

            // Check if exists
            var exists = await connection.QuerySingleOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM Settings WHERE Category = @Category AND [Key] = @Key",
                new { setting.Category, setting.Key }) > 0;

            if (exists)
            {
                return await UpdateAsync(setting);
            }
            else
            {
                await CreateAsync(setting);
                return true;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);

            // Check if it's a system setting
            var isSystem = await connection.QuerySingleOrDefaultAsync<bool>(
                "SELECT IsSystem FROM Settings WHERE Id = @Id", new { Id = id });

            if (isSystem)
                return false; // Cannot delete system settings

            var sql = "DELETE FROM Settings WHERE Id = @Id AND IsSystem = 0";
            var affected = await connection.ExecuteAsync(sql, new { Id = id });
            return affected > 0;
        }

        public async Task<bool> SetActiveAsync(int id, bool isActive, string updatedBy = "System")
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                UPDATE Settings
                SET IsActive = @IsActive,
                    UpdatedAt = GETUTCDATE(),
                    UpdatedBy = @UpdatedBy
                WHERE Id = @Id";

            var affected = await connection.ExecuteAsync(sql, new
            {
                Id = id,
                IsActive = isActive,
                UpdatedBy = updatedBy
            });

            return affected > 0;
        }

        // Additional methods for Settings UI management
        public async Task<List<Setting>> GetAllSettingsAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                SELECT Id, Category, [Key], [Value], ValueType, Description,
                       IsActive, IsSystem, DisplayOrder, CreatedAt, UpdatedAt,
                       CreatedBy, UpdatedBy
                FROM Settings
                ORDER BY Category, DisplayOrder, [Key]";

            var result = await connection.QueryAsync<Setting>(sql);
            return result.ToList();
        }

        public async Task<int> AddSettingAsync(Setting setting)
        {
            return await CreateAsync(setting);
        }

        public async Task<bool> UpdateSettingAsync(Setting setting)
        {
            return await UpdateAsync(setting);
        }

        public async Task<bool> DeleteSettingAsync(int id)
        {
            return await DeleteAsync(id);
        }

        // Helper method to get file classification configuration
        public async Task<FileClassificationConfig> GetFileClassificationConfigAsync()
        {
            var config = new FileClassificationConfig();

            // Get all file classification settings
            var settings = await GetByCategoryAsync(SettingCategories.FileClassification);

            foreach (var setting in settings)
            {
                var categoryParts = setting.Category.Split('.');
                if (categoryParts.Length < 2) continue;

                var fileType = categoryParts[1];
                var settingKey = setting.Key;

                // Parse the value based on type
                List<string> values = null;
                if (setting.ValueType == SettingValueTypes.Array)
                {
                    try
                    {
                        values = JsonSerializer.Deserialize<List<string>>(setting.Value);
                    }
                    catch { }
                }

                // Map to config structure
                switch (fileType)
                {
                    case "DataFiles":
                        if (settingKey == "extensions" && values != null)
                            config.DataFiles.Extensions = values;
                        else if (settingKey == "pathPatterns" && values != null)
                            config.DataFiles.PathPatterns = values;
                        else if (settingKey == "fileNamePatterns" && values != null)
                            config.DataFiles.FileNamePatterns = values;
                        break;

                    case "ConfigFiles":
                        if (settingKey == "extensions" && values != null)
                            config.ConfigFiles.Extensions = values;
                        else if (settingKey == "specificFiles" && values != null)
                            config.ConfigFiles.SpecificFiles = values;
                        else if (settingKey == "pathPatterns" && values != null)
                            config.ConfigFiles.PathPatterns = values;
                        break;

                    case "DocumentationFiles":
                        if (settingKey == "extensions" && values != null)
                            config.DocumentationFiles.Extensions = values;
                        else if (settingKey == "specificFiles" && values != null)
                            config.DocumentationFiles.SpecificFiles = values;
                        else if (settingKey == "pathPatterns" && values != null)
                            config.DocumentationFiles.PathPatterns = values;
                        break;

                    case "CodeFiles":
                        if (settingKey == "extensions" && values != null)
                            config.CodeFiles.Extensions = values;
                        else if (settingKey == "pathPatterns" && values != null)
                            config.CodeFiles.PathPatterns = values;
                        break;

                    case "TestFiles":
                        if (settingKey == "extensions" && values != null)
                            config.TestFiles.Extensions = values;
                        else if (settingKey == "specificFiles" && values != null)
                            config.TestFiles.SpecificFiles = values;
                        else if (settingKey == "pathPatterns" && values != null)
                            config.TestFiles.PathPatterns = values;
                        else if (settingKey == "fileNamePatterns" && values != null)
                            config.TestFiles.FileNamePatterns = values;
                        break;

                    case "Rules":
                        if (settingKey == "priority" && values != null)
                            config.Rules.Priority = values;
                        else if (settingKey == "defaultType")
                            config.Rules.DefaultType = setting.Value.Trim('"');
                        else if (settingKey == "caseSensitive")
                            config.Rules.CaseSensitive = bool.Parse(setting.Value);
                        else if (settingKey == "enableLogging")
                            config.Rules.EnableLogging = bool.Parse(setting.Value);
                        break;
                }
            }

            return config;
        }
    }

    // Helper class for file classification configuration
    public class FileClassificationConfig
    {
        public FileClassificationCategory DataFiles { get; set; } = new();
        public FileClassificationCategory ConfigFiles { get; set; } = new();
        public FileClassificationCategory DocumentationFiles { get; set; } = new();
        public FileClassificationCategory CodeFiles { get; set; } = new();
        public FileClassificationCategory TestFiles { get; set; } = new();
        public FileClassificationRules Rules { get; set; } = new();
    }

    public class FileClassificationCategory
    {
        public List<string> Extensions { get; set; } = new();
        public List<string> SpecificFiles { get; set; } = new();
        public List<string> PathPatterns { get; set; } = new();
        public List<string> FileNamePatterns { get; set; } = new();
    }

    public class FileClassificationRules
    {
        public List<string> Priority { get; set; } = new();
        public string DefaultType { get; set; } = "other";
        public bool CaseSensitive { get; set; } = false;
        public bool EnableLogging { get; set; } = true;
    }
}