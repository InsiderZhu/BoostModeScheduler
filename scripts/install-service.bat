@echo off
chcp 65001 >nul 2>&1
title BoostModeScheduler - 服务管理

:: 自动提权
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo 正在请求管理员权限...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

set "SERVICE_NAME=BoostModeSvc"
set "CONFIG_DIR=%ProgramData%\BoostModeSvc"

:: 查找 BoostModeService.exe（支持两种目录结构）
if exist "%~dp0BoostModeService.exe" (
    set "SERVICE_EXE=%~dp0BoostModeService.exe"
) else if exist "%~dp0..\publish\service\BoostModeService.exe" (
    set "SERVICE_EXE=%~dp0..\publish\service\BoostModeService.exe"
) else (
    echo [错误] 未找到 BoostModeService.exe
    echo 请确认该文件与 install-service.bat 在同一目录
    pause
    exit /b 1
)

:: 查找 config.json
if exist "%~dp0config.json" (
    set "CONFIG_SRC=%~dp0config.json"
) else if exist "%~dp0..\config.json" (
    set "CONFIG_SRC=%~dp0..\config.json"
)

echo ============================================
echo   BoostModeScheduler - 服务管理
echo ============================================
echo.
echo 请选择操作:
echo   1. 安装并启动服务
echo   2. 停止并卸载服务
echo   3. 重启服务
echo   4. 查看服务状态
echo.
set /p CHOICE="请输入选项 (1-4): "

if "%CHOICE%"=="1" goto INSTALL
if "%CHOICE%"=="2" goto UNINSTALL
if "%CHOICE%"=="3" goto RESTART
if "%CHOICE%"=="4" goto STATUS
echo 无效选项
pause
exit /b 1

:INSTALL
echo [1/4] 创建配置目录...
mkdir "%CONFIG_DIR%" 2>nul

echo [2/4] 复制默认配置...
if defined CONFIG_SRC (
    copy /y "%CONFIG_SRC%" "%CONFIG_DIR%\config.json" >nul 2>&1
) else (
    echo   - 跳过（未找到 config.json）
)

echo [3/4] 安装服务...
sc create %SERVICE_NAME% binPath="\"%SERVICE_EXE%\"" start=auto >nul 2>&1
if %errorlevel% equ 0 (
    echo   - 服务已创建
) else (
    sc config %SERVICE_NAME% binPath="\"%SERVICE_EXE%\"" start=auto >nul
    echo   - 服务已更新
)

echo [4/4] 启动服务...
sc start %SERVICE_NAME% >nul 2>&1
if %errorlevel% equ 0 (
    echo   - 服务已启动
) else (
    echo   - 服务已在运行中
)

echo.
echo 安装完成!
echo.
echo 配置工具: "%~dp0BoostModeConfig.exe"
pause
exit /b 0

:UNINSTALL
echo [1/2] 停止服务...
sc stop %SERVICE_NAME% >nul 2>&1

echo [2/2] 卸载服务...
sc delete %SERVICE_NAME% >nul 2>&1
if %errorlevel% neq 0 (
    echo   - 卸载失败（服务可能不存在）
) else (
    echo   - 服务已卸载
    echo [提示] 配置文件保留在: %CONFIG_DIR%
)
echo.
pause
exit /b 0

:RESTART
echo [1/2] 停止服务...
sc stop %SERVICE_NAME% >nul 2>&1
timeout /t 3 >nul

echo [2/2] 启动服务...
sc start %SERVICE_NAME% >nul 2>&1
if %errorlevel% equ 0 (
    echo   - 服务已重启
) else (
    echo   - 重启失败，请检查服务状态
)
echo.
pause
exit /b 0

:STATUS
sc query %SERVICE_NAME%
echo.
echo 日志目录: %CONFIG_DIR%\logs\
pause
exit /b 0
