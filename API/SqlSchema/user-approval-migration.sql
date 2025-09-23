-- =====================================================
-- DevView Database - User Approval System Migration
-- Version: 1.0
-- Date: 2025-09-21
-- Description: Adds user approval workflow and notification system
-- =====================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- =====================================================
-- SECTION 1: ALTER AuthUsers Table for Approval Workflow
-- =====================================================

-- 1.1 Add ApprovalStatus column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AuthUsers]') AND name = 'ApprovalStatus')
BEGIN
    ALTER TABLE [dbo].[AuthUsers]
    ADD [ApprovalStatus] NVARCHAR(20) NOT NULL DEFAULT 'Approved'
    CONSTRAINT CK_AuthUsers_ApprovalStatus CHECK (ApprovalStatus IN ('Pending', 'Approved', 'Rejected'));

    PRINT 'Added ApprovalStatus column to AuthUsers table';
END
GO

-- 1.2 Add RequestedAt column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AuthUsers]') AND name = 'RequestedAt')
BEGIN
    ALTER TABLE [dbo].[AuthUsers]
    ADD [RequestedAt] DATETIME2 NULL;

    PRINT 'Added RequestedAt column to AuthUsers table';
END
GO

-- 1.3 Add ApprovedAt column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AuthUsers]') AND name = 'ApprovedAt')
BEGIN
    ALTER TABLE [dbo].[AuthUsers]
    ADD [ApprovedAt] DATETIME2 NULL;

    PRINT 'Added ApprovedAt column to AuthUsers table';
END
GO

-- 1.4 Add ApprovedBy column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AuthUsers]') AND name = 'ApprovedBy')
BEGIN
    ALTER TABLE [dbo].[AuthUsers]
    ADD [ApprovedBy] INT NULL;

    -- Add foreign key constraint
    ALTER TABLE [dbo].[AuthUsers]
    ADD CONSTRAINT FK_AuthUsers_ApprovedBy
    FOREIGN KEY ([ApprovedBy]) REFERENCES [dbo].[AuthUsers]([Id]);

    PRINT 'Added ApprovedBy column to AuthUsers table';
END
GO

-- 1.5 Add RejectedAt column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AuthUsers]') AND name = 'RejectedAt')
BEGIN
    ALTER TABLE [dbo].[AuthUsers]
    ADD [RejectedAt] DATETIME2 NULL;

    PRINT 'Added RejectedAt column to AuthUsers table';
END
GO

-- 1.6 Add RejectedBy column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AuthUsers]') AND name = 'RejectedBy')
BEGIN
    ALTER TABLE [dbo].[AuthUsers]
    ADD [RejectedBy] INT NULL;

    -- Add foreign key constraint
    ALTER TABLE [dbo].[AuthUsers]
    ADD CONSTRAINT FK_AuthUsers_RejectedBy
    FOREIGN KEY ([RejectedBy]) REFERENCES [dbo].[AuthUsers]([Id]);

    PRINT 'Added RejectedBy column to AuthUsers table';
END
GO

-- 1.7 Add RejectionReason column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AuthUsers]') AND name = 'RejectionReason')
BEGIN
    ALTER TABLE [dbo].[AuthUsers]
    ADD [RejectionReason] NVARCHAR(500) NULL;

    PRINT 'Added RejectionReason column to AuthUsers table';
END
GO

-- 1.8 Add LinkedBitbucketUserId column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AuthUsers]') AND name = 'LinkedBitbucketUserId')
BEGIN
    ALTER TABLE [dbo].[AuthUsers]
    ADD [LinkedBitbucketUserId] INT NULL;

    -- Add foreign key constraint to Users table
    ALTER TABLE [dbo].[AuthUsers]
    ADD CONSTRAINT FK_AuthUsers_LinkedBitbucketUser
    FOREIGN KEY ([LinkedBitbucketUserId]) REFERENCES [dbo].[Users]([Id]);

    PRINT 'Added LinkedBitbucketUserId column to AuthUsers table';
END
GO

-- 1.9 Add Notes column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AuthUsers]') AND name = 'Notes')
BEGIN
    ALTER TABLE [dbo].[AuthUsers]
    ADD [Notes] NVARCHAR(MAX) NULL;

    PRINT 'Added Notes column to AuthUsers table';
END
GO

