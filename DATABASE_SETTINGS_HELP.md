# DevView Database Settings - Comprehensive Help Guide

## Overview
The Database Settings page (accessible via Admin → Settings → Database Settings tab) allows administrators to configure all system-wide settings that control DevView's behavior, authentication, file classification, and sync operations. All settings are stored in the database and can be modified without requiring application restarts.

## Navigation
Access the Database Settings page:
1. Log in as an Admin user
2. Navigate to **Admin** menu
3. Click on **Settings**
4. Select the **Database Settings** tab

## Setting Categories

The Database Settings page organizes configuration options into the following categories:

### 1. JWT Settings
Controls JSON Web Token authentication for the API.

| Setting | Type | Default | Impact | Valid Values |
|---------|------|---------|--------|--------------|
| **Issuer** | String | `devview-api` | Identifies who issued the JWT token | Any string identifying your organization |
| **Audience** | String | `devview-api` | Identifies intended recipients of the token | Typically same as issuer |
| **Key** | String | (generated) | Secret key for signing tokens | Minimum 32 characters, base64 encoded |
| **ExpirationDays** | Number | `30` | How long tokens remain valid | 1-365 (recommend 7-30 for security) |

⚠️ **Critical**: Changing the JWT Key will invalidate all existing user sessions, forcing all users to log in again.

### 2. AzureAd Settings
Configures Azure Active Directory integration for enterprise authentication.

| Setting | Type | Default | Impact | Valid Values |
|---------|------|---------|--------|--------------|
| **Enabled** | Boolean | `false` | Enables/disables Azure AD login | true/false |
| **Instance** | String | `https://login.microsoftonline.com/` | Azure AD instance URL | Microsoft login URL |
| **TenantId** | String | (empty) | Your Azure AD tenant identifier | GUID from Azure Portal |
| **ClientId** | String | (empty) | Application (client) ID | GUID from Azure App Registration |
| **ClientSecret** | String | (empty) | Application secret | Secret from Azure App Registration |
| **CallbackPath** | String | `/signin-oidc` | OAuth callback endpoint | Must match Azure App Registration |

📝 **Setup Guide**:
1. Register an app in Azure Portal
2. Add redirect URI: `https://your-domain/signin-oidc`
3. Create a client secret
4. Copy TenantId, ClientId, and ClientSecret to these settings
5. Set Enabled to `true`

### 3. Authentication Settings
Controls authentication behavior and policies.

| Setting | Type | Default | Impact | Valid Values |
|---------|------|---------|--------|--------------|
| **DefaultProvider** | String | `Database` | Primary authentication method | `Database`, `AzureAd` |
| **AllowFallback** | Boolean | `true` | Allow database login if Azure AD fails | true/false |
| **AutoCreateUsers** | Boolean | `true` | Auto-create user accounts on first Azure AD login | true/false |
| **SessionTimeoutMinutes** | Number | `1440` | Session duration (24 hours default) | 30-43200 (30 min to 30 days) |
| **RequireTwoFactor** | Boolean | `false` | Enforce 2FA for all users | true/false |

💡 **Best Practices**:
- Set `AllowFallback` to `false` in production for strict Azure AD-only access
- Reduce `SessionTimeoutMinutes` for high-security environments
- Enable `RequireTwoFactor` for admin accounts

### 4. Application Settings
General application configuration.

| Setting | Type | Default | Impact | Valid Values |
|---------|------|---------|--------|--------------|
| **ReportingTimezone** | String | `AUS Eastern Standard Time` | Timezone for all reports and analytics | Any valid Windows timezone ID |

📍 **Common Timezones**:
- `UTC` - Coordinated Universal Time
- `Eastern Standard Time` - US East Coast
- `Pacific Standard Time` - US West Coast
- `GMT Standard Time` - London
- `Central European Standard Time` - Paris/Berlin
- `India Standard Time` - Mumbai/Delhi
- `China Standard Time` - Beijing/Shanghai

### 5. AutoSync Settings
Controls automatic data synchronization behavior.

