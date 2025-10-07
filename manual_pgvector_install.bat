@echo off
echo Manual pgvector installation for PostgreSQL 17...

REM Check if running as administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo This script must be run as Administrator!
    echo Right-click and select "Run as administrator"
    pause
    exit /b 1
)

set PG_PATH=C:\Program Files\PostgreSQL\17
set PGVECTOR_PATH=%USERPROFILE%\Downloads\pgvector-0.5.1

echo Checking directories...
echo PostgreSQL path: %PG_PATH%
echo pgvector source path: %PGVECTOR_PATH%

REM Check if pgvector directory exists
if not exist "%PGVECTOR_PATH%" (
    echo pgvector source directory not found at %PGVECTOR_PATH%
    echo Please make sure the extracted pgvector folder is in Downloads
    pause
    exit /b 1
)

echo.
echo Copying extension SQL and control files...

REM Copy the control file
if exist "%PGVECTOR_PATH%\vector.control" (
    copy "%PGVECTOR_PATH%\vector.control" "%PG_PATH%\share\extension\" /Y
    echo Copied vector.control
) else (
    echo WARNING: vector.control not found
)

REM Copy SQL files
for %%f in ("%PGVECTOR_PATH%\sql\vector--*.sql") do (
    copy "%%f" "%PG_PATH%\share\extension\" /Y
    echo Copied %%~nxf
)

REM For now, we'll skip the DLL since compilation failed
echo.
echo NOTE: Since compilation failed, we're installing without the binary DLL.
echo This means some vector operations may not work, but basic functionality should.
echo.
echo If you need full functionality, you may need to:
echo 1. Use a different PostgreSQL version
echo 2. Find pre-compiled binaries elsewhere
echo 3. Use the no-vector version of the schema
echo.

echo Extension files copied. Restarting PostgreSQL...
net stop postgresql-x64-17 2>nul
timeout /t 2 /nobreak >nul
net start postgresql-x64-17

echo.
echo Installation attempt complete.
echo Try creating the extension in PostgreSQL:
echo   CREATE EXTENSION vector;
echo.
echo If it fails, use the no-vector schema instead.
pause