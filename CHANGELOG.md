# Changelog

## [Unreleased]

### Changed
- 服务启动/停止/重启写入 `SWITCH:` 格式日志，显示在切换记录中
- 服务状态刷新前调用 `Refresh()` 确保实时更新

### Fixed
- 移除 `install-service.ps1` 中的绝对路径，避免隐私泄露
- `.gitignore` 排除 `AGENTS.md` 和 `.codegraph/`

## [1.0.0] - 2026-05-29

### Added
- 项目初始化：`BoostModeCommon` 共享库 + `BoostModeService` 服务 + `BoostModeConfig` 配置工具
- CPU 负载检测：PerformanceCounter 封装，3 级 fallback
- 游戏进程白名单检测，发现游戏立即升载
- 滞回判定：升载确认 5s，降载确认 30s
- `PowerModeSwitcher` powercfg 封装：自动检测方案 GUID，读/写提升模式值
- `PowerModeSwitcher.SwitchToSeparate(acVal, dcVal)` AC/DC 独立设置
- 配置工具 WinForms：服务控制、实时状态、检测设置、进程白名单、手动切换
- 模式名称从系统 `powercfg /Q` 动态读取（GBK 936 编码）
- 窗口可调大小，控件自适应布局
- 内嵌切换记录展示，每 2 秒自动刷新，从日志文件解析
- AC/DC 独立下拉框配置空闲/负载模式
- 上次切换原因从日志读取（含手动切换），新增"上次自动切换时间"
- 日志写入 `%ProgramData%\BoostModeSvc\logs\`
- 发布脚本 `publish.bat`、服务安装脚本 `install-service.bat`

[Unreleased]: https://github.com/InsiderZhu/BoostModeScheduler/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/InsiderZhu/BoostModeScheduler/releases/tag/v1.0.0
