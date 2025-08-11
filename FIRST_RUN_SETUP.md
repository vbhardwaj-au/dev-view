# DevView First-Run Setup Guide

## 🚀 Overview

DevView now includes a secure first-run setup system that eliminates the need for hardcoded passwords or manual SQL scripts. When you start the application for the first time with an empty database, you'll be guided through creating your administrator account.

## 📋 Prerequisites

1. **Database Setup**: Ensure your SQL Server database is created and the connection string is configured in `API/appsettings.json`
2. **Run Schema Scripts**: Execute the following SQL scripts in order:
   - `API/SqlSchema/schema.sql` - Creates all tables
   - `API/SqlSchema/alter-auth.sql` - Adds authentication columns (if upgrading)
   - `API/SqlSchema/seed-auth-empty.sql` - Creates roles without default users

## 🎯 Setup Process

### Step 1: Start the Application

```bash
# Start both API and Web services
./start-dev.sh

# Or manually:
# Terminal 1
cd API && dotnet run

# Terminal 2
cd Web && dotnet run
```

### Step 2: Navigate to the Application

Open your browser and go to: http://localhost:5084

### Step 3: Automatic Redirect to Setup

If no users exist in the database, you'll be automatically redirected to the setup page at `/setup`.

### Step 4: Create Administrator Account

On the setup page, you'll see a beautiful form to create your first user:

1. **Username**: Choose a unique username for the admin account
2. **Display Name** (Optional): Your full name or display name
3. **Password**: Create a strong password that meets these requirements:
   - ✅ At least 8 characters
   - ✅ One uppercase letter
   - ✅ One lowercase letter
   - ✅ One number
   - ✅ One special character
4. **Confirm Password**: Re-enter your password

The password requirements will be shown with real-time validation indicators.

### Step 5: Complete Setup

Click "Create Administrator Account" to:
- Create your admin user in the database
- Automatically assign the Admin role
- Log you in automatically
- Redirect to the main dashboard

## 🔒 Security Features

- **No Default Passwords**: Each installation has unique credentials
- **Strong Password Policy**: Enforced password requirements
- **One-Time Setup**: Setup page only works when no users exist
- **Automatic Admin Role**: First user automatically gets full admin privileges
- **JWT Authentication**: Secure token-based authentication

## 🛠️ Troubleshooting

### "Setup Already Complete" Message

If you see this message, it means users already exist in the database. To reset:

```sql
-- Clear existing users (WARNING: This will delete all users!)
DELETE FROM AuthUserRoles;
DELETE FROM AuthUsers;
```

### Cannot Access Setup Page

The setup page is only accessible when:
1. No users exist in the `AuthUsers` table
2. The application can connect to the database

Check your:
- Database connection string in `API/appsettings.json`
- SQL Server is running and accessible
- Database has been created

### Build Errors

If you encounter build errors:

```bash
# Clean and rebuild
dotnet clean
dotnet build

# Restore packages
dotnet restore
```

## 📝 After Setup

Once your admin account is created, you can:

1. **Create Additional Users**: Go to Admin → User Management
2. **Assign Roles**: Admin, Manager, or User roles
3. **Configure Workspaces**: Set up your Bitbucket workspace
4. **Start Syncing**: Begin importing your repository data

## 🔑 Role Descriptions

- **Admin**: Full system access, user management, all settings
- **Manager**: Team management, elevated analytics access
- **User**: Standard access to dashboards and personal analytics

## 🚨 Important Security Notes

1. **Choose a Strong Password**: Your admin account has full system access
2. **Keep Credentials Secure**: Never share or commit passwords
3. **Regular Updates**: Change passwords periodically
4. **Audit Access**: Regularly review user accounts and permissions

## 📞 Need Help?

If you encounter issues during setup:

1. Check the application logs in the console
2. Verify database connectivity
3. Ensure all required SQL scripts have been run
4. Review the error messages on the setup page

---

**Remember**: This setup process ensures your DevView installation is secure from the start, with no default or hardcoded credentials that could be exploited.