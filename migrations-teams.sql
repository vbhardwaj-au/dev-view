-- Teams Feature Migration Script
-- Run this script against your existing DevView database to add Teams functionality
-- 
-- DevView - .NET 9 Bitbucket Analytics Solution
-- Copyright (c) 2025 Vikas Bhardwaj

-- Check if Teams table already exists
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Teams' AND xtype='U')
BEGIN
    PRINT 'Creating Teams table...'
    
    -- Teams table for organizing users into teams
    CREATE TABLE Teams (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(255) NOT NULL UNIQUE,
        Description NVARCHAR(1000),
        CreatedOn DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy NVARCHAR(255), -- BitbucketUserId of team creator
        IsActive BIT NOT NULL DEFAULT 1
    );
    
    PRINT 'Teams table created successfully.'
END
ELSE
BEGIN
    PRINT 'Teams table already exists. Skipping creation.'
END

-- Check if TeamMembers table already exists
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='TeamMembers' AND xtype='U')
BEGIN
    PRINT 'Creating TeamMembers table...'
    
    -- TeamMembers table for many-to-many relationship between Teams and Users
    CREATE TABLE TeamMembers (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        TeamId INT NOT NULL,
        UserId INT NOT NULL,
        AddedOn DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        AddedBy NVARCHAR(255), -- BitbucketUserId of person who added the member
        FOREIGN KEY (TeamId) REFERENCES Teams(Id) ON DELETE CASCADE,
        FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
        UNIQUE (TeamId, UserId) -- Prevent duplicate team memberships
    );
    
    PRINT 'TeamMembers table created successfully.'
END
ELSE
BEGIN
    PRINT 'TeamMembers table already exists. Skipping creation.'
END

-- Create indexes for performance
PRINT 'Creating indexes for Teams tables...'

-- Indexes for Teams table
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Teams_Name')
    CREATE INDEX IX_Teams_Name ON Teams(Name);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Teams_IsActive')
    CREATE INDEX IX_Teams_IsActive ON Teams(IsActive);

-- Indexes for TeamMembers table
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TeamMembers_TeamId')
    CREATE INDEX IX_TeamMembers_TeamId ON TeamMembers(TeamId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TeamMembers_UserId')
    CREATE INDEX IX_TeamMembers_UserId ON TeamMembers(UserId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TeamMembers_TeamId_UserId')
    CREATE INDEX IX_TeamMembers_TeamId_UserId ON TeamMembers(TeamId, UserId);

PRINT 'Indexes created successfully.'

-- Insert sample teams (optional - remove if not needed)
PRINT 'Creating sample teams...'

IF NOT EXISTS (SELECT * FROM Teams WHERE Name = 'Frontend Team')
BEGIN
    INSERT INTO Teams (Name, Description, CreatedBy) 
    VALUES ('Frontend Team', 'Responsible for user interface development and user experience', 'system');
    PRINT 'Sample Frontend Team created.'
END

IF NOT EXISTS (SELECT * FROM Teams WHERE Name = 'Backend Team')
BEGIN
    INSERT INTO Teams (Name, Description, CreatedBy) 
    VALUES ('Backend Team', 'Handles server-side development and API integration', 'system');
    PRINT 'Sample Backend Team created.'
END

IF NOT EXISTS (SELECT * FROM Teams WHERE Name = 'DevOps Team')
BEGIN
    INSERT INTO Teams (Name, Description, CreatedBy) 
    VALUES ('DevOps Team', 'Manages deployment, infrastructure, and CI/CD pipelines', 'system');
    PRINT 'Sample DevOps Team created.'
END

PRINT 'Teams feature migration completed successfully!'
PRINT ''
PRINT 'Next steps:'
PRINT '1. Build and deploy the updated application'
PRINT '2. Navigate to Admin > Teams in the web interface'
PRINT '3. Create your teams and assign members'
PRINT '4. Teams will now be available as filters throughout the application' 