| Setting | Type | Default | Impact | Valid Values |
|---------|------|---------|--------|--------------|
| **AutoSyncBatchDays** | Number | `30` | Days of data to sync per batch | 1-90 (smaller = faster, larger = fewer API calls) |
| **PollingInterval** | Number | `3600` | Seconds between sync checks | 300-86400 (5 min to 24 hours) |
| **MaxRetries** | Number | `3` | Retry attempts for failed syncs | 0-10 |
| **RetryDelaySeconds** | Number | `60` | Delay between retry attempts | 10-600 |
| **SyncSettings** | JSON | See below | Detailed sync configuration | Valid JSON object |

**SyncSettings JSON Structure**:
```json
{
  "Mode": "Delta",              // "Full" or "Delta"
  "DeltaSyncDays": 30,         // Days to look back for delta sync
  "SyncTargets": {
    "Users": true,              // Sync user data
    "Repositories": true,       // Sync repository metadata
    "Commits": true,           // Sync commit history
    "PullRequests": true       // Sync PR data
  },
  "Overwrite": false           // Whether to overwrite existing data
}
```

### 6. FileClassification Settings
The most complex and impactful setting - controls how files are categorized in analytics.

**Structure**: A single JSON configuration with categories and rules.

#### File Categories

**1. Data Files** (`dataFiles`)
- **Purpose**: Identify data, database, and structured content files
- **Extensions**: `.csv`, `.tsv`, `.json`, `.xml`, `.sql`, `.db`, `.sqlite`
- **Paths**: `/data/`, `/database/`, `/datasets/`, `/logs/`
- **Patterns**: `export_*`, `dump_*`, `backup_*`, `*.data.*`
- **Impact**: These files are counted as "Data" in commit analytics

**2. Configuration Files** (`configFiles`)
- **Purpose**: Application and system configuration
- **Extensions**: `.yaml`, `.yml`, `.toml`, `.ini`, `.env`, `.properties`, `.config`
- **Specific Files**: `package.json`, `Dockerfile`, `docker-compose.yml`, `.gitignore`
- **Paths**: `/config/`, `/.github/`, `/settings/`
- **Impact**: Counted as "Config" in analytics, tracked separately from code

**3. Documentation Files** (`documentationFiles`)
- **Purpose**: Documentation and text content
- **Extensions**: `.md`, `.rst`, `.txt`, `.adoc`, `.tex`
- **Specific Files**: `README.md`, `CHANGELOG.md`, `LICENSE`, `AUTHORS`
- **Paths**: `/docs/`, `/documentation/`, `/wiki/`
- **Impact**: Not counted in code metrics, shown separately

**4. Code Files** (`codeFiles`)
- **Purpose**: Actual source code
- **Extensions**: `.cs`, `.py`, `.js`, `.ts`, `.java`, `.go`, `.cpp`, `.c`, `.rb`, `.php`, `.swift`, `.kt`, `.rs`, `.scala`, `.r`, `.m`, `.h`, `.vue`, `.jsx`, `.tsx`
- **Paths**: `/src/`, `/source/`, `/lib/`, `/app/`, `/components/`
- **Impact**: Primary metric for development activity

**5. Test Files** (`testFiles`)
- **Purpose**: Unit tests, integration tests, test fixtures
- **Extensions**: `.test.js`, `.spec.ts`, `.test.cs`
- **Paths**: `/test/`, `/tests/`, `/__tests__/`, `/spec/`
- **Patterns**: `*Test.cs`, `test_*.py`, `*_test.go`, `*.spec.*`
- **Impact**: Tracked separately to measure test coverage trends

#### Classification Rules

**Priority Order** (first match wins):
1. **Specific Files** - Exact filename matches (e.g., `Dockerfile`)
2. **Path Patterns** - Directory patterns (e.g., `/tests/`)
3. **File Name Patterns** - Wildcards in filenames (e.g., `*.test.js`)
4. **Extensions** - File extensions (e.g., `.py`)
5. **Default** - Anything not matched becomes `other`

**Configuration Options**:
- `caseSensitive`: `false` - Case-insensitive matching (recommended)
- `defaultType`: `"other"` - Category for unmatched files

## How to Edit Settings

### Via the UI

