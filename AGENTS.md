# BoostModeScheduler — AGENTS.md

## 项目概览

根据 CPU 负载/游戏进程自动切换 Windows 处理器性能提升模式（空闲=3 高效，负载=1 已启用），AC/DC 独立配置。防止待机蓝屏且不影响游戏性能。

### 项目结构

```
├─ src/
│  ├─ BoostModeCommon/       共享库 (无第三方依赖，仅修改 by powercfg)
│  │  ├─ PowerModeSwitcher   powercfg 封装: 读/写提升模式值，自动检测方案 GUID
│  │  ├─ ConfigManager       读写 %ProgramData%\BoostModeSvc\{config,status}.json
│  │  └─ Logger              写日志到 %ProgramData%\BoostModeSvc\logs\yyyy-MM-dd.log
│  │
│  ├─ BoostModeService/      Windows 服务 (BackgroundService)
│  │  ├─ Worker              主循环: CPU 采样 → 进程检测 → 滞回判定 → 切换
│  │  ├─ CpuMonitor          PerformanceCounter 封装，3 级 fallback
│  │  └─ ProcessDetector     白名单进程匹配 (OrdinalIgnoreCase)
│  │
│  └─ BoostModeConfig/       WinForms 配置工具
│     └─ MainForm            服务控制 + 设置 + 白名单 + 手动切换 + 切换记录
│
├─ scripts/
│  ├─ publish.bat            一键编译发布 (需 .NET 8 SDK，自动 winget)
│  └─ install-service.bat    安装/卸载/重启/查看服务 (需管理员)
├─ config.json               默认配置 (部署到 %ProgramData% 后生效)
└─ publish/                  输出目录 (被 .gitignore 排除)
```

### 关键架构事实

- **服务名**: `BoostModeSvc`（sc 操作时用此名）
- **入口点**: `BoostModeService/Program.cs` 使用 `Host.CreateApplicationBuilder` + `AddWindowsService`
- **配置路径**: `%ProgramData%\BoostModeSvc\config.json`
- **状态文件**: `%ProgramData%\BoostModeSvc\status.json`（Worker 每轮写入，Config 每 2s 读取）
- **日志格式**: `[yyyy-MM-dd HH:mm:ss] [LEVEL] message`，切换事件格式为 `SWITCH: -> LOAD/IDLE/MANUAL (AC=N, DC=N)` + 换行 `Reason: ...` + `Result: ...`。Config 解析此格式显示"切换记录"。
- **电源 GUID**: 自动通过 `powercfg /GETACTIVESCHEME` 检测。硬编码的子组/设置 GUID: `54533251-...` / `be337238-...`
- **AC/DC 独立**: `PowerModeSwitcher.SwitchToSeparate(acVal, dcVal)` 分别设置电源和电池模式值，`SwitchTo(v)` 是两者一致时的便捷方法。
- **模式名称**: 运行时通过 `powercfg /Q` 以 GBK(代码页 936) 解码动态读取，不硬编码

### 开发命令

```powershell
# 编译
dotnet build src\BoostModeService\BoostModeService.csproj -c Release
dotnet build src\BoostModeConfig\BoostModeConfig.csproj -c Release

# 发布 (single-file self-contained, 无需预装 .NET)
dotnet publish src\BoostModeService\BoostModeService.csproj -c Release -o publish\service --self-contained true -p:PublishSingleFile=true
dotnet publish src\BoostModeConfig\BoostModeConfig.csproj -c Release -o publish\config --self-contained true -p:PublishSingleFile=true
```

### 部署操作

所有操作需**管理员权限**：

```powershell
# 安装/启动
sc create BoostModeSvc binPath="C:\path\to\publish\service\BoostModeService.exe" start=auto
sc start BoostModeSvc

# 停止/重启
sc stop BoostModeSvc; Start-Sleep 3; sc start BoostModeSvc

# 卸载
sc stop BoostModeSvc; sc delete BoostModeSvc
```

或运行 `scripts\install-service.bat`（交互式菜单）。

### 约束与陷阱

- **GBK 编码**: 从 powercfg 读取中文模式名称时需 `Encoding.GetEncoding(936)` + 注册 `CodePagesEncodingProvider`。两个 Program.cs 入口均有 `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`
- **PerformanceCounter 初始化**: `CpuMonitor.Initialize()` 必须在构造后调用（CPU 首次采样需要两次 `NextValue()` 加 500ms 间隔）
- **自包含发布**: `--self-contained true` + `-p:PublishSingleFile=true`。两个 exe 各自独立，无运行时依赖
- **服务启动延迟**: Worker 启动后先 `Task.Delay(10_000)` 等系统稳定
- **滞回**: 升载需满足阈值持续 `LoadHoldSeconds`，降载需持续 `IdleHoldSeconds`。游戏进程触发立即切换
