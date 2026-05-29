@echo off
setlocal enabledelayedexpansion
title BoostModeScheduler - Setup

:: Auto-elevate to admin
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting admin privileges...
    timeout /t 1 >nul
    powershell -Command "Start-Process '%~f0' -Verb RunAs -Wait"
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
    echo Please make sure setup.bat is in the same folder as BoostModeService.exe
    pause
    exit /b 1
)

set "SVC_NAME=BoostModeSvc"
set "CFG_DIR=%ProgramData%\BoostModeSvc"

echo ============================================
echo   BoostModeScheduler v1.1.0 - Quick Setup
echo ============================================
echo.

echo [1/4] Creating config directory...
mkdir "%CFG_DIR%" 2>nul

echo [2/4] Installing service...
sc create %SVC_NAME% binPath="\"%SVC_EXE%\"" start=auto >nul 2>&1
if %errorlevel% equ 0 (
    echo   - Service created
) else (
    sc config %SVC_NAME% binPath="\"%SVC_EXE%\"" start=auto >nul 2>&1
    echo   - Service updated
)

echo [3/4] Starting service...
sc start %SVC_NAME% >nul 2>&1
if %errorlevel% equ 0 (
    echo   - Service started
) else (
    echo   - Service already running
)

echo [4/4] Copying config...
if exist "%BASE_DIR%config.json" (
    copy /y "%BASE_DIR%config.json" "%CFG_DIR%\config.json" >nul 2>&1
    echo   - Config deployed
) else (
    echo   - Skipped (config.json not found)
)

echo.
echo ============================================
echo  Setup complete!
echo  Service: %SVC_NAME%
echo  Config:  %CFG_DIR%
echo ============================================
echo.

if exist "%BASE_DIR%BoostModeConfig.exe" (
    echo Launching config tool...
    start "" "%BASE_DIR%BoostModeConfig.exe"
)

echo Press any key to exit...
pause >nul
exit /b 0