-- 1.10 Add RequestReason column for user's access request reason
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AuthUsers]') AND name = 'RequestReason')
BEGIN
    ALTER TABLE [dbo].[AuthUsers]
    ADD [RequestReason] NVARCHAR(500) NULL;

    PRINT 'Added RequestReason column to AuthUsers table';
END
GO

-- 1.11 Add Team column for user's team selection
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AuthUsers]') AND name = 'Team')
BEGIN
    ALTER TABLE [dbo].[AuthUsers]
    ADD [Team] NVARCHAR(100) NULL;

    PRINT 'Added Team column to AuthUsers table';
END
GO

-- =====================================================
-- SECTION 2: CREATE NotificationConfig Table
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[NotificationConfig]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[NotificationConfig] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [MenuItemKey] NVARCHAR(100) NOT NULL UNIQUE,
        [DisplayName] NVARCHAR(100) NOT NULL,
        [QueryType] NVARCHAR(20) NOT NULL, -- 'SQL', 'API', 'Static'
        [Query] NVARCHAR(MAX) NULL,
        [ApiEndpoint] NVARCHAR(500) NULL,
        [StaticValue] INT NULL,
        [RefreshIntervalSeconds] INT NOT NULL DEFAULT 60,
        [DisplayType] NVARCHAR(20) NOT NULL DEFAULT 'Badge', -- 'Badge', 'Dot', 'Both'
        [PulseOnNew] BIT NOT NULL DEFAULT 1,
        [MinimumRole] NVARCHAR(50) NULL, -- Minimum role required to see notification
        [IsActive] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT CK_NotificationConfig_QueryType CHECK (QueryType IN ('SQL', 'API', 'Static')),
        CONSTRAINT CK_NotificationConfig_DisplayType CHECK (DisplayType IN ('Badge', 'Dot', 'Both'))
    );

    PRINT 'Created NotificationConfig table';
END
GO

