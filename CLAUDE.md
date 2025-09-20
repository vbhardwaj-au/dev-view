# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Tech Stack
- **.NET 9** - ASP.NET Core Web API and Blazor Server
- **SQL Server** - Database with Dapper ORM
- **Bitbucket Cloud API** - External integration for data syncing
- **Chart.js** - Frontend charting library (v3.9+)
- **Radzen Blazor** - UI component library

## Project Structure
The solution follows a clean architecture pattern with 6 main projects:
- **API** - RESTful API service (port 5000)
- **Web** - Blazor Server UI (port 5084)
- **Data** - Shared data layer with models and repositories
- **Integration** - Bitbucket API client layer (.NET 8/9)
- **AutoSync** - Background sync service
- **Entities** - Shared DTOs between projects

## Development Commands

### Build and Run
```bash
# Quick start both API and Web
./start-dev.sh

# Individual projects
cd API && dotnet run
cd Web && dotnet run
cd AutoSync && dotnet run

# Run with specific environment
ASPNETCORE_ENVIRONMENT=Development dotnet run --project API
ASPNETCORE_ENVIRONMENT=Development dotnet run --project Web

# Build solution
dotnet build

# Run tests
dotnet test

# Clean solution
dotnet clean
```

### Database Setup
```sql
-- Create database
CREATE DATABASE DevView;

-- Apply schema (in order)
-- 1. API/SqlSchema/schema.sql
-- 2. API/SqlSchema/alter-auth.sql
-- 3. API/SqlSchema/seed-auth.sql
-- 4. API/SqlSchema/migrations-azure-ad.sql (for Azure AD)
```

### Publishing
```bash
# Publish API
cd API && dotnet publish -c Release -o ./publish

# Publish Web
cd Web && dotnet publish -c Release -o ./publish

# Publish AutoSync
cd AutoSync && dotnet publish -c Release -o ./publish
```

## Architecture Overview

### API Layer (API project)
- **Port**: 5000 (HTTP), 5001 (HTTPS)
- **Authentication**: JWT Bearer tokens with optional Azure AD integration
- **Endpoints**: Analytics, Commits, PullRequests, Sync, Teams, Auth, UserDashboard
- **Services**: AnalyticsService, CommitAnalysisService, PullRequestAnalysisService, AuthenticationService
- **Swagger**: Available at http://localhost:5000/swagger
- **CORS**: Configured for ports 5084 and 7051

### Web Layer (Web project)
- **Port**: 5084 (HTTP), 7051 (HTTPS)
- **Framework**: Blazor Server with InteractiveServer render mode
- **Authentication**: JWT-based with cookie auth for server-side, Azure AD support
- **Key Pages**: Dashboard, UserDashboard, Commits, PullRequests, TopCommitters, PrDashboard, Teams, Login
- **Services**: AuthService, HybridAuthService, WorkspaceService, BitbucketUrlService
- **JavaScript**: Chart integration (wwwroot/js/), auth helpers

### Data Layer (Data project)
- **ORM**: Dapper with SQL Server
- **Models**: Commit, CommitFile, PullRequest, SyncSettings, Team, TeamMember, AuthUser
- **Repositories**: CommitRepository, PullRequestRepository
- **Shared between**: API and AutoSync projects
- **Command timeout**: 5 minutes for long-running operations

### Integration Layer
- **Purpose**: Bitbucket Cloud API client
- **Services**: BitbucketCommitsService, BitbucketPullRequestsService, BitbucketRepositoriesService, BitbucketUsersService
- **Utilities**: DiffParserService, FileClassificationService
- **Note**: Targets .NET 8 while other projects use .NET 9

## Key Configuration Files

