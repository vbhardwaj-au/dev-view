# Quick Setup Guide

Welcome to the Bitbucket Analytics Dashboard! Follow these steps to get started quickly.

## 🚀 Quick Start (5 minutes)

### 1. Clone the Repository
```bash
git clone https://github.com/bhardwajvicky/bitbucket-analyser.git
cd bitbucket-analyser
```

### 2. Setup Configuration
```bash
./setup-config.sh
```
This script will:
- Create configuration files from templates
- Guide you through the setup process
- Make scripts executable

### 3. Configure Your Settings
Edit the created configuration files:

**BB.Api/appsettings.json** - Update these values:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=bb;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
  },
  "Bitbucket": {
    "ConsumerKey": "YOUR_BITBUCKET_CONSUMER_KEY",
    "ConsumerSecret": "YOUR_BITBUCKET_CONSUMER_SECRET"
  }
}
```

**BB.Web/appsettings.json** - Ready to use as-is (no sensitive data required)

### 4. Database Setup
1. Ensure SQL Server is running
2. Create database: `CREATE DATABASE bb;`
3. Run schema: Execute `BB.Api/SqlSchema/schema.sql`

### 5. Start the Application
```bash
./start-dev.sh
```

### 6. Access the Dashboard
- **📊 Dashboard**: http://localhost:5084/dashboard
- **🧪 API Test**: http://localhost:5084/api-test  
- **📖 API Docs**: http://localhost:5000/swagger

## 📋 Prerequisites
- .NET 9 SDK
- SQL Server (or SQL Server Express)
- Bitbucket OAuth credentials (for syncing data)

## 🔧 Getting Bitbucket Credentials
1. Go to [Bitbucket Cloud Settings](https://bitbucket.org/account/settings/app-passwords/)
2. Create a new OAuth consumer
3. Copy the Consumer Key and Consumer Secret
4. Add them to `BB.Api/appsettings.json`

## 📊 Sync Data
Before using the dashboard, sync some data:
1. Use Swagger UI: http://localhost:5000/swagger
2. Or use the API Test page: http://localhost:5084/api-test
3. Start with: `POST /api/sync/repositories/{workspace}`

## 🆘 Need Help?
- Check the [full README.md](README.md) for detailed documentation
- Visit the [troubleshooting section](README.md#-troubleshooting)
- Check the API Test page for connection issues

---
**Happy analyzing! 📈** 