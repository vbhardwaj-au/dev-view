-- Database schema for BitBucket Analytics

-- DEV/RESET: Drop existing tables to allow clean re-create (order matters due to FKs)
IF OBJECT_ID('dbo.PullRequestApprovals','U') IS NOT NULL DROP TABLE dbo.PullRequestApprovals;
IF OBJECT_ID('dbo.PullRequestCommits','U') IS NOT NULL DROP TABLE dbo.PullRequestCommits;
IF OBJECT_ID('dbo.CommitFiles','U') IS NOT NULL DROP TABLE dbo.CommitFiles;
IF OBJECT_ID('dbo.RepositorySyncLog','U') IS NOT NULL DROP TABLE dbo.RepositorySyncLog;
IF OBJECT_ID('dbo.PullRequests','U') IS NOT NULL DROP TABLE dbo.PullRequests;
IF OBJECT_ID('dbo.Commits','U') IS NOT NULL DROP TABLE dbo.Commits;
IF OBJECT_ID('dbo.TeamMembers','U') IS NOT NULL DROP TABLE dbo.TeamMembers;
IF OBJECT_ID('dbo.Teams','U') IS NOT NULL DROP TABLE dbo.Teams;
IF OBJECT_ID('dbo.Users','U') IS NOT NULL DROP TABLE dbo.Users;
IF OBJECT_ID('dbo.Repositories','U') IS NOT NULL DROP TABLE dbo.Repositories;

-- Users table
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BitbucketUserId NVARCHAR(255) NOT NULL UNIQUE,
    DisplayName NVARCHAR(255) NOT NULL,
    AvatarUrl NVARCHAR(500),
    CreatedOn DATETIME2,
    ExcludeFromReporting BIT NOT NULL DEFAULT 0 -- Hide user from reporting/UI
);

-- Repositories table
CREATE TABLE Repositories (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BitbucketRepoId NVARCHAR(255),
    Name NVARCHAR(255) NOT NULL,
    Slug NVARCHAR(255) NOT NULL,
    Workspace NVARCHAR(255) NOT NULL,
    CreatedOn DATETIME2,
    LastDeltaSyncDate DATETIME2 NULL, -- New column for tracking last delta sync
    ExcludeFromSync BIT NOT NULL DEFAULT 0, -- Exclude this repository from synchronization
    ExcludeFromReporting BIT NOT NULL DEFAULT 0 -- Exclude this repository from analytics/reporting
);

-- Commits table
CREATE TABLE Commits (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BitbucketCommitHash NVARCHAR(255) NOT NULL UNIQUE,
    RepositoryId INT NOT NULL,
    AuthorId INT NOT NULL,
    Date DATETIME2 NOT NULL,
    Message NVARCHAR(MAX),
    LinesAdded INT DEFAULT 0,
    LinesRemoved INT DEFAULT 0,
    IsMerge BIT NOT NULL DEFAULT 0,
    IsRevert BIT NOT NULL DEFAULT 0,
    CodeLinesAdded INT,
    CodeLinesRemoved INT,
    DataLinesAdded INT NOT NULL DEFAULT 0,
    DataLinesRemoved INT NOT NULL DEFAULT 0,
    ConfigLinesAdded INT NOT NULL DEFAULT 0,
    ConfigLinesRemoved INT NOT NULL DEFAULT 0,
    DocsLinesAdded INT NOT NULL DEFAULT 0,
    DocsLinesRemoved INT NOT NULL DEFAULT 0,
    IsPRMergeCommit BIT NOT NULL DEFAULT 0,
    FOREIGN KEY (RepositoryId) REFERENCES Repositories(Id),
    FOREIGN KEY (AuthorId) REFERENCES Users(Id)
);

-- Pull Requests table
CREATE TABLE PullRequests (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BitbucketPrId NVARCHAR(255) NOT NULL,
    RepositoryId INT NOT NULL,
    AuthorId INT NOT NULL,
    Title NVARCHAR(MAX),
    State NVARCHAR(50),
    CreatedOn DATETIME2,
    UpdatedOn DATETIME2,
    MergedOn DATETIME2,
    ClosedOn DATETIME2, -- New column for when the PR was closed
    IsRevert BIT NOT NULL DEFAULT 0, -- New column to track if PR is a revert
    FOREIGN KEY (RepositoryId) REFERENCES Repositories(Id),
    FOREIGN KEY (AuthorId) REFERENCES Users(Id),
    UNIQUE (RepositoryId, BitbucketPrId) -- Composite unique key
);

