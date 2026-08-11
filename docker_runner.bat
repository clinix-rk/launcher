@echo off
setlocal enableextensions enabledelayedexpansion

:: ============================================================================
:: Docker Desktop Launch & Compose Wrapper with Web Check
:: Description: Opens Docker Desktop, waits for the engine to initialize,
::              executes 'docker compose up -d', waits for port 80, and opens browser.
:: ============================================================================

:: 1. Launch Docker Desktop unconditionally
echo [INFO] Opening Docker Desktop...
start "" "C:\Program Files\Docker\Docker\Docker Desktop.exe"

:: 2. Wait for the Docker Engine to respond to CLI calls
echo [INFO] Waiting for Docker engine to start properly...
:WAIT_FOR_ENGINE
timeout /t 3 /nobreak >nul
docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Docker engine is not ready yet. Retrying...
    goto WAIT_FOR_ENGINE
)

echo [INFO] Docker engine is up and responsive.

:: 3. Run docker compose up -d
:RUN_COMPOSE
echo [INFO] Executing docker compose up -d...
docker compose up -d %*

if %errorlevel% neq 0 (
    echo [ERROR] Docker compose failed to execute.
    exit /b %errorlevel%
)

:: 4. Wait for http://localhost:80 to return a valid HTTP response code
echo [INFO] Waiting for http://localhost:80 to respond...
:HTTP_WAIT_LOOP
timeout /t 2 /nobreak >nul
curl -s -o nul -w "%%{http_code}" http://localhost:80 | findstr /R "^[2345]" >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Waiting for web service on port 80...
    goto HTTP_WAIT_LOOP
)

:: 5. Open default browser to localhost:80
echo [INFO] Web service ready. Opening browser...
start http://localhost:80

exit /b 0