@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem =====================================================================
rem  Store Platform Capstone - one-time local setup (Windows)
rem  Run from the repository root:   setup.cmd
rem  Then start everything with:     docker compose up --build
rem =====================================================================

cd /d "%~dp0"

echo.
echo === Store Platform setup ===
echo.

rem ---- 1. .NET SDK ------------------------------------------------------
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] The .NET SDK was not found on PATH.
    echo         Install the .NET 10 SDK from https://dotnet.microsoft.com/download
    exit /b 1
)
for /f "usebackq delims=" %%v in (`dotnet --version`) do set "DOTNET_VERSION=%%v"
echo [OK]   .NET SDK !DOTNET_VERSION!

rem ---- 2. Docker --------------------------------------------------------
where docker >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Docker was not found on PATH.
    echo         Install Docker Desktop from https://www.docker.com/products/docker-desktop
    exit /b 1
)
docker info >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Docker is installed but the engine is not running.
    echo         Start Docker Desktop and run setup.cmd again.
    exit /b 1
)
docker compose version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] "docker compose" ^(Compose v2^) is not available.
    echo         Update Docker Desktop to a version that includes Compose v2.
    exit /b 1
)
echo [OK]   Docker engine and Compose v2 are available

rem ---- 3. .env ----------------------------------------------------------
if exist ".env" (
    echo [OK]   .env already exists - leaving it unchanged
) else (
    if not exist ".env.example" (
        echo [ERROR] .env.example is missing. Re-clone the repository.
        exit /b 1
    )
    copy /y ".env.example" ".env" >nul
    echo [OK]   Created .env from .env.example ^(local development values^)
)

rem ---- 4. Restore and build the solution -------------------------------
echo.
echo Restoring and building CruzDusty_StorePlatformCapstone.slnx ...
dotnet build CruzDusty_StorePlatformCapstone.slnx --nologo -v minimal
if errorlevel 1 (
    echo [ERROR] dotnet build failed. Fix the errors above and run setup.cmd again.
    exit /b 1
)
echo [OK]   Solution builds

rem ---- 5. Validate docker-compose.yml (once it exists) -----------------
if exist "docker-compose.yml" (
    docker compose config --quiet
    if errorlevel 1 (
        echo [ERROR] docker-compose.yml is invalid. See the message above.
        exit /b 1
    )
    echo [OK]   docker-compose.yml is valid
) else (
    echo [INFO] docker-compose.yml is not in the repository yet - skipping compose validation
)

echo.
echo Setup complete. Next step:
echo     docker compose up --build
echo.
endlocal
exit /b 0
