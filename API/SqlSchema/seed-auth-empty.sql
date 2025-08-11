-- DevView Initial Setup Script (Empty - No Default Users)
-- This script creates the authentication structure without any default users
-- The first user will be created through the web setup page

-- Create Roles if they don't exist
IF NOT EXISTS (SELECT 1 FROM AuthRoles WHERE Name = 'Admin')
BEGIN
    INSERT INTO AuthRoles (Name, Description)
    VALUES ('Admin', 'Full system administrator access');
END

IF NOT EXISTS (SELECT 1 FROM AuthRoles WHERE Name = 'Manager')
BEGIN
    INSERT INTO AuthRoles (Name, Description)
    VALUES ('Manager', 'Elevated permissions for team management');
END

IF NOT EXISTS (SELECT 1 FROM AuthRoles WHERE Name = 'User')
BEGIN
    INSERT INTO AuthRoles (Name, Description)
    VALUES ('User', 'Standard user access');
END

PRINT 'Authentication roles created successfully.';
PRINT '';
PRINT '=================================================================';
PRINT 'IMPORTANT: First-Run Setup Required';
PRINT '=================================================================';
PRINT 'No default users have been created for security reasons.';
PRINT '';
PRINT 'To create your administrator account:';
PRINT '1. Start the application';
PRINT '2. Navigate to http://localhost:5084';
PRINT '3. You will be automatically redirected to the setup page';
PRINT '4. Create your administrator account with a secure password';
PRINT '';
PRINT 'The first user created will automatically be assigned the Admin role.';
PRINT '=================================================================';