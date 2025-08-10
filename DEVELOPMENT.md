# Development Guide

## Quick Start

### Option 1: Use the Startup Script (Recommended)
```bash
./start-dev.sh
```

This script will:
- Check if ports are available
- Start BB.Api on port 5000
- Wait for API to be ready
- Start BB.Web on port 5084
- Show you all the important URLs

### Option 2: Manual Startup

## Running the BB Solution

This solution consists of two projects that need to run simultaneously:

### 1. BB.Api (Backend API) - Port 5000
The API project provides data endpoints for the web application.

**To run:**
```bash
cd BB.Api
dotnet run
```

The API will be available at:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- Swagger UI: http://localhost:5000/swagger

### 2. BB.Web (Blazor Frontend) - Port 5084
The web application provides the dashboard UI.

**To run:**
```bash
cd BB.Web  
dotnet run
```

The web app will be available at:
- HTTP: http://localhost:5084
- HTTPS: https://localhost:7051

## Debugging Tools

### 1. API Test Page
Navigate to http://localhost:5084/api-test to:
- Test the connection between the web app and API
- See detailed error messages and response times
- View the actual HTTP client configuration
- Get direct links to test API endpoints

### 2. Dashboard Debug Info
The dashboard now shows debug information including:
- API base URL configuration
- Loading states
- Repository count and names
- Detailed error messages

### 3. Enhanced Logging
Both applications now have comprehensive logging. Check the browser console (F12) for detailed logs when using the web application.

## Important Notes

1. **CORS Configuration**: The API now includes CORS configuration to allow the web app to access it
2. **Start API First**: Always start the BB.Api project before the BB.Web project
3. **Database Required**: Make sure SQL Server is running and the database schema is created
4. **Sync Data First**: Before viewing the dashboard, sync some data using the API endpoints:
   - POST `/api/sync/repositories/{workspace}` - Sync repositories
   - POST `/api/sync/commits/{workspace}/{repoSlug}` - Sync commits

## Troubleshooting

### "No Repositories Found" or Blank Dropdown
1. Use the **API Test page** (http://localhost:5084/api-test) to diagnose the issue
2. Check the **Debug Information** section on the Dashboard
3. Ensure BB.Api is running on port 5000
4. Check that repositories have been synced via the API
5. Verify database connection and data

### API Connection Errors
1. Confirm BB.Api is running: http://localhost:5000/swagger
2. Check the `ApiBaseUrl` setting in BB.Web/appsettings.json
3. Ensure no firewall blocking port 5000
4. Check for CORS errors in browser console (F12)

### Common Issues
- **Port conflicts**: Use `lsof -i:5000` and `lsof -i:5084` to check if ports are in use
- **CORS errors**: Make sure both projects are running on the correct ports
- **Database connection**: Verify SQL Server is running and connection string is correct

## Development Workflow

1. Start BB.Api project (or use `./start-dev.sh`)
2. Use Swagger UI or Postman to sync some data
3. Start BB.Web project  
4. Navigate to the **API Test page** to verify connection
5. Navigate to the **Dashboard** to view analytics

## Configuration

### BB.Api Configuration (appsettings.json)
- Database connection string
- Bitbucket OAuth credentials
- CORS settings (configured for ports 5084 and 7051)

### BB.Web Configuration (appsettings.json)  
- `ApiBaseUrl`: Should point to BB.Api (http://localhost:5000)

## Log Files

When using the startup script, logs are written to:
- `api.log` - BB.Api application logs
- `web.log` - BB.Web application logs

Use `tail -f api.log` or `tail -f web.log` to monitor logs in real-time. 