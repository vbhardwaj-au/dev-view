-- Seed data for DevView Database
-- This file contains all initial data needed for the application

SET NOCOUNT ON;

-- ============================================
-- 1. Create Authentication Roles
-- ============================================

-- Insert Admin role
IF NOT EXISTS (SELECT 1 FROM AuthRoles WHERE Name = 'Admin')
BEGIN
    INSERT INTO AuthRoles (Name, Description, CreatedOn)
    VALUES ('Admin', 'Administrator with full system access', GETUTCDATE());
    PRINT 'Admin role created';
END

-- Insert User role
IF NOT EXISTS (SELECT 1 FROM AuthRoles WHERE Name = 'User')
BEGIN
    INSERT INTO AuthRoles (Name, Description, CreatedOn)
    VALUES ('User', 'Standard user with basic access', GETUTCDATE());
    PRINT 'User role created';
END

-- Insert Manager role
IF NOT EXISTS (SELECT 1 FROM AuthRoles WHERE Name = 'Manager')
BEGIN
    INSERT INTO AuthRoles (Name, Description, CreatedOn)
    VALUES ('Manager', 'Manager role with elevated permissions', GETUTCDATE());
    PRINT 'Manager role created';
END

-- ============================================
-- 2. Create Default Users
-- ============================================

-- Create Admin user (password: Admin#12345!)
DECLARE @adminUsername NVARCHAR(100) = 'admin';
DECLARE @adminDisplayName NVARCHAR(255) = 'Administrator';
DECLARE @adminPasswordHash VARBINARY(64) = 0x741049FF46E6513B110098FBE6776E13D4436DE5F85C18AEB444C55CE35664546B09EFDD7F4107EBF98106D78B31D25C94E9DAE3BB33F3AD79740BD9A5BB1FB8;
DECLARE @adminPasswordSalt VARBINARY(32) = 0x348A52BEAB2C24068BA9B36DFE35EE77C96200473696C1F5962E2D8C0AECC893;

IF NOT EXISTS (SELECT 1 FROM AuthUsers WHERE Username = @adminUsername)
BEGIN
    INSERT INTO AuthUsers (Username, PasswordHash, PasswordSalt, DisplayName, IsActive, CreatedOn)
    VALUES (@adminUsername, @adminPasswordHash, @adminPasswordSalt, @adminDisplayName, 1, GETUTCDATE());
    
    -- Assign Admin role
    DECLARE @adminUserId INT = (SELECT Id FROM AuthUsers WHERE Username = @adminUsername);
    DECLARE @adminRoleId INT = (SELECT Id FROM AuthRoles WHERE Name = 'Admin');
    
    IF @adminRoleId IS NOT NULL AND @adminUserId IS NOT NULL
    BEGIN
        INSERT INTO AuthUserRoles (UserId, RoleId)
        VALUES (@adminUserId, @adminRoleId);
        PRINT 'Admin user created: username=admin, password=Admin#12345!';
    END
END
ELSE
BEGIN
    PRINT 'Admin user already exists';
END

-- Create User (password: User#12345!)
DECLARE @userUsername NVARCHAR(100) = 'user';
DECLARE @userDisplayName NVARCHAR(255) = 'Normal User';
DECLARE @userPasswordHash VARBINARY(64) = 0xCFBAC15009697576DEE7488079615BB1F2FB3495B1154A2024B4F355B73934D14658CCD12D0D7E14A979E8F4107E227CD368F6B3D07839BD426D5A67E043E06A;
DECLARE @userPasswordSalt VARBINARY(32) = 0x9C12ECD1B0AAC7F7D665EA02E4F8DA21EB3375AEFDFB9E374EF8C579E03DDE3A;

IF NOT EXISTS (SELECT 1 FROM AuthUsers WHERE Username = @userUsername)
BEGIN
    INSERT INTO AuthUsers (Username, PasswordHash, PasswordSalt, DisplayName, IsActive, CreatedOn)
    VALUES (@userUsername, @userPasswordHash, @userPasswordSalt, @userDisplayName, 1, GETUTCDATE());
    
    -- Assign User role
    DECLARE @userId INT = (SELECT Id FROM AuthUsers WHERE Username = @userUsername);
    DECLARE @userRoleId INT = (SELECT Id FROM AuthRoles WHERE Name = 'User');
    
    IF @userRoleId IS NOT NULL AND @userId IS NOT NULL
    BEGIN
        INSERT INTO AuthUserRoles (UserId, RoleId)
        VALUES (@userId, @userRoleId);
        PRINT 'User created: username=user, password=User#12345!';
    END
END
ELSE
BEGIN
    PRINT 'User already exists';
END

-- Create Manager user (password: Manager#12345!)
DECLARE @managerUsername NVARCHAR(100) = 'manager';
DECLARE @managerDisplayName NVARCHAR(255) = 'Manager User';
DECLARE @managerPasswordHash VARBINARY(64) = 0x77509DA05D979D0870DB13FEDBA1420D5BB7548CB656A8B332F8AF436DB770D0BB6A7E5FA9CBA060E7F9E3B25A625644D3FE921F3D77937082C7029C7CE55E78;
DECLARE @managerPasswordSalt VARBINARY(32) = 0x499D1EF29379573A8A2C6A2FEC7B105D2C52CD8E99B4CBF78E35A65C4AACE843;

IF NOT EXISTS (SELECT 1 FROM AuthUsers WHERE Username = @managerUsername)
BEGIN
    INSERT INTO AuthUsers (Username, PasswordHash, PasswordSalt, DisplayName, IsActive, CreatedOn)
    VALUES (@managerUsername, @managerPasswordHash, @managerPasswordSalt, @managerDisplayName, 1, GETUTCDATE());
    
    -- Assign Manager role
    DECLARE @managerUserId INT = (SELECT Id FROM AuthUsers WHERE Username = @managerUsername);
    DECLARE @managerRoleId INT = (SELECT Id FROM AuthRoles WHERE Name = 'Manager');
    
    IF @managerRoleId IS NOT NULL AND @managerUserId IS NOT NULL
    BEGIN
        INSERT INTO AuthUserRoles (UserId, RoleId)
        VALUES (@managerUserId, @managerRoleId);
        PRINT 'Manager user created: username=manager, password=Manager#12345!';
    END
END
ELSE
BEGIN
    PRINT 'Manager user already exists';
END

-- ============================================
-- 3. Display Created Users and Roles
-- ============================================

PRINT '';
PRINT 'Current Roles:';
SELECT Id, Name, Description FROM AuthRoles ORDER BY Name;

PRINT '';
PRINT 'Current Users:';
SELECT u.Id, u.Username, u.DisplayName, u.IsActive, 
       STRING_AGG(r.Name, ', ') AS Roles
FROM AuthUsers u
LEFT JOIN AuthUserRoles ur ON u.Id = ur.UserId
LEFT JOIN AuthRoles r ON ur.RoleId = r.Id
GROUP BY u.Id, u.Username, u.DisplayName, u.IsActive
ORDER BY u.Username;

PRINT '';
PRINT '============================================';
PRINT 'Seed data script completed successfully';
PRINT 'Default Login Credentials:';
PRINT '  Admin:   username=admin,   password=Admin#12345!';
PRINT '  User:    username=user,    password=User#12345!';
PRINT '  Manager: username=manager, password=Manager#12345!';
PRINT '============================================';