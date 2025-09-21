-- =====================================================
-- DevView Database Production Migration - GitConnections Updates
-- Version: 1.0
-- Date: 2025-09-21
-- Description: ALTER script to update GitConnections table with new columns
--
-- IMPORTANT: Run this AFTER PRODUCTION-migration-consolidated.sql
-- =====================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- =====================================================
-- SECTION 1: ALTER GitConnections Table
-- =====================================================

-- 1.1 Add Priority column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GitConnections]') AND name = 'Priority')
BEGIN
    ALTER TABLE [dbo].[GitConnections]
    ADD [Priority] INT NOT NULL DEFAULT 0;

    PRINT 'Added Priority column to GitConnections table';
END
GO

-- 1.2 Add Workspace column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GitConnections]') AND name = 'Workspace')
BEGIN
    ALTER TABLE [dbo].[GitConnections]
    ADD [Workspace] NVARCHAR(100) NULL;

    PRINT 'Added Workspace column to GitConnections table';
END
GO

-- 1.3 Add AdditionalSettings column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GitConnections]') AND name = 'AdditionalSettings')
BEGIN
    ALTER TABLE [dbo].[GitConnections]
    ADD [AdditionalSettings] NVARCHAR(MAX) NULL;

    PRINT 'Added AdditionalSettings column to GitConnections table';
END
GO

-- 1.4 Rename AppPassword to Password if needed
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GitConnections]') AND name = 'AppPassword')
BEGIN
    EXEC sp_rename 'dbo.GitConnections.AppPassword', 'Password', 'COLUMN';
    PRINT 'Renamed AppPassword column to Password in GitConnections table';
END
GO

-- 1.5 Drop RefreshToken column if it exists (not in production DB)
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GitConnections]') AND name = 'RefreshToken')
BEGIN
    ALTER TABLE [dbo].[GitConnections]
    DROP COLUMN [RefreshToken];

    PRINT 'Dropped RefreshToken column from GitConnections table';
END
GO

-- 1.6 Drop LastSyncDate column if it exists (not in production DB)
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GitConnections]') AND name = 'LastSyncDate')
BEGIN
    ALTER TABLE [dbo].[GitConnections]
    DROP COLUMN [LastSyncDate];

    PRINT 'Dropped LastSyncDate column from GitConnections table';
END
GO

-- 1.7 Modify ConsumerKey column size if needed
IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[dbo].[GitConnections]')
           AND name = 'ConsumerKey'
           AND max_length != 400) -- NVARCHAR(200) = 400 bytes
BEGIN
    ALTER TABLE [dbo].[GitConnections]
    ALTER COLUMN [ConsumerKey] NVARCHAR(200);

    PRINT 'Modified ConsumerKey column size to NVARCHAR(200)';
END
GO

-- =====================================================
-- SECTION 2: VERIFICATION QUERY
-- =====================================================
PRINT '';
PRINT '===== MIGRATION COMPLETE =====';
PRINT 'To verify the changes, run:';
PRINT 'SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE';
PRINT 'FROM INFORMATION_SCHEMA.COLUMNS';
PRINT 'WHERE TABLE_NAME = ''GitConnections''';
PRINT 'ORDER BY ORDINAL_POSITION;';
PRINT '==============================';