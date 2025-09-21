using Data.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Integration.Utils
{
    public class DatabaseFileClassificationService : FileClassificationService
    {
        private readonly SettingsRepository _settingsRepository;
        private readonly ILogger<DatabaseFileClassificationService> _dbLogger;
        private bool _configLoaded = false;

        public DatabaseFileClassificationService(
            SettingsRepository settingsRepository,
            IConfiguration configuration,
            ILogger<DatabaseFileClassificationService> logger)
            : base(configuration, logger)
        {
            _settingsRepository = settingsRepository;
            _dbLogger = logger;

            // Load configuration from database on initialization
            Task.Run(async () => await LoadConfigurationFromDatabase()).Wait();
        }

        protected override FileClassificationConfig LoadConfiguration(IConfiguration configuration)
        {
            // First check if we've already loaded from database
            if (_configLoaded && _config != null)
            {
                return _config;
            }

            // Otherwise return minimal config to prevent errors during initialization
            // The actual config will be loaded from database
            return new FileClassificationConfig
            {
                Extensions = new Dictionary<string, List<string>>(),
                PathPatterns = new Dictionary<string, List<string>>(),
                FileNamePatterns = new Dictionary<string, List<string>>(),
                SpecificFiles = new Dictionary<string, List<string>>(),
                Priority = new List<string> { "specificFiles", "pathPatterns", "fileNamePatterns", "extensions" },
                DefaultType = "other",
                CaseSensitive = false,
                EnableLogging = true
            };
        }

        private async Task LoadConfigurationFromDatabase()
        {
            try
            {
                _dbLogger.LogInformation("Loading file classification configuration from database...");

                var dbConfig = await _settingsRepository.GetFileClassificationConfigAsync();

                if (dbConfig != null)
                {
                    // Convert from Data.Repositories.FileClassificationConfig to Integration.Utils.FileClassificationConfig
                    var config = ConvertToIntegrationConfig(dbConfig);

                    // Log summary of loaded configuration
                    _dbLogger.LogInformation("Successfully loaded file classification config from database:");
                    _dbLogger.LogInformation("  - Data file extensions: {Count}", config.Extensions.GetValueOrDefault("data", new List<string>()).Count);
                    _dbLogger.LogInformation("  - Config file extensions: {Count}", config.Extensions.GetValueOrDefault("config", new List<string>()).Count);
                    _dbLogger.LogInformation("  - Code file extensions: {Count}", config.Extensions.GetValueOrDefault("code", new List<string>()).Count);
                    _dbLogger.LogInformation("  - Documentation file extensions: {Count}", config.Extensions.GetValueOrDefault("docs", new List<string>()).Count);
                    _dbLogger.LogInformation("  - Test file patterns: {Count}", config.FileNamePatterns.GetValueOrDefault("test", new List<string>()).Count);
                    _dbLogger.LogInformation("  - Priority order: {Priority}", string.Join(" -> ", config.Priority));
                    _dbLogger.LogInformation("  - Default type: {DefaultType}", config.DefaultType);
                    _dbLogger.LogInformation("  - Case sensitive: {CaseSensitive}", config.CaseSensitive);
                    _dbLogger.LogInformation("  - Logging enabled: {EnableLogging}", config.EnableLogging);

                    UpdateConfiguration(config);
                    _configLoaded = true;
                }
                else
                {
                    _dbLogger.LogWarning("No file classification configuration found in database. Using default configuration.");
                    UpdateConfiguration(GetDefaultConfiguration());
                }
            }
            catch (Exception ex)
            {
                _dbLogger.LogError(ex, "Failed to load file classification configuration from database. Using default configuration.");
                UpdateConfiguration(GetDefaultConfiguration());
            }
        }

        private FileClassificationConfig ConvertToIntegrationConfig(Data.Repositories.FileClassificationConfig dbConfig)
        {
            var config = new FileClassificationConfig
            {
                Extensions = new Dictionary<string, List<string>>(),
                PathPatterns = new Dictionary<string, List<string>>(),
                FileNamePatterns = new Dictionary<string, List<string>>(),
                SpecificFiles = new Dictionary<string, List<string>>(),
                Priority = dbConfig.Rules?.Priority ?? new List<string> { "specificFiles", "pathPatterns", "fileNamePatterns", "extensions" },
                DefaultType = dbConfig.Rules?.DefaultType ?? "other",
                CaseSensitive = dbConfig.Rules?.CaseSensitive ?? false,
                EnableLogging = dbConfig.Rules?.EnableLogging ?? true
            };

            // Convert data files
            if (dbConfig.DataFiles != null)
            {
                if (dbConfig.DataFiles.Extensions?.Any() == true)
                    config.Extensions["data"] = new List<string>(dbConfig.DataFiles.Extensions);
                if (dbConfig.DataFiles.PathPatterns?.Any() == true)
                    config.PathPatterns["data"] = new List<string>(dbConfig.DataFiles.PathPatterns);
                if (dbConfig.DataFiles.FileNamePatterns?.Any() == true)
                    config.FileNamePatterns["data"] = new List<string>(dbConfig.DataFiles.FileNamePatterns);
                if (dbConfig.DataFiles.SpecificFiles?.Any() == true)
                    config.SpecificFiles["data"] = new List<string>(dbConfig.DataFiles.SpecificFiles);
            }

            // Convert config files
            if (dbConfig.ConfigFiles != null)
            {
                if (dbConfig.ConfigFiles.Extensions?.Any() == true)
                    config.Extensions["config"] = new List<string>(dbConfig.ConfigFiles.Extensions);
                if (dbConfig.ConfigFiles.PathPatterns?.Any() == true)
                    config.PathPatterns["config"] = new List<string>(dbConfig.ConfigFiles.PathPatterns);
                if (dbConfig.ConfigFiles.FileNamePatterns?.Any() == true)
                    config.FileNamePatterns["config"] = new List<string>(dbConfig.ConfigFiles.FileNamePatterns);
                if (dbConfig.ConfigFiles.SpecificFiles?.Any() == true)
                    config.SpecificFiles["config"] = new List<string>(dbConfig.ConfigFiles.SpecificFiles);
            }

            // Convert documentation files
            if (dbConfig.DocumentationFiles != null)
            {
                if (dbConfig.DocumentationFiles.Extensions?.Any() == true)
                    config.Extensions["docs"] = new List<string>(dbConfig.DocumentationFiles.Extensions);
                if (dbConfig.DocumentationFiles.PathPatterns?.Any() == true)
                    config.PathPatterns["docs"] = new List<string>(dbConfig.DocumentationFiles.PathPatterns);
                if (dbConfig.DocumentationFiles.FileNamePatterns?.Any() == true)
                    config.FileNamePatterns["docs"] = new List<string>(dbConfig.DocumentationFiles.FileNamePatterns);
                if (dbConfig.DocumentationFiles.SpecificFiles?.Any() == true)
                    config.SpecificFiles["docs"] = new List<string>(dbConfig.DocumentationFiles.SpecificFiles);
            }

            // Convert code files
            if (dbConfig.CodeFiles != null)
            {
                if (dbConfig.CodeFiles.Extensions?.Any() == true)
                    config.Extensions["code"] = new List<string>(dbConfig.CodeFiles.Extensions);
                if (dbConfig.CodeFiles.PathPatterns?.Any() == true)
                    config.PathPatterns["code"] = new List<string>(dbConfig.CodeFiles.PathPatterns);
                if (dbConfig.CodeFiles.FileNamePatterns?.Any() == true)
                    config.FileNamePatterns["code"] = new List<string>(dbConfig.CodeFiles.FileNamePatterns);
                if (dbConfig.CodeFiles.SpecificFiles?.Any() == true)
                    config.SpecificFiles["code"] = new List<string>(dbConfig.CodeFiles.SpecificFiles);
            }

            // Convert test files
            if (dbConfig.TestFiles != null)
            {
                if (dbConfig.TestFiles.Extensions?.Any() == true)
                    config.Extensions["test"] = new List<string>(dbConfig.TestFiles.Extensions);
                if (dbConfig.TestFiles.PathPatterns?.Any() == true)
                    config.PathPatterns["test"] = new List<string>(dbConfig.TestFiles.PathPatterns);
                if (dbConfig.TestFiles.FileNamePatterns?.Any() == true)
                    config.FileNamePatterns["test"] = new List<string>(dbConfig.TestFiles.FileNamePatterns);
                if (dbConfig.TestFiles.SpecificFiles?.Any() == true)
                    config.SpecificFiles["test"] = new List<string>(dbConfig.TestFiles.SpecificFiles);
            }

            return config;
        }

        public async Task ReloadConfigurationAsync()
        {
            _dbLogger.LogInformation("Reloading file classification configuration from database...");
            await LoadConfigurationFromDatabase();
        }
    }
}