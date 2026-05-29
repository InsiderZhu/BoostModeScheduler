# BoostModeScheduler

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

> **Windows 处理器性能提升模式自动切换工具** / *Windows Processor Boost Mode Auto-Switcher*

根据 CPU 负载和前台游戏进程，自动在"空闲"与"负载"处理器性能提升模式间切换，防止待机蓝屏的同时确保游戏性能不受影响。AC（电源）和 DC（电池）模式值可独立配置。

*Automatically toggles between idle and load processor performance boost modes based on CPU usage and foreground game processes — preventing idle crashes without sacrificing gaming performance. AC (power) and DC (battery) values are independently configurable.*

---

## Features / 功能

- **⚡ 智能模式切换** — 低负载时切至节能模式（默认 3-高效），高负载或检测到游戏时切至性能模式（默认 1-已启用）
- **🕹️ 游戏进程检测** — 白名单匹配前台进程，发现游戏立即升载，无延迟
- **⚙️ AC/DC 独立配置** — 电源（AC）和电池（DC）可分别配置空闲/负载模式值
- **📈 滞回判定** — 升载确认 5s，降载确认 30s，避免频繁抖动
- **🔧 图形配置工具** — WinForms 界面，可实时查看状态、调整参数、手动切换
- **📋 切换记录** — 所有切换事件写入日志，配置工具内实时显示切换历史
- **🖥️ Windows 服务** — 后台静默运行，开机自启，无需用户登录
- **🔄 模式名称自动读取** — 从系统 `powercfg /Q` 动态读取，与系统显示一致

---

## Architecture / 架构

```
BoostModeScheduler
├─ src/
│  ├─ BoostModeCommon/        # 共享库：配置读写、powercfg 封装、日志
│  │  ├─ PowerModeSwitcher    # powercfg 封装：读/写提升模式值，自动检测方案 GUID
│  │  ├─ ConfigManager        # 读写 %ProgramData%\BoostModeSvc\{config,status}.json
│  │  └─ Logger               # 日志写入 %ProgramData%\BoostModeSvc\logs\
│  │
│  ├─ BoostModeService/       # Windows 服务 (BackgroundService)
│  │  ├─ Worker               # 主循环：CPU 采样 → 进程检测 → 滞回判定 → 切换
│  │  ├─ CpuMonitor           # PerformanceCounter 封装，3 级 fallback
│  │  └─ ProcessDetector      # 白名单进程匹配
│  │
│  └─ BoostModeConfig/        # WinForms 配置工具
│     └─ MainForm             # 服务控制 + 设置 + 白名单 + 手动切换 + 切换记录
│
├─ scripts/                   # 部署脚本
├─ config.json                # 默认配置
└─ publish/                   # 编译输出
```

### Data Flow / 数据流

```
                    ┌──────────────────┐
                    │   Config Tool    │ ← 用户界面
                    │ (WinForms)       │ → 手动切换、查看日志
                    └────────┬─────────┘
                             │ 写入/读取
                    ┌────────▼─────────┐
                    │  config.json     │
                    │  status.json     │
                    │  logs/           │
                    └────────┬─────────┘
                             │ 读取
                    ┌────────▼─────────┐
                    │   Worker (服务)   │ ← CPU 采样每 2s
                    │                  │ → 游戏进程检测
                    │                  │ → powercfg 切换
                    └────────┬─────────┘
                             │
                    ┌────────▼─────────┐
                    │   powercfg       │
                    │  (系统电源设置)    │
                    └──────────────────┘
```

---

## Requirements / 环境要求

- **系统**: Windows 10/11 (64-bit)
- **权限**: 管理员权限（安装服务、修改电源设置）
- **运行时**: .NET 8 运行时 可选（发布版为 self-contained，无需预装）

---

## Installation / 安装

### 1. Download / 下载发布版

