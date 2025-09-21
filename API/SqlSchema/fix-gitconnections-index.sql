-- Fix for GitConnections QUOTED_IDENTIFIER issue
-- The unique filtered index causes issues with UPDATE statements

-- Drop the problematic unique filtered index if it exists
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_GitConnections_Active_Priority' AND object_id = OBJECT_ID('GitConnections'))
BEGIN
    DROP INDEX UX_GitConnections_Active_Priority ON [dbo].[GitConnections];
    PRINT 'Dropped unique filtered index UX_GitConnections_Active_Priority';
END
GO

-- Create a regular non-unique index instead for performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_GitConnections_Active_Priority' AND object_id = OBJECT_ID('GitConnections'))
BEGIN
    CREATE INDEX IX_GitConnections_Active_Priority
    ON [dbo].[GitConnections] ([GitServerType], [Priority], [IsActive]);
    PRINT 'Created non-unique index IX_GitConnections_Active_Priority';
END
GO

PRINT 'GitConnections index fix completed successfully';