1. **Navigate to the setting category** using the category buttons
2. **Click the Edit button** (pencil icon) next to the setting
3. **Modify the value**:
   - For Boolean: Select true/false from dropdown
   - For Number: Enter numeric value
   - For String: Type new value
   - For JSON: Edit in the textarea (ensure valid JSON)
4. **Click Save** (checkmark icon) to apply
5. **Click Cancel** (X icon) to discard changes

### Value Type Guidelines

**Boolean Values**:
- Must be exactly `true` or `false` (lowercase)
- No quotes needed

**Number Values**:
- Integer or decimal numbers
- No quotes or commas
- Example: `30`, `3600`, `1.5`

**String Values**:
- Plain text without quotes in the UI
- Special characters allowed
- Example: `Eastern Standard Time`

**JSON Values**:
- Must be valid JSON syntax
- Use double quotes for strings
- Validate before saving
- Example:
```json
{
  "key": "value",
  "number": 123,
  "boolean": true,
  "array": ["item1", "item2"]
}
```

## Impact of Changes

### Immediate Effect (No Restart Required)
- All authentication settings
- Timezone changes
- File classification rules
- Sync settings

### Requires Re-login
- JWT Key changes
- JWT Expiration changes
- Authentication provider switches

### Affects Future Operations Only
- AutoSync settings (next sync cycle)
- File classification (new commits only)
- Session timeout (new sessions only)

## Best Practices

### Security
1. **JWT Key**: Generate a strong, unique key (use online generator for base64 strings)
2. **Session Timeout**: Balance security with user convenience
3. **Azure AD**: Prefer enterprise authentication in corporate environments
4. **Fallback Auth**: Disable in production for strict access control

### File Classification
1. **Review Regularly**: Update patterns as your codebase evolves
2. **Test Patterns**: Check a sample of files to ensure correct classification
3. **Language-Specific**: Add extensions for new languages/frameworks
4. **Project Structure**: Align path patterns with your repository structure

### Sync Configuration
1. **Batch Size**: Smaller batches (7-14 days) for initial sync, larger (30+ days) for ongoing
2. **Polling Interval**: Hourly (3600s) for active development, daily (86400s) for stable projects
3. **Delta Sync**: Use for regular updates, Full sync monthly for data integrity

## Troubleshooting

### Common Issues

**"Invalid JSON" Error**:
- Validate JSON using online validator
- Check for missing commas, quotes, or brackets
- Ensure all strings use double quotes

**Authentication Not Working**:
- Verify Azure AD settings if enabled
- Check JWT Key hasn't been accidentally changed
- Confirm DefaultProvider matches your setup

**Files Classified Incorrectly**:
1. Check classification priority order
2. Verify patterns aren't too broad
3. Test with specific file paths
4. Consider case sensitivity

**Sync Not Running**:
- Check PollingInterval isn't too large
- Verify SyncSettings.SyncTargets are enabled
- Review AutoSync logs for errors

### Getting Help

For additional assistance:
- Check application logs in Admin → Logs
- Review sync status in Admin → Sync Status
- Contact your system administrator
- Refer to API documentation at `/swagger`

## Examples

### Example 1: Configure for US East Coast Team
```json
Application.ReportingTimezone: "Eastern Standard Time"
JWT.ExpirationDays: 7
Authentication.SessionTimeoutMinutes: 480
```

### Example 2: Add Python Data Science File Types
Update FileClassification.Config to include:
```json
"dataFiles": {
  "extensions": [".csv", ".pkl", ".parquet", ".feather", ".h5", ".npy", ".npz"],
  "paths": ["/data/", "/notebooks/", "/datasets/"],
  "patterns": ["*.model", "*.weights"]
}
```

### Example 3: Enable Azure AD with Fallback
```
AzureAd.Enabled: true
AzureAd.TenantId: "your-tenant-guid"
AzureAd.ClientId: "your-app-guid"
Authentication.DefaultProvider: "AzureAd"
Authentication.AllowFallback: true
```

## Summary

The Database Settings page provides comprehensive control over DevView's behavior. Changes are stored in the database and most take effect immediately without restart. Always test configuration changes in a development environment first, especially authentication and file classification settings that can significantly impact system behavior and analytics accuracy.

Remember: With great configuration power comes great responsibility. Document your changes and maintain backups of working configurations.