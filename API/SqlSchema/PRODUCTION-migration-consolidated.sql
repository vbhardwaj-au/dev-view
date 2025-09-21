-- =====================================================
-- DevView Database Production Migration - Consolidated
-- Version: 3.0
-- Date: 2025-09-21
-- Description: Complete migration script for production deployment
--
-- IMPORTANT: This script consolidates all migrations and should be run
-- on production databases that need the latest schema and configuration changes.
-- =====================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- =====================================================
-- SECTION 1: CREATE SCHEMA (Tables)
-- =====================================================

-- 1.1 CREATE GIT CONNECTIONS TABLE
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[GitConnections]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[GitConnections] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Name] NVARCHAR(100) NOT NULL,
        [GitServerType] NVARCHAR(50) NOT NULL, -- 'Bitbucket', 'GitHub', 'GitLab', 'AzureDevOps'
        [ApiBaseUrl] NVARCHAR(500) NOT NULL,
        [ConsumerKey] NVARCHAR(200),
        [ConsumerSecret] NVARCHAR(500),
        [AccessToken] NVARCHAR(500),
        [Username] NVARCHAR(100),
        [Password] NVARCHAR(500),
        [PersonalAccessToken] NVARCHAR(500),
        [IsActive] BIT NOT NULL DEFAULT 0,
        [Priority] INT NOT NULL DEFAULT 0,
        [Workspace] NVARCHAR(100),
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2,
        [CreatedBy] NVARCHAR(100),
        [UpdatedBy] NVARCHAR(100),
        [AdditionalSettings] NVARCHAR(MAX)
    );

    -- Ensure only one connection can be active at a time
    CREATE UNIQUE INDEX IX_GitConnections_IsActive
    ON GitConnections(IsActive)
    WHERE IsActive = 1;

    PRINT 'Created GitConnections table';
END
ELSE
BEGIN
    -- Fix the column name if it's still 'Type' instead of 'GitServerType'
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GitConnections]') AND name = 'Type')
    BEGIN
        EXEC sp_rename 'dbo.GitConnections.Type', 'GitServerType', 'COLUMN';
        PRINT 'Renamed column Type to GitServerType in GitConnections table';
    END
END
GO

-- 1.2 CREATE SETTINGS TABLE
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Settings]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.Settings (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Category NVARCHAR(100) NOT NULL,
        [Key] NVARCHAR(200) NOT NULL,
        [Value] NVARCHAR(MAX) NOT NULL,
        ValueType NVARCHAR(50) NOT NULL DEFAULT 'String', -- String, Number, Boolean, JSON, Array
        Description NVARCHAR(500),
        IsActive BIT NOT NULL DEFAULT 1,
        IsSystem BIT NOT NULL DEFAULT 0, -- System settings cannot be deleted
        DisplayOrder INT DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2,
        CreatedBy NVARCHAR(100) DEFAULT 'System',
        UpdatedBy NVARCHAR(100) DEFAULT 'System',

        -- Unique constraint on Category + Key combination
        CONSTRAINT UQ_Settings_Category_Key UNIQUE (Category, [Key])
    );

    -- Index for faster lookups by category
    CREATE INDEX IX_Settings_Category ON dbo.Settings(Category);

    PRINT 'Created Settings table';
END
GO

-- =====================================================
-- SECTION 2: FILE CLASSIFICATION TABLES
-- =====================================================

-- 2.1 CREATE FILE CLASSIFICATION CATEGORIES TABLE
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FileClassificationCategories]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[FileClassificationCategories](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [Name] [nvarchar](100) NOT NULL,
        [Description] [nvarchar](500) NULL,
        [DisplayOrder] [int] NOT NULL DEFAULT 0,
        [IsActive] [bit] NOT NULL DEFAULT 1,
        [CreatedAt] [datetime] NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] [datetime] NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy] [nvarchar](100) NOT NULL DEFAULT 'System',
        [UpdatedBy] [nvarchar](100) NOT NULL DEFAULT 'System',
    CONSTRAINT [PK_FileClassificationCategories] PRIMARY KEY CLUSTERED ([Id] ASC)
    ) ON [PRIMARY]

    CREATE UNIQUE INDEX [UX_FileClassificationCategories_Name] ON [dbo].[FileClassificationCategories] ([Name])

    PRINT 'Created FileClassificationCategories table';
END
GO

