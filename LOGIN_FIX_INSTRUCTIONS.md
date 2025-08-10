# Login Fix Instructions

## Problem Identified
The login was failing because the password hashes in the database were created using SQL Server's `HASHBYTES` function, which encodes strings differently than the C# `AuthController` expects. The mismatch was in:
1. String encoding (SQL uses Unicode/UTF-16LE vs C# expecting UTF-8)
2. The exact byte representation when converting NVARCHAR to VARBINARY

## Solution
A proper SQL script has been generated with the correct password hashes that match the C# authentication logic.

## Steps to Fix

1. **Execute the SQL Script**
   Run the following SQL script on your database:
   ```
   API/SqlSchema/fix-users.sql
   ```
   
   This script will:
   - Delete existing 'user' and 'admin' accounts
   - Create new accounts with properly hashed passwords
   - Assign correct roles (User and Admin)

2. **Login Credentials**
   After running the script, you can login with:
   - **User Account**: 
     - Username: `user`
     - Password: `User#12345!`
   - **Admin Account**: 
     - Username: `admin`  
     - Password: `Admin#12345!`

3. **Access the Application**
   - Navigate to: http://localhost:5084/login
   - Enter the credentials above
   - The login should now work correctly

## Technical Details

### The Issue
- SQL's `CONVERT(VARBINARY(MAX), @password)` uses UTF-16LE encoding for NVARCHAR
- C# AuthController expects UTF-8 encoded password bytes
- This encoding mismatch caused authentication failures

### The Fix
- Generated proper hashes using C# code that matches the AuthController logic
- Used `Encoding.UTF8.GetBytes(password)` + salt
- Computed SHA512 hash of the combined bytes
- Created SQL script with the pre-computed correct hashes

### Files Created
- `/HashGenerator/Program.cs` - C# utility to generate correct hashes
- `/API/SqlSchema/fix-users.sql` - SQL script with correct user data
- `/API/Utils/PasswordHasher.cs` - Reusable password hashing utility

## Verification
You can verify the fix works by:
1. Running the SQL script
2. Testing login at http://localhost:5084/login
3. Or using the debug endpoint: `GET /api/auth/debug/user`

The login system should now function correctly with the provided credentials.