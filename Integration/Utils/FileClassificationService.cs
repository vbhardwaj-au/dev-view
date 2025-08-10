using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Integration.Utils
{
    public enum FileType
    {
        Code,
        Data,
        Config,
        Docs,
        Other
    }

    public class FileClassificationConfig
    {
        public Dictionary<string, List<string>> Extensions { get; set; } = new();
        public Dictionary<string, List<string>> PathPatterns { get; set; } = new();
        public Dictionary<string, List<string>> FileNamePatterns { get; set; } = new();
        public Dictionary<string, List<string>> SpecificFiles { get; set; } = new();
        public List<string> Priority { get; set; } = new();
        public string DefaultType { get; set; } = "other";
        public bool CaseSensitive { get; set; } = false;
        public bool EnableLogging { get; set; } = true;
    }

    public class FileClassificationService
    {
        private readonly ILogger<FileClassificationService> _logger;
        private readonly FileClassificationConfig _config;
        private readonly Dictionary<string, FileType> _fileTypeMapping;

        public FileClassificationService(IConfiguration configuration, ILogger<FileClassificationService> logger)
        {
            _logger = logger;
            _config = LoadConfiguration(configuration);
            _fileTypeMapping = new Dictionary<string, FileType>
            {
                { "code", FileType.Code },
                { "data", FileType.Data },
                { "config", FileType.Config },
                { "docs", FileType.Docs },
                { "other", FileType.Other }
            };
        }

        private FileClassificationConfig LoadConfiguration(IConfiguration configuration)
        {
            try
            {
                var config = new FileClassificationConfig();
                var section = configuration.GetSection("fileClassification");
                
                if (!section.Exists())
                {
                    _logger.LogWarning("File classification configuration not found. Using default configuration.");
                    return GetDefaultConfiguration();
                }

                // Load each file type configuration
                LoadFileTypeConfig(section, "dataFiles", config);
                LoadFileTypeConfig(section, "configFiles", config);
                LoadFileTypeConfig(section, "documentationFiles", config);
                LoadFileTypeConfig(section, "codeFiles", config);

                // Load rules
                var rulesSection = section.GetSection("rules");
                if (rulesSection.Exists())
                {
                    config.Priority = rulesSection.GetSection("priority").Get<List<string>>() ?? new List<string>();
                    config.DefaultType = rulesSection["defaultType"] ?? "other";
                    config.CaseSensitive = rulesSection.GetValue<bool>("caseSensitive");
                    config.EnableLogging = rulesSection.GetValue<bool>("enableLogging");
                }

                _logger.LogInformation("File classification configuration loaded successfully.");
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load file classification configuration. Using defaults.");
                return GetDefaultConfiguration();
            }
        }

        private void LoadFileTypeConfig(IConfigurationSection section, string fileTypeName, FileClassificationConfig config)
        {
            var fileTypeSection = section.GetSection(fileTypeName);
            if (!fileTypeSection.Exists()) return;

            var fileType = MapFileTypeName(fileTypeName);
            
            // Load extensions
            var extensions = fileTypeSection.GetSection("extensions").Get<List<string>>();
            if (extensions != null)
            {
                config.Extensions[fileType] = extensions;
            }

            // Load path patterns
            var pathPatterns = fileTypeSection.GetSection("pathPatterns").Get<List<string>>();
            if (pathPatterns != null)
            {
                config.PathPatterns[fileType] = pathPatterns;
            }

            // Load file name patterns
            var fileNamePatterns = fileTypeSection.GetSection("fileNamePatterns").Get<List<string>>();
            if (fileNamePatterns != null)
            {
                config.FileNamePatterns[fileType] = fileNamePatterns;
            }

            // Load specific files
            var specificFiles = fileTypeSection.GetSection("specificFiles").Get<List<string>>();
            if (specificFiles != null)
            {
                config.SpecificFiles[fileType] = specificFiles;
            }
        }

        private string MapFileTypeName(string configName)
        {
            return configName switch
            {
                "dataFiles" => "data",
                "configFiles" => "config",
                "documentationFiles" => "docs",
                "codeFiles" => "code",
                _ => "other"
            };
        }

        private FileClassificationConfig GetDefaultConfiguration()
        {
            return new FileClassificationConfig
            {
                Extensions = new Dictionary<string, List<string>>
                {
                    { "data", new List<string> { ".csv", ".json", ".xml", ".sql", ".log" } },
                    { "config", new List<string> { ".yaml", ".yml", ".json", ".ini", ".cfg" } },
                    { "docs", new List<string> { ".md", ".txt", ".rst" } },
                    { "code", new List<string> { ".cs", ".js", ".ts", ".py", ".html", ".css" } }
                },
                Priority = new List<string> { "specificFiles", "pathPatterns", "fileNamePatterns", "extensions" },
                DefaultType = "other",
                CaseSensitive = false,
                EnableLogging = true
            };
        }

        public FileType ClassifyFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return FileType.Other;
            }

            var fileName = Path.GetFileName(filePath);
            var extension = Path.GetExtension(filePath);
            var normalizedPath = NormalizePath(filePath);

            // Apply classification rules in priority order
            foreach (var rule in _config.Priority)
            {
                var result = rule switch
                {
                    "specificFiles" => CheckSpecificFiles(fileName),
                    "pathPatterns" => CheckPathPatterns(normalizedPath),
                    "fileNamePatterns" => CheckFileNamePatterns(fileName),
                    "extensions" => CheckExtensions(extension),
                    _ => null
                };

                if (result.HasValue)
                {
                    if (_config.EnableLogging)
                    {
                        _logger.LogDebug("File '{FilePath}' classified as '{FileType}' using rule '{Rule}'", 
                            filePath, result.Value, rule);
                    }
                    return result.Value;
                }
            }

            // Default classification
            var defaultFileType = _fileTypeMapping.GetValueOrDefault(_config.DefaultType, FileType.Other);
            
            if (_config.EnableLogging)
            {
                _logger.LogDebug("File '{FilePath}' classified as default type '{FileType}'", 
                    filePath, defaultFileType);
            }

            return defaultFileType;
        }

        private FileType? CheckSpecificFiles(string fileName)
        {
            foreach (var kvp in _config.SpecificFiles)
            {
                var comparison = _config.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                if (kvp.Value.Any(file => string.Equals(fileName, file, comparison)))
                {
                    return _fileTypeMapping.GetValueOrDefault(kvp.Key, FileType.Other);
                }
            }
            return null;
        }

        private FileType? CheckPathPatterns(string normalizedPath)
        {
            foreach (var kvp in _config.PathPatterns)
            {
                var comparison = _config.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                if (kvp.Value.Any(pattern => normalizedPath.Contains(pattern, comparison)))
                {
                    return _fileTypeMapping.GetValueOrDefault(kvp.Key, FileType.Other);
                }
            }
            return null;
        }

        private FileType? CheckFileNamePatterns(string fileName)
        {
            foreach (var kvp in _config.FileNamePatterns)
            {
                if (kvp.Value.Any(pattern => MatchesPattern(fileName, pattern)))
                {
                    return _fileTypeMapping.GetValueOrDefault(kvp.Key, FileType.Other);
                }
            }
            return null;
        }

        private FileType? CheckExtensions(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return null;

            foreach (var kvp in _config.Extensions)
            {
                var comparison = _config.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                if (kvp.Value.Any(ext => string.Equals(extension, ext, comparison)))
                {
                    return _fileTypeMapping.GetValueOrDefault(kvp.Key, FileType.Other);
                }
            }
            return null;
        }

        private string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private bool MatchesPattern(string fileName, string pattern)
        {
            // Simple wildcard matching (* only)
            var comparison = _config.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            
            if (pattern.Contains('*'))
            {
                var parts = pattern.Split('*');
                if (parts.Length == 2)
                {
                    var prefix = parts[0];
                    var suffix = parts[1];
                    
                    return fileName.StartsWith(prefix, comparison) && fileName.EndsWith(suffix, comparison);
                }
            }
            
            return string.Equals(fileName, pattern, comparison);
        }

        public Dictionary<FileType, int> GetClassificationStats()
        {
            var stats = new Dictionary<FileType, int>();
            foreach (FileType fileType in Enum.GetValues<FileType>())
            {
                stats[fileType] = 0;
            }
            return stats;
        }

        public void ReloadConfiguration(IConfiguration configuration)
        {
            var newConfig = LoadConfiguration(configuration);
            // Update the config (note: this is not thread-safe, consider using IOptionsMonitor for production)
            _logger.LogInformation("File classification configuration reloaded.");
        }
    }
} 