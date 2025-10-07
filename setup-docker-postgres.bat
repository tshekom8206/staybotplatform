@echo off
echo Setting up PostgreSQL with pgvector using Docker...

REM Check if Docker is running
docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo Docker is not running or not installed.
    echo Please install Docker Desktop and make sure it's running.
    echo Download from: https://www.docker.com/products/docker-desktop
    pause
    exit /b 1
)

cd /d "%~dp0"

echo Starting PostgreSQL with pgvector and Redis...
docker-compose -f infra/docker-compose-with-postgres.yml up -d

echo Waiting for PostgreSQL to be ready...
timeout /t 10 /nobreak >nul

echo Testing database connection...
docker exec hostr-postgres psql -U postgres -d hostr -c "CREATE EXTENSION IF NOT EXISTS vector;"
docker exec hostr-postgres psql -U postgres -d hostr -c "CREATE EXTENSION IF NOT EXISTS pg_trgm;"

if %errorlevel% equ 0 (
    echo PostgreSQL with pgvector is ready!
    echo.
    echo Connection details:
    echo   Host: localhost
    echo   Port: 5432
    echo   Database: hostr
    echo   Username: postgres
    echo   Password: postgres
    echo.
    echo Update your appsettings.json to use:
    echo   "Host=localhost;Database=hostr;Username=postgres;Password=postgres"
    echo.
    echo You can now run the full schema:
    echo   psql -h localhost -U postgres -d hostr -f apps/api/Data/Migrations/DropOldTables.sql
    echo   psql -h localhost -U postgres -d hostr -f apps/api/Data/Migrations/CreateInitialSchema.sql
    echo   psql -h localhost -U postgres -d hostr -f apps/api/Data/SeedData.sql
) else (
    echo Failed to set up pgvector. Using fallback...
    echo You can use the no-vector schema instead.
)

echo.
echo Services started:
echo   PostgreSQL: localhost:5432
echo   Redis: localhost:6379
echo   MailHog: localhost:8025
echo.
pause