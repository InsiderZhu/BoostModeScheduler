# BoostModeScheduler - .NET SDK 安装脚本
# 以管理员身份运行此脚本安装 .NET 8 SDK

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  BoostModeScheduler - 安装 .NET SDK" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "[错误] 请以管理员身份运行此脚本" -ForegroundColor Red
    pause
    exit 1
}

Write-Host "正在检查 .NET SDK..." -ForegroundColor Yellow

try {
    dotnet --version
    Write-Host ".NET SDK 已安装" -ForegroundColor Green
    dotnet --list-sdks
    Write-Host ""
    Write-Host "完成! 现在可以运行 publish.bat 编译项目。" -ForegroundColor Green
    pause
    exit 0
} catch {
    Write-Host ".NET SDK 未安装，正在通过 winget 安装..." -ForegroundColor Yellow
}

winget install Microsoft.DotNet.SDK.8 --silent --accept-package-agreements

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host ".NET 8 SDK 安装成功!" -ForegroundColor Green
    Write-Host "请重新打开终端，然后运行 publish.bat 编译项目。" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "自动安装失败，请手动安装:" -ForegroundColor Red
    Write-Host "  https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0" -ForegroundColor Yellow
}

pause
