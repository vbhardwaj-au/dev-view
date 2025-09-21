-- =====================================================
-- DevView Database - Add Missing File Classification Tables
-- Version: 1.0
-- Date: 2025-09-21
-- Description: Creates FileClassificationCategories and FileClassificationRules tables
--              that were missing from production
-- =====================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- =====================================================
-- SECTION 1: CREATE FILE CLASSIFICATION TABLES
-- =====================================================

-- 1.1 CREATE FILE CLASSIFICATION CATEGORIES TABLE
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
ELSE
BEGIN
    PRINT 'FileClassificationCategories table already exists';
END
GO

-- 1.2 CREATE FILE CLASSIFICATION RULES TABLE
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
ELSE
BEGIN
    PRINT 'FileClassificationRules table already exists';
END
GO

-- =====================================================
-- SECTION 2: INSERT FILE CLASSIFICATION CONFIGURATION
-- =====================================================

-- Check if FileClassification settings already exist
IF NOT EXISTS (SELECT 1 FROM dbo.Settings WHERE Category = 'FileClassification' AND [Key] = 'Config')
BEGIN
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

    PRINT 'File classification configuration inserted';
END
ELSE
BEGIN
    PRINT 'File classification configuration already exists';
END
GO

-- =====================================================
-- SECTION 3: VERIFICATION
-- =====================================================
PRINT '';
PRINT '===== MIGRATION COMPLETE =====';
PRINT 'Tables created:'
PRINT '  - FileClassificationCategories'
PRINT '  - FileClassificationRules';
PRINT '';
PRINT 'Configuration added:'
PRINT '  - FileClassification settings';
PRINT '';
PRINT 'To verify, run:';
PRINT 'SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES';
PRINT 'WHERE TABLE_NAME LIKE ''FileClassification%''';
PRINT '===============================';