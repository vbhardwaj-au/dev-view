using System;

namespace Data.Models
{
    public class Setting
    {
        public int Id { get; set; }
        public string Category { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public string ValueType { get; set; } = "String";
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsSystem { get; set; } = false;
        public int DisplayOrder { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = "System";
        public string UpdatedBy { get; set; } = "System";
    }

    public static class SettingValueTypes
    {
        public const string String = "String";
        public const string Number = "Number";
        public const string Boolean = "Boolean";
        public const string JSON = "JSON";
        public const string Array = "Array";
    }

    public static class SettingCategories
    {
        public const string FileClassification = "FileClassification";
        public const string FileClassificationDataFiles = "FileClassification.DataFiles";
        public const string FileClassificationConfigFiles = "FileClassification.ConfigFiles";
        public const string FileClassificationDocumentationFiles = "FileClassification.DocumentationFiles";
        public const string FileClassificationCodeFiles = "FileClassification.CodeFiles";
        public const string FileClassificationTestFiles = "FileClassification.TestFiles";
        public const string FileClassificationRules = "FileClassification.Rules";
        public const string AutoSync = "AutoSync";
        public const string Application = "Application";
        public const string Authentication = "Authentication";
    }
}