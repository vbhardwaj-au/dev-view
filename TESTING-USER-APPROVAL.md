# User Approval System - Testing Guide

## Prerequisites

### 1. Database Setup
First, run the migration script to set up the database:

```sql
-- Run this script in your SQL Server database
-- File: API/SqlSchema/user-approval-migration.sql
```

### 2. Update Configuration
Add this to your `API/appsettings.json`:

```json
{
  "Authentication": {
    "AutoApproveNewUsers": false  // Set to false to test approval workflow
  }
}
```

## Testing Scenarios

### Scenario 1: Test Database User Approval Flow

#### Step 1: Create a Test User (Pending Status)
```sql
-- Create a test user with pending status
INSERT INTO AuthUsers
(Username, PasswordHash, PasswordSalt, DisplayName, Email,
 Department, JobTitle, AuthProvider, IsActive, CreatedOn,
 ApprovalStatus, RequestedAt)
VALUES
('testuser1', 0x00, 0x00, 'Test User One', 'testuser1@example.com',
 'Engineering', 'Developer', 'Database', 1, GETUTCDATE(),
 'Pending', GETUTCDATE());

-- Get the user ID for verification
SELECT * FROM AuthUsers WHERE Username = 'testuser1';
```

#### Step 2: Test Login Block
1. Start the application:
```bash
./start-dev.sh
```

2. Navigate to: http://localhost:5084/login
3. Try to login with `testuser1` (will fail with "Account pending approval")

#### Step 3: Test Admin Approval
```sql
-- First, ensure you have an admin user
UPDATE AuthUsers
SET ApprovalStatus = 'Approved'
WHERE Username = 'admin';

-- Verify admin has Admin role
INSERT INTO AuthUserRoles (UserId, RoleId)
SELECT u.Id, r.Id
FROM AuthUsers u, AuthRoles r
WHERE u.Username = 'admin' AND r.Name = 'Admin'
AND NOT EXISTS (
    SELECT 1 FROM AuthUserRoles ur
    WHERE ur.UserId = u.Id AND ur.RoleId = r.Id
);
```

4. Login as admin
5. Navigate to: http://localhost:5084/admin/user-approvals
6. You should see "Test User One" in pending list
7. Click "Review" and approve the user

### Scenario 2: Test Azure AD User Approval Flow

#### Step 1: Enable Azure AD (Optional)
If you have Azure AD configured:
```json
{
  "AzureAd": {
    "Enabled": true,
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id"
  }
}
```

#### Step 2: Simulate Azure AD User Creation
```sql
-- Simulate an Azure AD user creation with pending status
INSERT INTO AuthUsers
(Username, PasswordHash, PasswordSalt, DisplayName, Email,
 Department, JobTitle, AuthProvider, AzureAdObjectId, IsActive,
 CreatedOn, ApprovalStatus, RequestedAt)
VALUES
('azureuser@company.com', 0x00, 0x00, 'Azure Test User',
 'azureuser@company.com', 'IT', 'Manager', 'AzureAd',
 'azure-obj-id-123', 1, GETUTCDATE(), 'Pending', GETUTCDATE());
```

### Scenario 3: Test Notification System

#### Step 1: Check Notification Count
```sql
-- Check pending users count (should match notification)
SELECT COUNT(*) FROM AuthUsers WHERE ApprovalStatus = 'Pending';

-- Verify notification configuration exists
SELECT * FROM NotificationConfig WHERE MenuItemKey = 'user-approvals';
```

#### Step 2: Test via API
```bash
# Get notification count (need JWT token)
curl -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  http://localhost:5000/api/notifications/user-approvals

# Get all notifications
curl -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  http://localhost:5000/api/notifications/all
```

### Scenario 4: Test Rejection Flow

#### Step 1: Create Another Test User
```sql
INSERT INTO AuthUsers
(Username, PasswordHash, PasswordSalt, DisplayName, Email,
 AuthProvider, IsActive, CreatedOn, ApprovalStatus, RequestedAt,
 RequestReason, Team)
VALUES
('testuser2', 0x00, 0x00, 'Test User Two', 'testuser2@example.com',
 'Database', 1, GETUTCDATE(), 'Pending', GETUTCDATE(),
 'Need access for project X', 'Platform Team');
```

#### Step 2: Test Rejection
1. Login as admin
2. Go to user approvals page
3. Review "Test User Two"
4. Click "Reject" and provide a reason
5. Verify user cannot login

### Scenario 5: Test Request Access Page

#### Step 1: Create Pending User with Password
```sql
-- Create user with actual password for testing
DECLARE @salt VARBINARY(32) = CRYPT_GEN_RANDOM(32);
DECLARE @password NVARCHAR(100) = 'TestPass123!';
DECLARE @combined VARBINARY(MAX) = CAST(@password AS VARBINARY(MAX)) + @salt;
DECLARE @hash VARBINARY(64) = HASHBYTES('SHA2_512', @combined);

INSERT INTO AuthUsers
(Username, PasswordHash, PasswordSalt, DisplayName, Email,
 AuthProvider, IsActive, CreatedOn, ApprovalStatus)
VALUES
('pendinguser', @hash, @salt, 'Pending Test User', 'pending@example.com',
 'Database', 1, GETUTCDATE(), 'Pending');
```

