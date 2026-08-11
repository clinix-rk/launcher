@echo off
setlocal enableextensions enabledelayedexpansion

:: ============================================================================
:: Docker Desktop Launch & Compose Wrapper
:: ============================================================================

:: 1. Launch Docker Desktop via native CLI tool
echo [INFO] Requesting Docker Desktop to start...
call docker desktop start >nul 2>&1

:: Fallback check if 'docker desktop' CLI plugin is not available
if %errorlevel% neq 0 (
    echo [WARN] 'docker desktop' CLI plugin not found. Falling back to default executable path...
    start "" "C:\Program Files\Docker\Docker\Docker Desktop.exe"
)

:: 2. Wait for Docker Engine to start
echo [INFO] Waiting for Docker engine to initialize...
:WAIT_FOR_ENGINE
timeout /t 3 /nobreak >nul
docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Docker engine is not ready yet. Retrying...
    goto WAIT_FOR_ENGINE
)

echo [INFO] Docker engine is up and responsive.

:: 3. Execute docker compose up -d
echo [INFO] Executing docker compose up -d...
docker compose up -d %*

if %errorlevel% neq 0 (
    echo [ERROR] Docker compose failed.
    exit /b %errorlevel%
)

:: 4. Poll localhost:80
echo [INFO] Waiting for http://localhost:80...
:HTTP_WAIT_LOOP
timeout /t 2 /nobreak >nul
curl -s -o nul -w "%%{http_code}" http://localhost:80 | findstr /R "^[2345]" >nul 2>&1
if %errorlevel% neq 0 (
    goto HTTP_WAIT_LOOP
)

:: 5. Open Browser
echo [INFO] Web service ready. Opening browser...
start http://localhost:80

exit /b 0