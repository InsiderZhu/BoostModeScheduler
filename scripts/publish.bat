@echo off
chcp 65001 >nul
echo ============================================
echo   BoostModeScheduler - 一键编译发布
echo ============================================
echo.

:: Check admin
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [错误] 请以管理员身份运行此脚本
    pause
    exit /b 1
)

:: Check .NET SDK
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [信息] .NET SDK 未安装，正在通过 winget 安装...
    winget install Microsoft.DotNet.SDK.8 --silent --accept-package-agreements
    if %errorlevel% neq 0 (
        echo [错误] 安装 .NET SDK 失败，请手动从 https://dotnet.microsoft.com/download 安装 .NET 8 SDK
        pause
        exit /b 1
    )
    echo [信息] .NET SDK 安装完毕，请重新运行此脚本
    pause
    exit /b 0
)

echo [1/3] 编译项目...
cd /d "%~dp0..\src"
dotnet publish BoostModeService\BoostModeService.csproj -c Release -o "%~dp0..\publish\service" --self-contained true -p:PublishSingleFile=true
if %errorlevel% neq 0 (
    echo [错误] 服务编译失败
    pause
    exit /b 1
)

dotnet publish BoostModeConfig\BoostModeConfig.csproj -c Release -o "%~dp0..\publish\config" --self-contained true -p:PublishSingleFile=true
if %errorlevel% neq 0 (
    echo [错误] 配置工具编译失败
    pause
    exit /b 1
)

echo [2/3] 复制文件...
copy /y "%~dp0..\config.json" "%~dp0..\publish\" >nul

echo [3/3] 编译完成!
echo.
echo   发布目录: %~dp0..\publish\
echo   服务:    publish\service\BoostModeService.exe
echo   配置:    publish\config\BoostModeConfig.exe
echo.
echo 下一步: 运行 install-service.bat 安装 Windows 服务
echo.

pause
