# DevView Project Roles

This document outlines the role and responsibility of each project within the DevView solution.

## Project Overview

DevView follows a clean architecture pattern with clear separation of concerns across multiple projects:

### 🔧 API Project
**Role**: RESTful Web API Service
**Responsibilities**:
- Provides HTTP endpoints for data access and analytics
- Serves as the primary interface between the Web UI and backend data
- Implements business logic for analytics, reporting, and data visualization
- Manages authentication and authorization (when implemented)
- Exposes Swagger documentation for API testing

**Key Components**:
- Analytics endpoints (commit activity, contributor stats, etc.)
- Sync endpoints for manual data synchronization
- Data validation and transformation
- CORS configuration for web client communication

### 🌐 Web Project
**Role**: User Interface (Blazor Server Application)
**Responsibilities**:
- Provides the interactive dashboard and user interface
- Renders analytics charts and visualizations
- Manages user sessions and real-time updates
- Implements responsive design for various devices
- Handles client-side interactions and form submissions

**Key Components**:
- Analytics dashboard with interactive charts
- Admin pages for data management
- Repository and user management interfaces
- Real-time data updates via SignalR (when applicable)

### 📊 Data Project
**Role**: Shared Data Layer
**Responsibilities**:
- Defines database entity models shared across projects
- Implements repository pattern for data access
- Provides consistent CRUD operations for all data entities
- Manages database connections and transactions
- Centralizes data access logic to avoid code duplication

**Key Components**:
- Database models (Commit, CommitFile, PullRequest, SyncSettings)
- Repository services (CommitRepository, PullRequestRepository)
- Base database service with common operations
- Data transfer objects and configuration models

### 🔌 Integration Project
**Role**: External API Integration Layer
**Responsibilities**:
- Handles all communication with Bitbucket Cloud API
- Implements API authentication and rate limiting
- Parses and transforms external data into internal models
- Manages API error handling and retry logic
- Provides abstraction layer for external service interactions

**Key Components**:
- Bitbucket API client implementations
- Data parsing and transformation services
- File classification and diff parsing utilities
- Repository, commit, and pull request synchronization

### ⏰ AutoSync Project
**Role**: Background Data Synchronization Service
**Responsibilities**:
- Performs scheduled or manual data synchronization from Bitbucket
- Implements various sync modes (Full, Delta, Selective)
- Manages batch processing for large data sets
- Logs sync operations and handles errors gracefully
- Can be deployed as a console app, Windows Service, or Docker container

**Key Components**:
- Main synchronization orchestration logic
- Configuration management for sync settings
- Batch processing with configurable time windows
- Sync progress tracking and logging

## Data Flow Relationships

```
Web ←→ API ←→ Data ←→ Database
              ↓
          Integration ←→ Bitbucket API
              ↑
          AutoSync
```

## Shared Dependencies

- **Data Project**: Used by both API and AutoSync for consistent data models and database operations
- **Integration Project**: Used by both API and AutoSync for Bitbucket API interactions
- **Database**: Accessed through the Data project's repository pattern

## Deployment Considerations

- **API + Web**: Typically deployed together on the same server/container for optimal performance
- **AutoSync**: Can be deployed separately as a background service with scheduled execution
- **Data + Integration**: Deployed as libraries within the consuming applications
- **Database**: Separate SQL Server instance accessible by all components

## Development Guidelines

1. **Data Project**: Changes to models affect both API and AutoSync - coordinate carefully
2. **Integration Project**: API changes should maintain backwards compatibility for both consumers
3. **API Project**: Focus on RESTful principles and proper HTTP status codes
4. **Web Project**: Ensure responsive design and accessibility standards
5. **AutoSync Project**: Implement robust error handling and resumable operations

---

*Last Updated: After Data project refactoring - consolidating database operations for code reuse between API and AutoSync*