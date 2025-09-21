-- =====================================================
-- DevView Database - Drop Unused FileClassification Tables
-- Version: 1.0
-- Date: 2025-09-21
-- Description: Drops FileClassificationCategories and FileClassificationRules tables
--              as they are not used by the application (uses Settings table instead)
-- =====================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- =====================================================
-- SECTION 1: DROP FILE CLASSIFICATION TABLES
-- =====================================================

-- 1.1 DROP FILE CLASSIFICATION RULES TABLE (must drop first due to foreign key)
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FileClassificationRules]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[FileClassificationRules];
    PRINT 'Dropped FileClassificationRules table';
END
ELSE
BEGIN
    PRINT 'FileClassificationRules table does not exist';
END
GO

-- 1.2 DROP FILE CLASSIFICATION CATEGORIES TABLE
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FileClassificationCategories]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[FileClassificationCategories];
    PRINT 'Dropped FileClassificationCategories table';
END
ELSE
BEGIN
    PRINT 'FileClassificationCategories table does not exist';
END
GO

-- =====================================================
-- SECTION 2: VERIFY FILE CLASSIFICATION CONFIG IN SETTINGS
-- =====================================================

-- Check if FileClassification settings exist in Settings table
IF EXISTS (SELECT 1 FROM dbo.Settings WHERE Category = 'FileClassification' AND [Key] = 'Config')
BEGIN
    PRINT 'FileClassification configuration exists in Settings table (this is what the app uses)';
END
ELSE
BEGIN
    PRINT 'WARNING: FileClassification configuration NOT found in Settings table';
    PRINT 'You may need to insert the configuration using PRODUCTION-migration-consolidated.sql';
END
GO

-- =====================================================
-- SECTION 3: VERIFICATION
-- =====================================================
PRINT '';
PRINT '===== CLEANUP COMPLETE =====';
PRINT 'Tables dropped:'
PRINT '  - FileClassificationRules'
PRINT '  - FileClassificationCategories';
PRINT '';
PRINT 'Note: The application uses Settings table for file classification,';
PRINT '      not these separate tables.';
PRINT '';
PRINT 'To verify tables are gone:';
PRINT 'SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES';
PRINT 'WHERE TABLE_NAME LIKE ''FileClassification%''';
PRINT '===============================';