-- =====================================================
-- SECTION 3: CREATE NotificationState Table (Track viewed state)
-- =====================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[NotificationState]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[NotificationState] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [UserId] INT NOT NULL,
        [MenuItemKey] NVARCHAR(100) NOT NULL,
        [LastViewedCount] INT NOT NULL DEFAULT 0,
        [LastViewedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [LastCheckedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FOREIGN KEY ([UserId]) REFERENCES [dbo].[AuthUsers]([Id]) ON DELETE CASCADE,
        UNIQUE ([UserId], [MenuItemKey])
    );

    CREATE INDEX IX_NotificationState_UserId ON NotificationState(UserId);
    CREATE INDEX IX_NotificationState_MenuItemKey ON NotificationState(MenuItemKey);

    PRINT 'Created NotificationState table';
END
GO

-- =====================================================
-- SECTION 4: INSERT Default Notification Configurations
-- =====================================================

-- Insert notification config for user approvals
IF NOT EXISTS (SELECT 1 FROM [dbo].[NotificationConfig] WHERE MenuItemKey = 'user-approvals')
BEGIN
    INSERT INTO [dbo].[NotificationConfig]
    (MenuItemKey, DisplayName, QueryType, Query, RefreshIntervalSeconds, DisplayType, PulseOnNew, MinimumRole, IsActive)
    VALUES
    ('user-approvals', 'Pending User Approvals', 'SQL',
     'SELECT COUNT(*) FROM AuthUsers WHERE ApprovalStatus = ''Pending''',
     60, 'Badge', 1, 'Admin', 1);

    PRINT 'Inserted user-approvals notification config';
END
GO

-- =====================================================
-- SECTION 5: UPDATE Existing Users to Approved Status
-- =====================================================

-- Set all existing users to Approved status
UPDATE [dbo].[AuthUsers]
SET [ApprovalStatus] = 'Approved',
    [ApprovedAt] = GETUTCDATE()
WHERE [ApprovalStatus] IS NULL OR [ApprovalStatus] = '';

PRINT 'Updated existing users to Approved status';
GO

-- =====================================================
-- SECTION 6: CREATE Indexes for Performance
-- =====================================================

-- Index for approval status queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuthUsers_ApprovalStatus' AND object_id = OBJECT_ID('AuthUsers'))
BEGIN
    CREATE INDEX IX_AuthUsers_ApprovalStatus ON AuthUsers(ApprovalStatus);
    PRINT 'Created index on ApprovalStatus';
END
GO

-- Index for linked Bitbucket users
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuthUsers_LinkedBitbucketUserId' AND object_id = OBJECT_ID('AuthUsers'))
BEGIN
    CREATE INDEX IX_AuthUsers_LinkedBitbucketUserId ON AuthUsers(LinkedBitbucketUserId);
    PRINT 'Created index on LinkedBitbucketUserId';
END
GO

-- =====================================================
-- SECTION 7: CREATE Views for Easy Querying
-- =====================================================

-- Drop existing view if exists
IF OBJECT_ID('dbo.vw_PendingApprovals','V') IS NOT NULL DROP VIEW dbo.vw_PendingApprovals;
GO

-- Create view for pending approvals
CREATE VIEW vw_PendingApprovals AS
SELECT
    u.Id,
    u.Username,
    u.DisplayName,
    u.Email,
    u.Department,
    u.JobTitle,
    u.Team,
    u.RequestReason,
    u.RequestedAt,
    u.AuthProvider,
    u.AzureAdObjectId,
    DATEDIFF(HOUR, u.RequestedAt, GETUTCDATE()) as HoursPending
FROM AuthUsers u
WHERE u.ApprovalStatus = 'Pending'
AND u.IsActive = 1;
GO

PRINT 'Created vw_PendingApprovals view';
GO

-- =====================================================
-- SECTION 8: STORED PROCEDURES
-- =====================================================

-- Drop existing procedure if exists
IF OBJECT_ID('dbo.sp_ApproveUser','P') IS NOT NULL DROP PROCEDURE dbo.sp_ApproveUser;
GO

-- Stored procedure to approve user
CREATE PROCEDURE sp_ApproveUser
    @UserId INT,
    @ApprovedById INT,
    @RoleId INT = NULL,
    @LinkedBitbucketUserId INT = NULL,
    @Notes NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRANSACTION;

    BEGIN TRY
        -- Update user approval status
        UPDATE AuthUsers
        SET ApprovalStatus = 'Approved',
            ApprovedAt = GETUTCDATE(),
            ApprovedBy = @ApprovedById,
            LinkedBitbucketUserId = @LinkedBitbucketUserId,
            Notes = ISNULL(@Notes, Notes)
        WHERE Id = @UserId;

        -- Assign role if provided
        IF @RoleId IS NOT NULL
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM AuthUserRoles WHERE UserId = @UserId AND RoleId = @RoleId)
            BEGIN
                INSERT INTO AuthUserRoles (UserId, RoleId)
                VALUES (@UserId, @RoleId);
            END
        END

        COMMIT TRANSACTION;

        SELECT 'User approved successfully' as Message;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

PRINT 'Created sp_ApproveUser stored procedure';
GO

-- Drop existing procedure if exists
IF OBJECT_ID('dbo.sp_RejectUser','P') IS NOT NULL DROP PROCEDURE dbo.sp_RejectUser;
GO

-- Stored procedure to reject user
CREATE PROCEDURE sp_RejectUser
    @UserId INT,
    @RejectedById INT,
    @RejectionReason NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE AuthUsers
    SET ApprovalStatus = 'Rejected',
        RejectedAt = GETUTCDATE(),
        RejectedBy = @RejectedById,
        RejectionReason = @RejectionReason,
        IsActive = 0 -- Deactivate rejected users
    WHERE Id = @UserId;

    SELECT 'User rejected' as Message;
END
GO

PRINT 'Created sp_RejectUser stored procedure';
GO

-- =====================================================
-- SECTION 9: PRINT SUMMARY
-- =====================================================
PRINT '';
PRINT '===== USER APPROVAL MIGRATION COMPLETE =====';
PRINT 'Tables Modified:';
PRINT '  - AuthUsers (added approval workflow columns)';
PRINT '';
PRINT 'Tables Created:';
PRINT '  - NotificationConfig';
PRINT '  - NotificationState';
PRINT '';
PRINT 'Views Created:';
PRINT '  - vw_PendingApprovals';
PRINT '';
PRINT 'Stored Procedures Created:';
PRINT '  - sp_ApproveUser';
PRINT '  - sp_RejectUser';
PRINT '';
PRINT 'Default Configurations:';
PRINT '  - User approval notification config added';
PRINT '  - Existing users set to Approved status';
PRINT '=============================================';