#### Step 2: Test Access Request Flow
1. Try to login with `pendinguser` / `TestPass123!`
2. You should be redirected to `/request-access` or `/pending-approval`
3. Fill in the request form
4. Submit and verify the request is recorded

## API Testing with Postman/curl

### 1. Get Pending Approvals (Admin Only)
```bash
GET http://localhost:5000/api/approvals/pending
Authorization: Bearer {admin_jwt_token}
```

### 2. Approve User
```bash
POST http://localhost:5000/api/approvals/approve/{userId}
Authorization: Bearer {admin_jwt_token}
Content-Type: application/json

{
  "roleName": "User",
  "linkedBitbucketUserId": null,
  "notes": "Approved for project access"
}
```

### 3. Reject User
```bash
POST http://localhost:5000/api/approvals/reject/{userId}
Authorization: Bearer {admin_jwt_token}
Content-Type: application/json

{
  "rejectionReason": "Invalid department"
}
```

### 4. Check My Status
```bash
GET http://localhost:5000/api/approvals/my-status
Authorization: Bearer {user_jwt_token}
```

## Verification Queries

### Check System Status
```sql
-- 1. View all pending users
SELECT Id, Username, DisplayName, Email, Department,
       ApprovalStatus, RequestedAt, RequestReason, Team
FROM AuthUsers
WHERE ApprovalStatus = 'Pending'
ORDER BY RequestedAt;

-- 2. View approval statistics
SELECT
    COUNT(CASE WHEN ApprovalStatus = 'Pending' THEN 1 END) as Pending,
    COUNT(CASE WHEN ApprovalStatus = 'Approved' THEN 1 END) as Approved,
    COUNT(CASE WHEN ApprovalStatus = 'Rejected' THEN 1 END) as Rejected
FROM AuthUsers;

-- 3. View recent approvals/rejections
SELECT u.Username, u.ApprovalStatus, u.ApprovedAt, u.RejectedAt,
       a.Username as ApprovedBy, r.Username as RejectedBy,
       u.RejectionReason
FROM AuthUsers u
LEFT JOIN AuthUsers a ON u.ApprovedBy = a.Id
LEFT JOIN AuthUsers r ON u.RejectedBy = r.Id
WHERE u.ApprovedAt > DATEADD(day, -7, GETUTCDATE())
   OR u.RejectedAt > DATEADD(day, -7, GETUTCDATE())
ORDER BY COALESCE(u.ApprovedAt, u.RejectedAt) DESC;

-- 4. Check notification state
SELECT * FROM NotificationConfig;
SELECT * FROM NotificationState;
```

## Troubleshooting

### Issue: Users can still login despite pending status
**Solution**: Check that approval status check is in both authentication paths:
- Database authentication (line 61-83 in AuthenticationService.cs)
- Azure AD authentication (line 251-274 in AuthenticationService.cs)

### Issue: Admin can't see pending users
**Solution**: Verify admin role:
```sql
SELECT u.Username, r.Name as RoleName
FROM AuthUsers u
JOIN AuthUserRoles ur ON u.Id = ur.UserId
JOIN AuthRoles r ON ur.RoleId = r.Id
WHERE r.Name = 'Admin';
```

### Issue: Notifications not showing
**Solution**: Check notification config:
```sql
-- Ensure config exists and is active
SELECT * FROM NotificationConfig
WHERE MenuItemKey = 'user-approvals' AND IsActive = 1;

-- If missing, insert it:
INSERT INTO NotificationConfig
(MenuItemKey, DisplayName, QueryType, Query, RefreshIntervalSeconds,
 DisplayType, PulseOnNew, MinimumRole, IsActive)
VALUES
('user-approvals', 'Pending User Approvals', 'SQL',
 'SELECT COUNT(*) FROM AuthUsers WHERE ApprovalStatus = ''Pending''',
 60, 'Badge', 1, 'Admin', 1);
```

## Reset Test Data
```sql
-- Clean up test users
DELETE FROM AuthUsers
WHERE Username IN ('testuser1', 'testuser2', 'pendinguser', 'azureuser@company.com');

-- Reset all users to approved (careful in production!)
UPDATE AuthUsers
SET ApprovalStatus = 'Approved',
    ApprovedAt = GETUTCDATE()
WHERE ApprovalStatus = 'Pending';
```

## Expected Behaviors

✅ **Pending users cannot login** - They see "Account pending approval" message
✅ **Rejected users cannot login** - They see "Account access denied" message
✅ **Admin sees notification badge** - Count of pending approvals
✅ **Approval workflow** - Admin can approve/reject with reasons
✅ **Role assignment** - Users get appropriate roles on approval
✅ **Audit trail** - System tracks who approved/rejected and when
✅ **Auto-redirect** - Approved users go to home, pending to request page

## Next Steps After Testing

Once verified, you can:
1. Set `AutoApproveNewUsers` to `true` in production if needed
2. Customize the approval notification refresh interval
3. Add email notifications (future enhancement)
4. Implement bulk approval features