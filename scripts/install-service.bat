@echo off
setlocal enabledelayedexpansion
title BoostModeScheduler - Service Manager

:: Auto-elevate to admin
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting admin privileges...
    timeout /t 1 >nul
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

:: Locate service exe (same dir first, then dev structure)
if exist "%~dp0BoostModeService.exe" (
    set "SVC_EXE=%~dp0BoostModeService.exe"
    set "BASE_DIR=%~dp0"
) else if exist "%~dp0..\publish\service\BoostModeService.exe" (
    set "SVC_EXE=%~dp0..\publish\service\BoostModeService.exe"
    set "BASE_DIR=%~dp0.."
) else (
    echo [ERROR] BoostModeService.exe not found
    echo Please make sure the exe file is in the same folder as this script
    pause
    exit /b 1
)

:: Locate config.json
if exist "%~dp0config.json" (
    set "CFG_SRC=%~dp0config.json"
) else if exist "%~dp0..\config.json" (
    set "CFG_SRC=%~dp0..\config.json"
)

set "SVC_NAME=BoostModeSvc"
set "CFG_DIR=%ProgramData%\BoostModeSvc"

echo ============================================
echo   BoostModeScheduler - Service Manager
echo ============================================
echo.
echo Select action:
echo   1. Install and start service
echo   2. Stop and uninstall service
echo   3. Restart service
echo   4. View service status
echo.
set /p CHOICE="Enter choice (1-4): "

if "%CHOICE%"=="1" goto INSTALL
if "%CHOICE%"=="2" goto UNINSTALL
if "%CHOICE%"=="3" goto RESTART
if "%CHOICE%"=="4" goto STATUS
echo Invalid choice
pause
exit /b 1

:INSTALL
echo [1/4] Creating config directory...
mkdir "%CFG_DIR%" 2>nul

echo [2/4] Copying default config...
if defined CFG_SRC (
    copy /y "%CFG_SRC%" "%CFG_DIR%\config.json" >nul 2>&1
) else (
    echo   - Skipped (config.json not found)
)

echo [3/4] Installing service...
sc create %SVC_NAME% binPath="\"%SVC_EXE%\"" start=auto >nul 2>&1
if %errorlevel% equ 0 (
    echo   - Service created
) else (
    sc config %SVC_NAME% binPath="\"%SVC_EXE%\"" start=auto >nul 2>&1
    echo   - Service updated
)

echo [4/4] Starting service...
sc start %SVC_NAME% >nul 2>&1
if %errorlevel% equ 0 (
    echo   - Service started
) else (
    echo   - Service already running
)

echo.
echo Install complete!
echo.
echo Config tool: "%BASE_DIR%BoostModeConfig.exe"
pause
exit /b 0

:UNINSTALL
echo [1/2] Stopping service...
sc stop %SVC_NAME% >nul 2>&1

echo [2/2] Uninstalling service...
sc delete %SVC_NAME% >nul 2>&1
if %errorlevel% neq 0 (
    echo   - Uninstall failed (service may not exist)
) else (
    echo   - Service uninstalled
    echo [NOTE] Config files kept at: %CFG_DIR%
)
echo.
pause
exit /b 0

:RESTART
echo [1/2] Stopping service...
sc stop %SVC_NAME% >nul 2>&1
timeout /t 3 >nul

echo [2/2] Starting service...
sc start %SVC_NAME% >nul 2>&1
if %errorlevel% equ 0 (
    echo   - Service restarted
) else (
    echo   - Restart failed, check service status
)
echo.
pause
exit /b 0

:STATUS
sc query %SVC_NAME%
echo.
echo Log directory: %CFG_DIR%\logs\
pause
exit /b 0
