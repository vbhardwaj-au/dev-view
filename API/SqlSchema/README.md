# Database Schema Management

This directory contains SQL scripts for managing the DevView database schema.

## Files

- **schema.sql** - Complete database schema (DDL) for creating a new database from scratch
- **seed.sql** - Sample data for development/testing
- **seed-auth-empty.sql** - Creates default admin user for initial setup
- **migrations/** - Incremental migration scripts for updating existing databases

## Creating a New Database

1. Create the database:
```sql
CREATE DATABASE [dev-v-db];
```

2. Run the schema script:
```bash
sqlcmd -S server -d dev-v-db -U username -P password -i schema.sql
```

3. (Optional) Seed initial data:
```bash
sqlcmd -S server -d dev-v-db -U username -P password -i seed-auth-empty.sql
```

## Updating an Existing Database

Run migration scripts in order:
```bash
sqlcmd -S server -d dev-v-db -U username -P password -i migrations/001_add_auth_role_columns.sql
```

## Migration History

| Script | Date | Description |
|--------|------|-------------|
| 001_add_auth_role_columns.sql | 2025-01-11 | Adds Description and CreatedOn columns to AuthRoles table |