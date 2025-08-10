using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Integration.Utils
{
    public class FileChangeSummary
    {
        public int TotalAdded { get; set; }
        public int TotalRemoved { get; set; }
        public int CodeAdded { get; set; }
        public int CodeRemoved { get; set; }
        public int DataAdded { get; set; }
        public int DataRemoved { get; set; }
        public int ConfigAdded { get; set; }
        public int ConfigRemoved { get; set; }
        public int DocsAdded { get; set; }
        public int DocsRemoved { get; set; }
        public List<FileChangeDetail> FileChanges { get; set; } = new();
    }

    public class FileChangeDetail
    {
        public string FilePath { get; set; } = string.Empty;
        public FileType FileType { get; set; }
        public string ChangeStatus { get; set; } = string.Empty; // "added", "modified", "removed"
        public int LinesAdded { get; set; }
        public int LinesRemoved { get; set; }
        public string FileExtension { get; set; } = string.Empty;
    }

    public class DiffParserService
    {
        private static readonly string[] CommentMarkers = { "//", "/*", "*", "*/", "#", "<!--", "-->" };
        private readonly FileClassificationService _fileClassifier;
        private readonly ILogger<DiffParserService> _logger;

        public DiffParserService(FileClassificationService fileClassifier, ILogger<DiffParserService> logger)
        {
            _fileClassifier = fileClassifier;
            _logger = logger;
        }

        // Legacy method for backward compatibility
        public (int totalAdded, int totalRemoved, int codeAdded, int codeRemoved) ParseDiff(string diffContent)
        {
            var summary = ParseDiffWithClassification(diffContent);
            return (summary.TotalAdded, summary.TotalRemoved, summary.CodeAdded, summary.CodeRemoved);
        }

        public FileChangeSummary ParseDiffWithClassification(string diffContent)
        {
            var summary = new FileChangeSummary();
            var lines = diffContent.Split('\n');
            
            string currentFile = string.Empty;
            var currentFileChanges = new Dictionary<string, FileChangeDetail>();
            bool inFileContent = false;

            foreach (var line in lines)
            {
                // Parse file headers
                if (line.StartsWith("diff --git"))
                {
                    inFileContent = false;
                    var match = Regex.Match(line, @"diff --git a/(.*?) b/(.*)");
                    if (match.Success)
                    {
                        currentFile = match.Groups[2].Value; // Use the "b/" version (after changes)
                    }
                }
                else if (line.StartsWith("+++"))
                {
                    var match = Regex.Match(line, @"\+\+\+ b/(.*)");
                    if (match.Success)
                    {
                        currentFile = match.Groups[1].Value;
                        
                        if (!currentFileChanges.ContainsKey(currentFile))
                        {
                            var fileType = _fileClassifier.ClassifyFile(currentFile);
                            var fileExtension = Path.GetExtension(currentFile);
                            
                            currentFileChanges[currentFile] = new FileChangeDetail
                            {
                                FilePath = currentFile,
                                FileType = fileType,
                                FileExtension = fileExtension,
                                ChangeStatus = DetermineChangeStatus(diffContent, currentFile)
                            };
                        }
                        inFileContent = true;
                    }
                }
                else if (line.StartsWith("---") || line.StartsWith("index ") || line.StartsWith("@@"))
                {
                    // Skip these lines
                    continue;
                }
                else if (inFileContent && !string.IsNullOrEmpty(currentFile))
                {
                    // Count line changes
                    if (line.StartsWith("+") && !line.StartsWith("+++"))
                    {
                        summary.TotalAdded++;
                        if (currentFileChanges.ContainsKey(currentFile))
                        {
                            currentFileChanges[currentFile].LinesAdded++;
                            
                            var fileType = currentFileChanges[currentFile].FileType;
                            var lineContent = line.Substring(1);
                            
                            // For code files, only count non-comment/non-whitespace lines
                            // For data/config/docs files, count ALL lines
                            if (fileType == FileType.Code)
                            {
                                if (!IsCommentOrWhitespace(lineContent))
                                {
                                    IncrementFileTypeCounter(summary, fileType, true);
                                }
                            }
                            else
                            {
                                // For data, config, and docs files, count ALL lines (including comments and whitespace)
                                IncrementFileTypeCounter(summary, fileType, true);
                            }
                        }
                    }
                    else if (line.StartsWith("-") && !line.StartsWith("---"))
                    {
                        summary.TotalRemoved++;
                        if (currentFileChanges.ContainsKey(currentFile))
                        {
                            currentFileChanges[currentFile].LinesRemoved++;
                            
                            var fileType = currentFileChanges[currentFile].FileType;
                            var lineContent = line.Substring(1);
                            
                            // For code files, only count non-comment/non-whitespace lines
                            // For data/config/docs files, count ALL lines
                            if (fileType == FileType.Code)
                            {
                                if (!IsCommentOrWhitespace(lineContent))
                                {
                                    IncrementFileTypeCounter(summary, fileType, false);
                                }
                            }
                            else
                            {
                                // For data, config, and docs files, count ALL lines (including comments and whitespace)
                                IncrementFileTypeCounter(summary, fileType, false);
                            }
                        }
                    }
                }
            }

            summary.FileChanges = currentFileChanges.Values.ToList();
            
            _logger.LogDebug("Parsed diff: {FileCount} files, {TotalAdded}+/{TotalRemoved}- lines, " +
                           "Code: {CodeAdded}+/{CodeRemoved}-, Data: {DataAdded}+/{DataRemoved}-, " +
                           "Config: {ConfigAdded}+/{ConfigRemoved}-, Docs: {DocsAdded}+/{DocsRemoved}-",
                           summary.FileChanges.Count, summary.TotalAdded, summary.TotalRemoved,
                           summary.CodeAdded, summary.CodeRemoved, summary.DataAdded, summary.DataRemoved,
                           summary.ConfigAdded, summary.ConfigRemoved, summary.DocsAdded, summary.DocsRemoved);

            return summary;
        }

        private void IncrementFileTypeCounter(FileChangeSummary summary, FileType fileType, bool isAddition)
        {
            switch (fileType)
            {
                case FileType.Code:
                    if (isAddition) summary.CodeAdded++; else summary.CodeRemoved++;
                    break;
                case FileType.Data:
                    if (isAddition) summary.DataAdded++; else summary.DataRemoved++;
                    break;
                case FileType.Config:
                    if (isAddition) summary.ConfigAdded++; else summary.ConfigRemoved++;
                    break;
                case FileType.Docs:
                    if (isAddition) summary.DocsAdded++; else summary.DocsRemoved++;
                    break;
                default:
                    // "Other" files are not counted in specific categories but are included in totals
                    break;
            }
        }

        private string DetermineChangeStatus(string diffContent, string filePath)
        {
            // Look for file creation/deletion indicators
            var lines = diffContent.Split('\n');
            bool foundFile = false;
            
            foreach (var line in lines)
            {
                if (line.Contains($"a/{filePath}") || line.Contains($"b/{filePath}"))
                {
                    foundFile = true;
                    continue;
                }
                
                if (foundFile)
                {
                    if (line.StartsWith("new file mode"))
                        return "added";
                    if (line.StartsWith("deleted file mode"))
                        return "removed";
                    if (line.StartsWith("@@"))
                        return "modified";
                }
            }
            
            return "modified"; // Default assumption
        }

        private bool IsCommentOrWhitespace(string line)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
            {
                return true;
            }

            return CommentMarkers.Any(marker => trimmedLine.StartsWith(marker));
        }
    }
} 