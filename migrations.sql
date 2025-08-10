-- Migration script for adding sync tracking columns
-- Run this script against existing databases to add the new columns

-- Add LastDeltaSyncDate column to Repositories table
ALTER TABLE Repositories ADD LastDeltaSyncDate DATETIME2 NULL;

-- Add CommitCount column to RepositorySyncLog table
ALTER TABLE RepositorySyncLog ADD CommitCount INT NULL;

-- Create index for performance on the new column
CREATE INDEX IX_Repositories_LastDeltaSyncDate ON Repositories(LastDeltaSyncDate);

-- Add a comment for documentation
EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Date when the repository was last synced by AutoSync or manual sync (Delta mode)', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'Repositories', 
    @level2type = N'COLUMN', @level2name = N'LastDeltaSyncDate';

-- Add a comment for CommitCount column
EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'Number of commits synced during this sync operation (includes commits from PR sync)', 
    @level0type = N'SCHEMA', @level0name = N'dbo', 
    @level1type = N'TABLE', @level1name = N'RepositorySyncLog', 
    @level2type = N'COLUMN', @level2name = N'CommitCount'; 