-- New table for Pull Request Approvals
CREATE TABLE PullRequestApprovals (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PullRequestId INT NOT NULL,
    UserUuid NVARCHAR(255) NOT NULL,
    DisplayName NVARCHAR(255) NOT NULL,
    Role NVARCHAR(50), -- e.g., 'REVIEWER', 'PARTICIPANT'
    Approved BIT NOT NULL, -- True if approved, False if not (e.g., changes requested)
    State NVARCHAR(50), -- e.g., 'approved', 'changes_requested', 'needs_work'
    ApprovedOn DATETIME2, -- Timestamp of the approval
    FOREIGN KEY (PullRequestId) REFERENCES PullRequests(Id),
    -- Consider adding a unique constraint if an approval by a specific user for a specific PR can only exist once
    UNIQUE (PullRequestId, UserUuid)
);

-- Teams table for organizing users into teams
CREATE TABLE Teams (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL UNIQUE,
    Description NVARCHAR(1000),
    CreatedOn DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy NVARCHAR(255), -- BitbucketUserId of team creator
    IsActive BIT NOT NULL DEFAULT 1
);

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

-- Indexes for performance
CREATE INDEX IX_Commits_RepositoryId ON Commits(RepositoryId);
CREATE INDEX IX_Commits_AuthorId ON Commits(AuthorId);
CREATE INDEX IX_Commits_Date ON Commits(Date);
CREATE INDEX IX_Commits_BitbucketCommitHash ON Commits(BitbucketCommitHash);
CREATE INDEX IX_Commits_DataLines ON Commits(DataLinesAdded, DataLinesRemoved);
CREATE INDEX IX_Commits_ConfigLines ON Commits(ConfigLinesAdded, ConfigLinesRemoved);
CREATE INDEX IX_Commits_DocsLines ON Commits(DocsLinesAdded, DocsLinesRemoved);
CREATE INDEX IX_Commits_IsRevert ON Commits(IsRevert);
CREATE INDEX IX_PullRequests_RepositoryId ON PullRequests(RepositoryId);
CREATE INDEX IX_PullRequests_AuthorId ON PullRequests(AuthorId);
CREATE INDEX IX_PullRequests_State ON PullRequests(State);
CREATE INDEX IX_PullRequests_IsRevert ON PullRequests(IsRevert);
-- (CommitFiles indexes moved below, after table creation)

-- Indexes for repository exclusion flags
CREATE INDEX IX_Repositories_ExcludeFromSync ON Repositories(ExcludeFromSync);
CREATE INDEX IX_Repositories_ExcludeFromReporting ON Repositories(ExcludeFromReporting);

-- Index for user exclusion flag
CREATE INDEX IX_Users_ExcludeFromReporting ON Users(ExcludeFromReporting);

-- PullRequestCommits join table
CREATE TABLE PullRequestCommits (
    PullRequestId INT NOT NULL,
    CommitId INT NOT NULL,
    PRIMARY KEY (PullRequestId, CommitId),
    FOREIGN KEY (PullRequestId) REFERENCES PullRequests(Id),
    FOREIGN KEY (CommitId) REFERENCES Commits(Id)
);
CREATE INDEX IX_PullRequestCommits_PullRequestId ON PullRequestCommits(PullRequestId);
CREATE INDEX IX_PullRequestCommits_CommitId ON PullRequestCommits(CommitId);

-- CommitFiles table for detailed file-level tracking
CREATE TABLE CommitFiles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CommitId INT NOT NULL,
    FilePath NVARCHAR(500) NOT NULL,
    FileType NVARCHAR(20) NOT NULL, -- 'code', 'data', 'config', 'docs', 'other'
    ChangeStatus NVARCHAR(20) NOT NULL, -- 'added', 'modified', 'removed'
    LinesAdded INT NOT NULL DEFAULT 0,
    LinesRemoved INT NOT NULL DEFAULT 0,
    FileExtension NVARCHAR(50),
    CreatedOn DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExcludeFromReporting BIT NOT NULL DEFAULT 0, -- New column to exclude files from reporting
    FOREIGN KEY (CommitId) REFERENCES Commits(Id) ON DELETE CASCADE
);

-- Indexes for CommitFiles (must come after table creation)
CREATE INDEX IX_CommitFiles_CommitId ON CommitFiles(CommitId);
CREATE INDEX IX_CommitFiles_FileType ON CommitFiles(FileType);
CREATE INDEX IX_CommitFiles_ChangeStatus ON CommitFiles(ChangeStatus);
CREATE INDEX IX_CommitFiles_FileExtension ON CommitFiles(FileExtension);
CREATE INDEX IX_CommitFiles_ExcludeFromReporting ON CommitFiles(ExcludeFromReporting);

