@echo off
echo Installing pgvector pre-compiled binaries for PostgreSQL 17...

REM Check if running as administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo This script must be run as Administrator!
    echo Right-click and select "Run as administrator"
    pause
    exit /b 1
)

set PG_PATH=C:\Program Files\PostgreSQL\17
set TEMP_DIR=%TEMP%\pgvector_install

echo Creating temporary directory...
if exist "%TEMP_DIR%" rmdir /s /q "%TEMP_DIR%"
mkdir "%TEMP_DIR%"
cd /d "%TEMP_DIR%"

echo Downloading pgvector pre-compiled binaries...
powershell -Command "& {[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri 'https://github.com/pgvector/pgvector/releases/download/v0.5.1/pgvector-v0.5.1-postgresql-17-windows-x64.zip' -OutFile 'pgvector.zip'}"

if not exist pgvector.zip (
    echo Failed to download pgvector. Please check your internet connection.
    echo You can manually download from: https://github.com/pgvector/pgvector/releases
    pause
    exit /b 1
)

echo Extracting files...
powershell -Command "Expand-Archive -Path 'pgvector.zip' -DestinationPath '.' -Force"

REM Find the extracted directory (it might have a different name)
for /d %%i in (*pgvector*) do set EXTRACT_DIR=%%i

if not defined EXTRACT_DIR (
    echo Could not find extracted pgvector directory
    dir
    pause
    exit /b 1
)

echo Installing pgvector files...

REM Copy DLL to lib directory
if exist "%EXTRACT_DIR%\lib\pgvector.dll" (
    copy "%EXTRACT_DIR%\lib\pgvector.dll" "%PG_PATH%\lib\" /Y
    echo Copied pgvector.dll
) else (
    echo WARNING: pgvector.dll not found in extracted files
    dir "%EXTRACT_DIR%\lib\"
)

REM Copy extension files
if exist "%EXTRACT_DIR%\share\extension" (
    xcopy "%EXTRACT_DIR%\share\extension\*.*" "%PG_PATH%\share\extension\" /Y /E
    echo Copied extension files
) else (
    echo WARNING: Extension files not found
    dir "%EXTRACT_DIR%\share\"
)

echo Cleaning up temporary files...
cd /d %TEMP%
rmdir /s /q "%TEMP_DIR%"

echo Restarting PostgreSQL service...
net stop postgresql-x64-17 2>nul
timeout /t 2 /nobreak >nul
net start postgresql-x64-17

echo.
echo pgvector installation complete!
echo.
echo Test the installation by running in PostgreSQL:
echo   CREATE EXTENSION vector;
echo   SELECT extname, extversion FROM pg_extension WHERE extname = 'vector';
echo.
pause