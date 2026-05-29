# Self-elevate if not admin
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "正在申请管理员权限..." -ForegroundColor Yellow
    Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  BoostModeScheduler - 安装服务" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$serviceExe = "E:\project\BoostModeScheduler\publish\service\BoostModeService.exe"
$configDir = "$env:ProgramData\BoostModeSvc"

if (-not (Test-Path $serviceExe)) {
    Write-Host "[错误] 未找到服务文件: $serviceExe" -ForegroundColor Red
    Write-Host "请先运行 publish.bat 编译项目" -ForegroundColor Yellow
    pause
    exit 1
}

Write-Host "[1/3] 确保配置目录..." -ForegroundColor Green
New-Item -ItemType Directory -Force -Path "$configDir\logs" | Out-Null

Write-Host "[2/3] 创建并启动服务..." -ForegroundColor Green
sc.exe create BoostModeSvc binPath="""$serviceExe""" start=auto
$createResult = $LASTEXITCODE

if ($createResult -eq 0) {
    Write-Host "  服务创建成功" -ForegroundColor Green
} else {
    Write-Host "  服务可能已存在，尝试更新配置..." -ForegroundColor Yellow
    sc.exe config BoostModeSvc binPath="""$serviceExe""" start=auto
}

Write-Host "[3/3] 启动服务..." -ForegroundColor Green
sc.exe start BoostModeSvc
$startResult = $LASTEXITCODE

Write-Host ""
Write-Host "服务状态:" -ForegroundColor Cyan
sc.exe query BoostModeSvc

Write-Host ""
if ($startResult -eq 0) {
    Write-Host "====================================" -ForegroundColor Green
    Write-Host "  服务部署完成!" -ForegroundColor Green
    Write-Host "====================================" -ForegroundColor Green
} else {
    Write-Host "====================================" -ForegroundColor Yellow
    Write-Host "  服务可能已运行，请检查上方状态" -ForegroundColor Yellow
    Write-Host "====================================" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "📌 配置工具: E:\project\BoostModeScheduler\publish\config\BoostModeConfig.exe" -ForegroundColor White
Write-Host "📌 配置文件: $configDir\config.json" -ForegroundColor White
Write-Host "📌 日志目录: $configDir\logs" -ForegroundColor White
Write-Host ""

Start-Sleep -Seconds 5
