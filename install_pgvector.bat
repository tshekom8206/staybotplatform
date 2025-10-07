@echo off
echo Installing pgvector for PostgreSQL 17...

REM Check if running as administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo This script must be run as Administrator!
    echo Right-click and select "Run as administrator"
    pause
    exit /b 1
)

set PG_PATH=C:\Program Files\PostgreSQL\17
set PGVECTOR_PATH=C:\Program Files\PostgreSQL\17\pgvector

echo Copying pgvector files...

REM Copy DLL files to lib directory
if exist "%PGVECTOR_PATH%\lib\pgvector.dll" (
    copy "%PGVECTOR_PATH%\lib\pgvector.dll" "%PG_PATH%\lib\" /Y
    echo Copied pgvector.dll to lib directory
) else (
    echo WARNING: pgvector.dll not found in %PGVECTOR_PATH%\lib\
)

REM Copy extension control and SQL files to share\extension
if exist "%PGVECTOR_PATH%\share\extension" (
    xcopy "%PGVECTOR_PATH%\share\extension\*.*" "%PG_PATH%\share\extension\" /Y /S
    echo Copied extension files to share\extension directory
) else (
    echo WARNING: Extension files not found in %PGVECTOR_PATH%\share\extension\
)

echo.
echo Restarting PostgreSQL service...
net stop postgresql-x64-17
timeout /t 2 /nobreak >nul
net start postgresql-x64-17

echo.
echo pgvector installation complete!
echo You can now use the vector extension in PostgreSQL.
pause