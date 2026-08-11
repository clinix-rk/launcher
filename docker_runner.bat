@echo off
setlocal enableextensions enabledelayedexpansion

:: ============================================================================
:: Docker Desktop Launch & Compose Wrapper with Web Check
:: Description: Ensures Docker Desktop is running on Windows, executes
::              'docker compose up -d', waits for port 80, and opens browser.
:: ============================================================================

:: 1. Check if Docker Desktop is already running
docker info >nul 2>&1
if %errorlevel% equ 0 (
    echo [INFO] Docker daemon is already running.
    goto RUN_COMPOSE
)

:: 2. Launch Docker Desktop if not running
echo [INFO] Starting Docker Desktop...
start "" "C:\Program Files\Docker\Docker\Docker Desktop.exe"

:: 3. Wait for the Docker daemon to become responsive
echo [INFO] Waiting for Docker daemon to initialize...
:WAIT_LOOP
timeout /t 3 /nobreak >nul
docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Still waiting for Docker daemon...
    goto WAIT_LOOP
)

echo [INFO] Docker daemon is ready.

:: 4. Run docker compose up -d
:RUN_COMPOSE
echo [INFO] Executing docker compose up -d...
docker compose up -d %*

if %errorlevel% neq 0 (
    echo [ERROR] Docker compose failed to execute.
    exit /b %errorlevel%
)

:: 5. Wait for localhost:80 to become available
echo [INFO] Waiting for http://localhost:80 to respond...
:HTTP_WAIT_LOOP
timeout /t 2 /nobreak >nul
curl -s -o nul -w "%%{http_code}" http://localhost:80 | findstr /R "^[2345]" >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Waiting for web service on port 80...
    goto HTTP_WAIT_LOOP
)

:: 6. Open default browser to localhost:80
echo [INFO] Web service ready. Opening browser...
start http://localhost:80

exit /b 0