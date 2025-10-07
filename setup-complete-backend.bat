@echo off
echo ========================================
echo    Hostr Backend Complete Setup
echo ========================================

REM Check if Docker is running
docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo ❌ Docker is not running!
    echo.
    echo Please:
    echo 1. Install Docker Desktop from: https://docs.docker.com/desktop/install/windows-install/
    echo 2. Start Docker Desktop
    echo 3. Wait for it to fully load
    echo 4. Run this script again
    pause
    exit /b 1
)

echo ✅ Docker is running
echo.

cd /d "%~dp0"

echo 🐳 Starting Docker services...
docker-compose -f infra/docker-compose.yml down --remove-orphans
docker-compose -f infra/docker-compose.yml up -d

echo ⏳ Waiting for services to be ready...
timeout /t 15 /nobreak >nul

echo 🔍 Checking service health...
docker-compose -f infra/docker-compose.yml ps

echo.
echo 🗄️ Testing database connection and extensions...
docker exec hostr-postgres psql -U postgres -d hostr -c "SELECT version();"
if %errorlevel% neq 0 (
    echo ❌ Database connection failed!
    echo Checking logs...
    docker logs hostr-postgres
    pause
    exit /b 1
)

docker exec hostr-postgres psql -U postgres -d hostr -c "SELECT extname FROM pg_extension WHERE extname IN ('vector', 'pg_trgm');"
if %errorlevel% neq 0 (
    echo ❌ Extensions not loaded properly!
    pause
    exit /b 1
)

echo ✅ Database and extensions are working!
echo.

echo 🏗️ Creating database schema...
docker exec -i hostr-postgres psql -U postgres -d hostr < apps/api/Data/Migrations/DropOldTables.sql
docker exec -i hostr-postgres psql -U postgres -d hostr < apps/api/Data/Migrations/CreateInitialSchema.sql
docker exec -i hostr-postgres psql -U postgres -d hostr < apps/api/Data/SeedData.sql

if %errorlevel% neq 0 (
    echo ❌ Schema creation failed!
    pause
    exit /b 1
)

echo ✅ Database schema created and seeded!
echo.

echo 🔧 Updating configuration...
REM Update appsettings.json to use Docker database
powershell -Command "(Get-Content 'apps/api/appsettings.json') -replace 'Host=localhost;Database=hostr;Username=postgres;Password=postgres', 'Host=localhost;Database=hostr;Username=postgres;Password=postgres' | Set-Content 'apps/api/appsettings.json'"

echo 📊 Verifying setup...
echo Testing tenant data...
docker exec hostr-postgres psql -U postgres -d hostr -c "SELECT slug, name, plan FROM \"Tenants\";"

echo Testing knowledge base with vectors...
docker exec hostr-postgres psql -U postgres -d hostr -c "SELECT id, source, array_length(embedding, 1) as vector_dimensions FROM \"KnowledgeBaseChunks\" LIMIT 2;"

echo.
echo 🎉 SETUP COMPLETE!
echo.
echo ================================
echo     Connection Details
echo ================================
echo 🗄️  PostgreSQL: localhost:5432
echo     Database: hostr
echo     Username: postgres  
echo     Password: postgres
echo.
echo 📝 Redis: localhost:6379
echo 📧 MailHog: http://localhost:8025
echo.
echo ================================
echo      Next Steps
echo ================================
echo 1. Start the API:
echo    cd apps/api
echo    dotnet run
echo.
echo 2. Start the Workers:
echo    cd apps/workers  
echo    dotnet run
echo.
echo 3. Test the API:
echo    http://localhost:5000/health
echo    http://localhost:5000/swagger
echo.
echo ================================
echo     Management Commands
echo ================================
echo 📊 View logs: docker-compose -f infra/docker-compose.yml logs
echo 🛑 Stop:     docker-compose -f infra/docker-compose.yml down
echo 🔄 Restart:  docker-compose -f infra/docker-compose.yml restart
echo.
pause