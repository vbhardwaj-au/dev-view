# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Tech Stack
- **.NET 9** - ASP.NET Core Web API and Blazor Server
- **SQL Server** - Database with Dapper ORM
- **Bitbucket Cloud API** - External integration for data syncing
- **Chart.js** - Frontend charting library
- **Radzen Blazor** - UI component library

## Project Structure
The solution follows a clean architecture pattern with 5 main projects:
- **API** - RESTful API service (port 5000)
- **Web** - Blazor Server UI (port 5084) 
- **Data** - Shared data layer with models and repositories
- **Integration** - Bitbucket API client layer
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

# Build solution
dotnet build

# Run tests
dotnet test
```

### Database
```sql
-- Create database
CREATE DATABASE bb;

-- Apply schema
-- Run API/SqlSchema/schema.sql
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
- **Port**: 5000
- **Authentication**: JWT Bearer tokens (optional)
- **Endpoints**: Analytics, Commits, PullRequests, Sync, Teams, Auth
- **Services**: AnalyticsService, CommitAnalysisService, PullRequestAnalysisService
- **Swagger**: Available at http://localhost:5000/swagger

### Web Layer (Web project)
- **Port**: 5084
- **Framework**: Blazor Server with InteractiveServer render mode
- **Authentication**: JWT-based with cookie auth for server-side
- **Key Pages**: Dashboard, UserDashboard, Commits, PullRequests, TopCommitters
- **Services**: AuthService, WorkspaceService, BitbucketUrlService

### Data Layer (Data project)
- **ORM**: Dapper with SQL Server
- **Models**: Commit, CommitFile, PullRequest, SyncSettings, Team, TeamMember
- **Repositories**: CommitRepository, PullRequestRepository
- **Shared between**: API and AutoSync projects

### Integration Layer
- **Purpose**: Bitbucket Cloud API client
- **Services**: BitbucketCommitsService, BitbucketPullRequestsService, BitbucketRepositoriesService, BitbucketUsersService
- **Utilities**: DiffParserService, FileClassificationService

## Key Configuration Files

### API/appsettings.json
- Database connection string
- Bitbucket OAuth credentials (ConsumerKey, ConsumerSecret)
- JWT settings (optional)

### Web/appsettings.json
- ApiBaseUrl (default: http://localhost:5000)

### AutoSync/appsettings.json
- Same database and Bitbucket settings as API
- Sync configuration settings

## Database Schema
Main tables:
- **Users** - Bitbucket users with avatar support
- **Repositories** - Repository metadata with ExcludeFromReporting flag
- **Commits** - Commit data with lines added/removed
- **CommitFiles** - Individual file changes per commit
- **PullRequests** - PR metadata and metrics
- **Teams** - Team organization
- **TeamMembers** - Team membership

## Authentication Flow
1. User logs in via /login page
2. API validates credentials against Users table
3. JWT token generated and stored in localStorage
4. BearerHandler adds token to API requests
5. Server-side cookie auth for Blazor components

## Sync Modes
- **Full Sync**: Historical data in 10-day batches
- **Delta Sync**: Recent changes only
- **Manual Sync**: Via API endpoints or AutoSync console

## Important Notes
- The solution was renamed from "BB" to "DevView" but some references to "BB" may remain in configuration
- The startup script references outdated ports (5005 instead of 5000) - use dotnet run directly
- Integration project targets .NET 8 while others use .NET 9
- CORS is configured for ports 5084 and 7051
- Default command timeout is 5 minutes for long-running operations