-- Update script for existing DB
-- Add the new column only if it doesn't already exist
IF COL_LENGTH('Commits','IsPRMergeCommit') IS NULL
BEGIN
    ALTER TABLE Commits ADD IsPRMergeCommit BIT NOT NULL DEFAULT 0;
END

-- Add columns to Repositories if running as an update script (ignore errors if they already exist)
-- Add repository exclusion flags only if they don't already exist
IF COL_LENGTH('Repositories','ExcludeFromSync') IS NULL
BEGIN
    ALTER TABLE Repositories ADD ExcludeFromSync BIT NOT NULL DEFAULT 0;
END
IF COL_LENGTH('Repositories','ExcludeFromReporting') IS NULL
BEGIN
    ALTER TABLE Repositories ADD ExcludeFromReporting BIT NOT NULL DEFAULT 0;
END

-- Add user exclusion flag if missing
IF COL_LENGTH('Users','ExcludeFromReporting') IS NULL
BEGIN
    ALTER TABLE Users ADD ExcludeFromReporting BIT NOT NULL DEFAULT 0;
END

-- Create index on Users.ExcludeFromReporting if missing
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_ExcludeFromReporting' AND object_id = OBJECT_ID('Users'))
BEGIN
    CREATE INDEX IX_Users_ExcludeFromReporting ON Users(ExcludeFromReporting);
END

-- DEPRECATED: Old message-based logic (kept for reference)
-- Set IsMerge for commits whose message starts with 'merge' or 'Merged' (case-insensitive)
-- UPDATE Commits
-- SET IsMerge = 1
-- WHERE LOWER(LEFT(LTRIM(Message), 5)) = 'merge' OR LOWER(LEFT(LTRIM(Message), 6)) = 'merged';

-- DEPRECATED: Old PR merge commit detection (kept for reference)
-- Set IsPRMergeCommit for commits that are the merge_commit for a PR
-- UPDATE Commits
-- SET IsPRMergeCommit = 1
-- WHERE BitbucketCommitHash IN (
--     SELECT pr.MergeCommitHash
--     FROM (
--         SELECT BitbucketPrId, (SELECT TOP 1 BitbucketCommitHash FROM Commits WHERE Commits.Message LIKE '%pull request%' AND Commits.Message LIKE '%' + CAST(PullRequests.BitbucketPrId AS NVARCHAR) + '%') AS MergeCommitHash
--         FROM PullRequests
--     ) pr
--     WHERE pr.MergeCommitHash IS NOT NULL
-- );

-- NEW LOGIC: Use parents array from Bitbucket API
-- IsMerge = true when commit has 2+ parents (merge commits)
-- IsPRMergeCommit = true when commit has 2+ parents (most merge commits are PR merges)

-- NOTE: The new logic is implemented in the sync services:
-- - BitbucketCommitsService.cs: Uses commit.Parents.Count >= 2 for both IsMerge and IsPRMergeCommit
-- - BitbucketPullRequestsService.cs: Uses commit.Parents.Count >= 2 for both IsMerge and IsPRMergeCommit
-- - Both flags are now set to the same value (true for merge commits, false for regular commits)
-- 
-- For existing data, you need to re-sync commits to get the correct parent information
-- from the Bitbucket API, as the parents array was not previously captured.
--
-- MIGRATION STEPS:
-- 1. Deploy the updated code with parents array support
-- 2. Re-sync all repositories to capture parent information and update flags
-- 3. The sync process will automatically set the correct IsMerge and IsPRMergeCommit values

-- Clean up incorrect data (optional - only if you want to reset before re-sync)
-- UPDATE Commits SET IsMerge = 0, IsPRMergeCommit = 0;

-- POST-SYNC FIX: Update IsPRMergeCommit for commits that are merge commits and associated with PRs
-- Run this AFTER both commit sync and PR sync are complete
-- This fixes the case where commit sync runs first and sets IsPRMergeCommit=0 for all commits
UPDATE Commits 
SET IsPRMergeCommit = 1
WHERE IsMerge = 1 
  AND Id IN (
      SELECT DISTINCT prc.CommitId 
      FROM PullRequestCommits prc
      INNER JOIN PullRequests pr ON prc.PullRequestId = pr.Id
  );

