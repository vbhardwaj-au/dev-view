-- Check approved users without roles
SELECT
    u.Id,
    u.Username,
    u.Email,
    u.ApprovalStatus,
    u.ApprovedAt,
    COUNT(ur.UserId) as RoleCount
FROM AuthUsers u
LEFT JOIN AuthUserRoles ur ON u.Id = ur.UserId
WHERE u.ApprovalStatus = 'Approved'
GROUP BY u.Id, u.Username, u.Email, u.ApprovalStatus, u.ApprovedAt
HAVING COUNT(ur.UserId) = 0;

-- Assign default 'User' role to approved users without any roles
INSERT INTO AuthUserRoles (UserId, RoleId)
SELECT
    u.Id,
    (SELECT Id FROM AuthRoles WHERE Name = 'User')
FROM AuthUsers u
WHERE u.ApprovalStatus = 'Approved'
    AND NOT EXISTS (
        SELECT 1 FROM AuthUserRoles ur WHERE ur.UserId = u.Id
    );

-- Verify the fix
SELECT
    u.Username,
    u.Email,
    u.ApprovalStatus,
    r.Name as RoleName
FROM AuthUsers u
LEFT JOIN AuthUserRoles ur ON u.Id = ur.UserId
LEFT JOIN AuthRoles r ON ur.RoleId = r.Id
WHERE u.Email LIKE '%vikasbhard%'
   OR u.Username LIKE '%vikasbhard%';