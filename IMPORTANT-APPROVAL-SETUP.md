# User Approval System - Setup & Testing Guide

## Critical Setup for Your Testing Scenario

Your test plan is perfect, but we need one configuration update to handle the Azure AD approval flow properly.

### For Azure AD Users (Your Test Case)

The system will:
1. ✅ Create new AD user with "Pending" status on first login
2. ✅ Block access and return `RequiresApproval = true`
3. ⚠️ **Need to handle redirect to request page**

### Required Configuration Update

Add this to your `Web/appsettings.json`:

```json
{
  "Authentication": {
    "AutoApproveNewUsers": false,  // Ensures new users are pending
    "RequireApprovalForNewUsers": true
  }
}
```

### Manual Redirect Solution (Temporary)

After Azure AD login fails due to pending approval, manually navigate to:
- `/request-access` - To submit additional details
- `/pending-approval` - To check status

### Database Check

After first AD login attempt, verify user was created as pending:

```sql
SELECT Id, Username, Email, ApprovalStatus, RequestedAt, AuthProvider
FROM AuthUsers
WHERE Email = 'your-ad-email@company.com'
```

## Your Complete Test Flow

### 1. First AD Login Attempt
- Login with your AD account
- System creates user with "Pending" status
- **Current behavior**: Login will fail with "Account pending approval"
- **Manual action**: Navigate to `/request-access`

### 2. Submit Access Request
- Go to: `http://localhost:5084/request-access`
- Your AD details will be shown
- Add optional team/reason
- Submit request

### 3. Check Pending Status
- Try to login again OR
- Go to: `http://localhost:5084/pending-approval`
- See your pending request details

### 4. Admin Approval
- Login as admin
- Go to: `http://localhost:5084/admin/user-approvals`
- You'll see the pending AD user
- Can link to Bitbucket user if similar
- Approve with role assignment

### 5. Test Approved Access
- Login with your AD account again
- Should work normally now

## Quick SQL to Help Testing

### Create an Admin User (if needed)
```sql
-- Ensure you have an admin user
UPDATE AuthUsers
SET ApprovalStatus = 'Approved'
WHERE Username = 'admin';

-- Give admin role
INSERT INTO AuthUserRoles (UserId, RoleId)
SELECT u.Id, r.Id
FROM AuthUsers u, AuthRoles r
WHERE u.Username = 'admin'
  AND r.Name = 'Admin'
  AND NOT EXISTS (
    SELECT 1 FROM AuthUserRoles ur
    WHERE ur.UserId = u.Id AND ur.RoleId = r.Id
  );
```

### Monitor Your Test
```sql
-- Watch for your AD user creation
SELECT * FROM AuthUsers
WHERE AuthProvider = 'AzureAd'
ORDER BY CreatedOn DESC;

-- Check pending users
SELECT Id, Username, Email, ApprovalStatus, RequestedAt, RequestReason
FROM AuthUsers
WHERE ApprovalStatus = 'Pending';
```

## Expected Behaviors

1. **First AD Login**: Creates pending user, blocks access
2. **Request Page**: Shows your AD info (email, name, department)
3. **Pending Page**: Shows when request was submitted
4. **Admin Page**: Shows pending user with AD badge
5. **After Approval**: Normal access granted

## Known Limitation

Currently, after Azure AD authentication fails due to pending status, you need to manually navigate to `/request-access`.

A full solution would require updating the Azure AD callback handler to auto-redirect to the request page, but your manual navigation approach will work perfectly for testing!

## Troubleshooting

If AD user isn't created as pending:
```sql
-- Force user to pending for testing
UPDATE AuthUsers
SET ApprovalStatus = 'Pending',
    RequestedAt = GETUTCDATE()
WHERE Email = 'your-email@company.com';
```

If you can't see the admin page:
```sql
-- Check your admin user has correct role
SELECT u.Username, r.Name as Role
FROM AuthUsers u
JOIN AuthUserRoles ur ON u.Id = ur.UserId
JOIN AuthRoles r ON ur.RoleId = r.Id
WHERE u.Username = 'admin';
```