从 [Releases](https://github.com/InsiderZhu/BoostModeScheduler/releases) 下载最新版，或自行编译（见下方）。

### 2. Install Service / 安装服务

**以管理员身份运行 PowerShell**：

```powershell
# 安装并启动服务
sc.exe create BoostModeSvc binPath="C:\path\to\BoostModeService.exe" start=auto
sc.exe start BoostModeSvc

# 或使用脚本
.\scripts\install-service.bat
```

### 3. Run Config Tool / 运行配置工具

```powershell
.\publish\config\BoostModeConfig.exe
```

---

## Usage / 使用

### Configuration Tool / 配置工具

| Section / 区域 | Description / 说明 |
|---|---|
| **服务控制** | 启动、停止、重启服务 |
| **当前状态** | 实时显示当前模式、CPU 占用、游戏进程、切换原因 |
| **检测设置** | 调整 CPU 阈值、确认时间、轮询间隔、AC/DC 模式 |
| **进程白名单** | 管理被识别为"负载"的游戏进程列表 |
| **手动切换** | 立即强制切换到指定模式（AC/DC 独立） |
| **切换记录** | 展示最近的自动/手动切换历史（来自日志，重启不丢失） |

### Default Configuration / 默认配置

| Parameter / 参数 | Default / 默认值 |
|---|---|
| CPU Load Threshold / 负载阈值 | ≥ 50% |
| CPU Idle Threshold / 空闲阈值 | < 15% |
| Load Hold Time / 升载确认 | 5 seconds |
| Idle Hold Time / 降载确认 | 30 seconds |
| Poll Interval / 轮询间隔 | 2000 ms |
| Idle Mode AC/DC / 空闲模式 | 3 (Efficient / 高效) |
| Load Mode AC/DC / 负载模式 | 1 (Enabled / 已启用) |

---

## Build from Source / 从源码编译

### Prerequisites / 前置条件

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Commands / 命令

```powershell
# 编译
dotnet build src\BoostModeService\BoostModeService.csproj -c Release
dotnet build src\BoostModeConfig\BoostModeConfig.csproj -c Release

# 发布 (self-contained single-file)
dotnet publish src\BoostModeService\BoostModeService.csproj -c Release -o publish\service --self-contained true -p:PublishSingleFile=true
dotnet publish src\BoostModeConfig\BoostModeConfig.csproj -c Release -o publish\config --self-contained true -p:PublishSingleFile=true

# 或一键发布
.\scripts\publish.bat
```

---

## Power Mode Values / 提升模式值说明

系统支持的处理器性能提升模式值（可通过 `powercfg /Q` 查看）：

| Value | Name (English) | 名称 (中文) |
|:-----:|----------------|-------------|
| 0 | Disabled | 已禁用 |
| 1 | Enabled | 已启用 |
| 2 | Aggressive | 积极 |
| 3 | Efficient | 高效 |
| 4 | Aggressive Efficient | 积极高效 |
| 5 | Performance Preferred | 性能优先 |
| 6 | Efficient Performance Preferred | 高效性能优先 |

---

## Data Locations / 文件位置

| Item / 项目 | Path / 路径 |
|---|---|
| 配置文件 | `%ProgramData%\BoostModeSvc\config.json` |
| 状态文件 | `%ProgramData%\BoostModeSvc\status.json` |
| 日志目录 | `%ProgramData%\BoostModeSvc\logs\` |

---

## Troubleshooting / 常见问题

**Q: 服务启动失败？**
确保以管理员权限运行安装命令。查看日志文件 `%ProgramData%\BoostModeSvc\logs\` 排查错误。

**Q: 配置工具无法连接服务？**
确认服务已安装并启动：`sc.exe query BoostModeSvc`。首次安装后需等待 10 秒服务初始化。

**Q: 模式名称显示为乱码？**
系统需支持 GBK 编码（中文 Windows 默认）。配置工具和服务已内置 `CodePagesEncodingProvider` 支持。

---

## License / 许可

MIT License — see [LICENSE](LICENSE) for details.

---

## Disclaimer / 免责声明

修改处理器性能提升模式可能影响系统稳定性。请在使用前了解各模式的含义。作者不对因使用本工具造成的任何损失负责。

*Modifying processor performance boost modes may affect system stability. Please understand each mode's behavior before use. The author assumes no liability for any damages.*