-- Verification query: Check the results
-- SELECT 
--     COUNT(*) as TotalCommits,
--     SUM(CASE WHEN IsMerge = 1 THEN 1 ELSE 0 END) as MergeCommits,
--     SUM(CASE WHEN IsPRMergeCommit = 1 THEN 1 ELSE 0 END) as PRMergeCommits,
--     SUM(CASE WHEN IsMerge = 1 AND IsPRMergeCommit = 0 THEN 1 ELSE 0 END) as MergeCommitsNotInPR
-- FROM Commits;

-- RepositorySyncLog table for autosync logging
CREATE TABLE RepositorySyncLog (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    RepositoryId INT NOT NULL,
    StartDate DATETIME2 NOT NULL,
    EndDate DATETIME2 NOT NULL,
    SyncedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Status NVARCHAR(50) NOT NULL, -- e.g., 'Started', 'Completed', 'Failed'
    Message NVARCHAR(MAX) NULL,   -- error or info message
    CommitCount INT NULL,         -- number of commits synced in this operation
    FOREIGN KEY (RepositoryId) REFERENCES Repositories(Id)
);

CREATE INDEX IX_RepositorySyncLog_RepositoryId ON RepositorySyncLog(RepositoryId);
CREATE INDEX IX_RepositorySyncLog_StartEndDate ON RepositorySyncLog(StartDate, EndDate);

-- Indexes for performance on new table
CREATE INDEX IX_PullRequestApprovals_PullRequestId ON PullRequestApprovals(PullRequestId);
CREATE INDEX IX_PullRequestApprovals_UserUuid ON PullRequestApprovals(UserUuid);
CREATE INDEX IX_PullRequestApprovals_Approved ON PullRequestApprovals(Approved);

-- Indexes for Teams tables
CREATE INDEX IX_Teams_Name ON Teams(Name);
CREATE INDEX IX_Teams_IsActive ON Teams(IsActive);
CREATE INDEX IX_TeamMembers_TeamId ON TeamMembers(TeamId);
CREATE INDEX IX_TeamMembers_UserId ON TeamMembers(UserId);
CREATE INDEX IX_TeamMembers_TeamId_UserId ON TeamMembers(TeamId, UserId);

-- Authentication & Authorization tables (simple custom auth with Azure AD support)
CREATE TABLE AuthUsers (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(100) NOT NULL UNIQUE,
    PasswordHash VARBINARY(64) NOT NULL, -- SHA2_512
    PasswordSalt VARBINARY(32) NOT NULL, -- random salt
    DisplayName NVARCHAR(255) NULL,
    Email NVARCHAR(255) NULL, -- Azure AD integration
    JobTitle NVARCHAR(255) NULL, -- Azure AD integration
    Department NVARCHAR(255) NULL, -- Azure AD integration
    AuthProvider NVARCHAR(50) NOT NULL DEFAULT 'Database', -- 'Database' or 'AzureAd'
    AzureAdObjectId NVARCHAR(100) NULL, -- Azure AD Object ID
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedOn DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ModifiedOn DATETIME2 NULL,
    CONSTRAINT CK_AuthUsers_AuthProvider CHECK (AuthProvider IN ('Database', 'AzureAd'))
);

CREATE TABLE AuthRoles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(50) NOT NULL UNIQUE,
    Description NVARCHAR(500) NULL,
    CreatedOn DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE TABLE AuthUserRoles (
    UserId INT NOT NULL,
    RoleId INT NOT NULL,
    PRIMARY KEY (UserId, RoleId),
    FOREIGN KEY (UserId) REFERENCES AuthUsers(Id) ON DELETE CASCADE,
    FOREIGN KEY (RoleId) REFERENCES AuthRoles(Id) ON DELETE CASCADE
);

CREATE TABLE ApiKeys (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    KeyHash VARBINARY(64) NOT NULL, -- SHA2_512 of key+salt
    KeySalt VARBINARY(32) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedOn DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExpiresOn DATETIME2 NULL,
    CONSTRAINT UQ_ApiKeys_Name UNIQUE (Name)
);

-- Indexes
CREATE INDEX IX_AuthUsers_Username ON AuthUsers(Username);
CREATE INDEX IX_AuthUsers_Email ON AuthUsers(Email);
CREATE INDEX IX_AuthUsers_AzureAdObjectId ON AuthUsers(AzureAdObjectId);
CREATE INDEX IX_AuthUsers_AuthProvider ON AuthUsers(AuthProvider);
CREATE INDEX IX_AuthUserRoles_UserId ON AuthUserRoles(UserId);
CREATE INDEX IX_AuthUserRoles_RoleId ON AuthUserRoles(RoleId);