-- 2.2 CREATE FILE CLASSIFICATION RULES TABLE
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FileClassificationRules]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[FileClassificationRules](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [CategoryId] [int] NOT NULL,
        [RuleType] [nvarchar](50) NOT NULL, -- 'Extension', 'PathPattern', 'FileNamePattern', 'SpecificFile'
        [Pattern] [nvarchar](500) NOT NULL,
        [Description] [nvarchar](500) NULL,
        [Priority] [int] NOT NULL DEFAULT 0,
        [IsActive] [bit] NOT NULL DEFAULT 1,
        [CreatedAt] [datetime] NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] [datetime] NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy] [nvarchar](100) NOT NULL DEFAULT 'System',
        [UpdatedBy] [nvarchar](100) NOT NULL DEFAULT 'System',
    CONSTRAINT [PK_FileClassificationRules] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_FileClassificationRules_Category] FOREIGN KEY ([CategoryId])
        REFERENCES [dbo].[FileClassificationCategories] ([Id]) ON DELETE CASCADE
    ) ON [PRIMARY]

    CREATE INDEX [IX_FileClassificationRules_CategoryId] ON [dbo].[FileClassificationRules] ([CategoryId])
    CREATE INDEX [IX_FileClassificationRules_RuleType] ON [dbo].[FileClassificationRules] ([RuleType])
    CREATE INDEX [IX_FileClassificationRules_IsActive] ON [dbo].[FileClassificationRules] ([IsActive])

    PRINT 'Created FileClassificationRules table';
END
GO

-- =====================================================
-- SECTION 3: SEED DATA - Configuration Settings
-- =====================================================

-- Clear existing configuration settings to avoid duplicates
DELETE FROM dbo.Settings WHERE Category IN ('JWT', 'AzureAd', 'Authentication', 'Application', 'AutoSync');
GO

-- 3.1 JWT SETTINGS
INSERT INTO dbo.Settings (Category, [Key], [Value], ValueType, Description, IsSystem, DisplayOrder)
VALUES
    ('JWT', 'Issuer', 'devview-api', 'String', 'JWT token issuer', 1, 1),
    ('JWT', 'Audience', 'devview-api', 'String', 'JWT token audience', 1, 2),
    ('JWT', 'Key', 'aOen425ocYstsnLMsyziQoolYfcEbL9M33KBpZW2iWs=', 'String', 'JWT signing key (32+ characters)', 1, 3),
    ('JWT', 'ExpirationDays', '30', 'Number', 'JWT token expiration in days', 0, 4);

-- 3.2 AZURE AD SETTINGS
INSERT INTO dbo.Settings (Category, [Key], [Value], ValueType, Description, IsSystem, DisplayOrder)
VALUES
    ('AzureAd', 'Enabled', 'false', 'Boolean', 'Enable Azure AD authentication', 0, 1),
    ('AzureAd', 'Instance', 'https://login.microsoftonline.com/', 'String', 'Azure AD instance URL', 1, 2),
    ('AzureAd', 'TenantId', '', 'String', 'Azure AD tenant ID', 0, 3),
    ('AzureAd', 'ClientId', '', 'String', 'Azure AD client ID', 0, 4),
    ('AzureAd', 'ClientSecret', '', 'String', 'Azure AD client secret', 0, 5),
    ('AzureAd', 'CallbackPath', '/signin-oidc', 'String', 'Azure AD callback path', 1, 6);

-- 3.3 AUTHENTICATION SETTINGS
INSERT INTO dbo.Settings (Category, [Key], [Value], ValueType, Description, IsSystem, DisplayOrder)
VALUES
    ('Authentication', 'DefaultProvider', 'Database', 'String', 'Default authentication provider', 0, 1),
    ('Authentication', 'AllowFallback', 'true', 'Boolean', 'Allow fallback authentication', 0, 2),
    ('Authentication', 'AutoCreateUsers', 'true', 'Boolean', 'Auto-create users on first login', 0, 3),
    ('Authentication', 'SessionTimeoutMinutes', '1440', 'Number', 'Session timeout in minutes', 0, 4),
    ('Authentication', 'RequireTwoFactor', 'false', 'Boolean', 'Require two-factor authentication', 0, 5);

-- 3.4 APPLICATION SETTINGS
INSERT INTO dbo.Settings (Category, [Key], [Value], ValueType, Description, IsSystem, DisplayOrder)
VALUES
    ('Application', 'ReportingTimezone', 'AUS Eastern Standard Time', 'String', 'Timezone for reporting', 0, 1);

