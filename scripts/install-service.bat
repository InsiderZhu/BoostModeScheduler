@echo off
chcp 65001 >nul
echo ============================================
echo   BoostModeScheduler - 安装/卸载服务
echo ============================================
echo.

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [错误] 请以管理员身份运行此脚本
    pause
    exit /b 1
)

set "SERVICE_NAME=BoostModeSvc"
set "SERVICE_EXE=%~dp0..\publish\service\BoostModeService.exe"
set "CONFIG_DIR=%ProgramData%\BoostModeSvc"

if not exist "%SERVICE_EXE%" (
    echo [错误] 未找到服务文件: %SERVICE_EXE%
    echo 请先运行 publish.bat 编译项目
    pause
    exit /b 1
)

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
copy /y "%~dp0..\config.json" "%CONFIG_DIR%\config.json" >nul 2>&1

echo [3/4] 安装服务...
sc create %SERVICE_NAME% binPath="\"%SERVICE_EXE%\"" start=auto
if %errorlevel% neq 0 (
    echo [提示] 服务可能已存在，尝试更新...
    sc config %SERVICE_NAME% binPath="\"%SERVICE_EXE%\"" start=auto
)

echo [4/4] 启动服务...
sc start %SERVICE_NAME%
if %errorlevel% neq 0 (
    echo [警告] 启动失败，可能是权限问题
)
echo.
echo 安装完成! 服务名: %SERVICE_NAME%
echo 配置工具: publish\config\BoostModeConfig.exe
echo.
pause
exit /b 0

:UNINSTALL
echo [1/2] 停止服务...
sc stop %SERVICE_NAME% >nul 2>&1

echo [2/2] 卸载服务...
sc delete %SERVICE_NAME%
if %errorlevel% neq 0 (
    echo [错误] 卸载失败
) else (
    echo 卸载完成!
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
sc start %SERVICE_NAME%
echo.
echo 服务已重启!
echo.
pause
exit /b 0

:STATUS
sc query %SERVICE_NAME%
echo.
echo 日志目录: %CONFIG_DIR%\logs\
pause
exit /b 0
