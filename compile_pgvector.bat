@echo off
echo Compiling and installing pgvector from source...

REM Check if running as administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo This script must be run as Administrator!
    echo Right-click and select "Run as administrator"
    pause
    exit /b 1
)

REM Setup Visual Studio Build Tools environment
echo Setting up build environment...
call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"

REM Create temp directory
if not exist C:\temp mkdir C:\temp
cd /d C:\temp

REM Download pgvector if not exists
if not exist pgvector (
    echo Downloading pgvector source...
    git clone https://github.com/pgvector/pgvector.git
)

cd pgvector

REM Set PostgreSQL root path
set PGROOT=C:\Program Files\PostgreSQL\17

echo Compiling pgvector...
nmake /F Makefile.win

if %errorlevel% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo Installing pgvector...
nmake /F Makefile.win install

if %errorlevel% neq 0 (
    echo Install failed!
    pause
    exit /b 1
)

echo Restarting PostgreSQL service...
net stop postgresql-x64-17
timeout /t 3 /nobreak >nul
net start postgresql-x64-17

echo.
echo pgvector has been successfully compiled and installed!
echo You can now use CREATE EXTENSION vector; in PostgreSQL
pause