### API/appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=DevView;..."
  },
  "Bitbucket": {
    "ConsumerKey": "YOUR_KEY",
    "ConsumerSecret": "YOUR_SECRET"
  },
  "Jwt": {
    "Key": "32-character-minimum-secret-key",
    "Issuer": "devview-api",
    "Audience": "devview-api",
    "ExpirationDays": 30
  },
  "AzureAd": {
    "Enabled": false,
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET"
  },
  "Authentication": {
    "DefaultProvider": "Database",
    "AllowFallback": true,
    "AutoCreateUsers": true
  }
}
```

### Web/appsettings.json
- ApiBaseUrl (default: http://localhost:5000)
- AzureAd configuration (matching API settings)

### AutoSync/appsettings.json
- Same database and Bitbucket settings as API
- SyncSettings for mode (Full/Delta) and targets

## Database Schema
Main tables:
- **AuthUsers** - Authentication users with roles (Admin, Manager, User)
- **Users** - Bitbucket users with avatar support
- **Repositories** - Repository metadata with ExcludeFromReporting flag
- **Commits** - Commit data with lines added/removed
- **CommitFiles** - Individual file changes per commit
- **PullRequests** - PR metadata and metrics
- **Teams** - Team organization
- **TeamMembers** - Team membership

## Authentication Flow

### Database Authentication (Default)
1. User logs in via /login page with username/password
2. API validates credentials against AuthUsers table
3. JWT token generated and stored in localStorage
4. BearerHandler adds token to API requests
5. Server-side cookie auth for Blazor components

### Azure AD Authentication (Optional)
1. User clicks "Sign in with Microsoft" on /login
2. Redirects to Azure AD for authentication
3. Returns with OIDC token
4. User auto-created in AuthUsers table if new
5. JWT token generated for API access

### JWT Token Management
- Token stored in localStorage as 'jwtToken'
- Automatic token refresh before expiration
- Token added to API requests via Authorization header
- Expiration: 30 days (configurable in appsettings.json)

## Sync Modes
- **Full Sync**: Historical data in configurable batches (default 10 days)
- **Delta Sync**: Recent changes only (configurable days)
- **Manual Sync**: Via API endpoints or AutoSync console
- **Selective Sync**: Choose what to sync (users, repositories, commits, PRs)

## Role-Based Access Control
- **Admin**: Full system access, user management, all settings, can modify file classifications
- **Manager**: Team management, elevated analytics access
- **User**: Standard access to dashboards and personal analytics, read-only file classification

## Timezone Handling
- Default timezone: "AUS Eastern Standard Time"
- All date conversions handled at database level
- Inclusive end dates (includes full day up to 23:59:59)
- Configurable via Application:ReportingTimezone setting

## Important Notes
- Original project name was "BB", renamed to "DevView" - some references remain
- Integration project uses .NET 8 while others use .NET 9
- Build warnings policy: 0 warnings tolerated
- Default admin login: username `admin`, password `Admin#12345!`
- JWT tokens expire after 30 days
- CORS configured for specific ports - update if changing ports
- File classification is automatic with admin-only modification rights

## Testing & Quality Checks

### Run Tests
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test API.Tests/API.Tests.csproj

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Code Quality
```bash
# Check for build warnings
dotnet build --warnaserror

# Format code
dotnet format

# Restore packages
dotnet restore
```

## Common Development Scenarios

### Working with Authentication
- Check current user auth: `AuthService.IsAuthenticated()` in Web project
- Get user claims: `AuthService.GetUserClaims()` returns username, userId, role
- Admin-only features: Check `role == "Admin"` in authorization logic
- Azure AD testing: Set `AzureAd:Enabled` to `true` in appsettings.json

### Debugging Sync Issues
- Check AutoSync logs in console output
- Verify Bitbucket credentials in API/appsettings.json
- Test individual sync: Use API endpoints `/api/sync/*`
- Rate limiting: AutoSync respects Bitbucket API limits automatically

### Adding New Features
- API endpoints: Add to `API/Endpoints/` folder using minimal API pattern
- Blazor pages: Create in `Web/Components/Pages/` with `@page` directive
- Data models: Add to `Data/Models/` and create corresponding repository
- Services: Place in appropriate project's `Services/` folder