-- 3.5 AUTOSYNC SETTINGS
INSERT INTO dbo.Settings (Category, [Key], [Value], ValueType, Description, IsSystem, DisplayOrder)
VALUES
    ('AutoSync', 'AutoSyncBatchDays', '30', 'Number', 'Number of days to sync in each batch for historical data', 1, 1),
    ('AutoSync', 'SyncSettings', '{"Mode":"Delta","DeltaSyncDays":30,"SyncTargets":{"Users":true,"Repositories":true,"Commits":true,"PullRequests":true},"Overwrite":false}', 'JSON', 'Sync mode configuration', 1, 2),
    ('AutoSync', 'PollingInterval', '3600', 'Number', 'Polling interval in seconds', 0, 3),
    ('AutoSync', 'MaxRetries', '3', 'Number', 'Maximum number of retry attempts', 0, 4),
    ('AutoSync', 'RetryDelaySeconds', '60', 'Number', 'Delay between retries in seconds', 0, 5);

PRINT 'Configuration settings inserted';
GO

-- =====================================================
-- SECTION 4: FILE CLASSIFICATION IN SETTINGS TABLE
-- For DatabaseFileClassificationService compatibility
-- =====================================================

-- Clear existing file classification settings
DELETE FROM dbo.Settings WHERE Category = 'FileClassification';
GO

-- Insert consolidated file classification configuration as JSON
INSERT INTO dbo.Settings (Category, [Key], [Value], ValueType, Description, IsSystem, DisplayOrder)
VALUES
    ('FileClassification', 'Config', '{
        "dataFiles": {
            "extensions": [".csv",".tsv",".json",".jsonl",".xml",".xlsx",".xls",".sql",".log",".sqlite",".db"],
            "pathPatterns": ["/data/","/logs/","/dumps/","/exports/","/backups/"],
            "fileNamePatterns": ["export_*","dump_*","backup_*","log_*","data_*"]
        },
        "configFiles": {
            "extensions": [".yaml",".yml",".toml",".ini",".cfg",".conf",".env",".properties",".config"],
            "pathPatterns": ["/config/","/settings/","/.github/"],
            "specificFiles": ["package.json","Dockerfile","docker-compose.yml","appsettings.json",".gitignore"]
        },
        "documentationFiles": {
            "extensions": [".md",".rst",".txt",".adoc",".pdf"],
            "pathPatterns": ["/docs/","/documentation/","/wiki/"],
            "specificFiles": ["README.md","CHANGELOG.md","LICENSE","CONTRIBUTING.md"]
        },
        "codeFiles": {
            "extensions": [".cs",".vb",".fs",".py",".js",".ts",".jsx",".tsx",".java",".go",".rs",".cpp"],
            "pathPatterns": ["/src/","/source/","/lib/","/components/","/services/"]
        },
        "testFiles": {
            "extensions": [".test.js",".spec.ts",".test.cs"],
            "pathPatterns": ["/test/","/tests/","/__tests__/","/spec/"],
            "fileNamePatterns": ["*Test.cs","*Tests.cs","test_*.py","*_test.py"]
        },
        "rules": {
            "priority": ["specificFiles","pathPatterns","fileNamePatterns","extensions"],
            "defaultType": "other",
            "caseSensitive": false,
            "enableLogging": true
        }
    }', 'JSON', 'File classification configuration', 1, 1);

PRINT 'File classification configuration inserted for DatabaseFileClassificationService';
GO

-- =====================================================
-- SECTION 5: PRINT SUMMARY
-- =====================================================
PRINT '';
PRINT '===== MIGRATION COMPLETE =====';
PRINT 'Tables created/updated:';
PRINT '  - GitConnections';
PRINT '  - Settings';
PRINT '  - FileClassificationCategories';
PRINT '  - FileClassificationRules';
PRINT '';
PRINT 'Configuration inserted:';
PRINT '  - JWT settings';
PRINT '  - Azure AD settings';
PRINT '  - Authentication settings';
PRINT '  - Application settings';
PRINT '  - AutoSync settings';
PRINT '  - File Classification configuration';
PRINT '';
PRINT 'IMPORTANT: Update the following settings with your production values:';
PRINT '  - JWT.Key (generate a secure key)';
PRINT '  - AzureAd settings (if using Azure AD)';
PRINT '  - Application.ReportingTimezone (if different)';
PRINT '==============================';