@echo off
chcp 65001 >nul 2>&1
title BoostModeScheduler - 一键安装

:: === 自动提权 ===
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo 正在请求管理员权限，请在弹出的 UAC 窗口中点击"是"...
    timeout /t 2 >nul
    powershell -Command "Start-Process '%~f0' -Verb RunAs -Wait"
    exit /b
)

echo ============================================
echo   BoostModeScheduler v1.1.0 - 一键安装
echo ============================================
echo.

:: 定位到脚本所在目录
cd /d "%~dp0"

:: 检查必要文件
if not exist "%~dp0BoostModeService.exe" (
    echo [错误] 未找到 BoostModeService.exe
    echo 请确认 setup.bat 和所有 exe 在同一目录
    pause
    exit /b 1
)

set "SVC_NAME=BoostModeSvc"
set "CFG_DIR=%ProgramData%\BoostModeSvc"

echo [1/4] 创建配置目录...
mkdir "%CFG_DIR%" 2>nul

echo [2/4] 安装服务...
sc create %SVC_NAME% binPath="\"%~dp0BoostModeService.exe\"" start=auto >nul 2>&1
if %errorlevel% equ 0 (
    echo   - 服务已创建
) else (
    sc config %SVC_NAME% binPath="\"%~dp0BoostModeService.exe\"" start=auto >nul
    echo   - 服务已更新
)

echo [3/4] 启动服务...
sc start %SVC_NAME% >nul 2>&1
if %errorlevel% equ 0 (
    echo   - 服务已启动
) else (
    echo   - 服务已在运行中
)

echo [4/4] 复制配置文件...
if exist "%~dp0config.json" (
    copy /y "%~dp0config.json" "%CFG_DIR%\config.json" >nul 2>&1
    echo   - 配置已部署
) else (
    echo   - 跳过（未找到 config.json）
)

echo.
echo ============================================
echo  安装完成！
echo  服务名: %SVC_NAME%
echo  配置目录: %CFG_DIR%
echo ============================================
echo.

:: 启动配置工具
if exist "%~dp0BoostModeConfig.exe" (
    echo 正在打开配置工具...
    start "" "%~dp0BoostModeConfig.exe"
)

echo 按任意键退出...
pause >nul
exit /b 0
