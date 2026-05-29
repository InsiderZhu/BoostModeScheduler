using BoostModeCommon;
using BoostModeCommon.Models;
using Microsoft.Extensions.Hosting;

namespace BoostModeService;

public class Worker : BackgroundService
{
    private AppConfig _config;
    private readonly CpuMonitor _cpuMonitor;
    private ProcessDetector _processDetector;
    private readonly PowerModeSwitcher _powerSwitcher;

    private int _loadTickCount;
    private int _idleTickCount;
    private bool _isLoadMode;
    private int _currentModeValue = -1;
    private string _lastSwitchReason = "";
    private readonly DateTime _startTime = DateTime.Now;

    public Worker(PowerModeSwitcher powerSwitcher, CpuMonitor cpuMonitor)
    {
        _powerSwitcher = powerSwitcher;
        _cpuMonitor = cpuMonitor;
        _config = ConfigManager.Load();
        _processDetector = new ProcessDetector(_config.ProcessWhitelist);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.Info("========================================");
        Logger.Info("BoostModeSvc starting...");

        _config = ConfigManager.Load();

        _cpuMonitor.Initialize();

        _processDetector = new ProcessDetector(_config.ProcessWhitelist);

        _currentModeValue = _powerSwitcher.ReadCurrentMode();
        _isLoadMode = _currentModeValue == _config.LoadModeValue;

        Logger.Info($"Initial mode: {(_isLoadMode ? "LOAD" : "IDLE")} (value={_currentModeValue})");
        Logger.Info($"Config: LoadThreshold={_config.CpuLoadThreshold}%, IdleThreshold={_config.CpuIdleThreshold}%");
        Logger.Info($"Config: LoadHold={_config.LoadHoldSeconds}s, IdleHold={_config.IdleHoldSeconds}s");
        Logger.Info($"Config: ProcessDetection={_config.UseProcessDetection}, CpuDetection={_config.UseCpuDetection}");

        Logger.Info("Waiting 10s for system stabilization...");
        await Task.Delay(10_000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _config = ConfigManager.Load();
                _processDetector.UpdateWhitelist(_config.ProcessWhitelist);

                float cpuUsage = _cpuMonitor.GetCpuUsage();
                var gameProcesses = _processDetector.GetCurrentGameProcesses();
                bool hasGame = _config.UseProcessDetection && gameProcesses.Count > 0;

                bool shouldBeLoad = DetermineMode(cpuUsage, hasGame);

                if (shouldBeLoad != _isLoadMode)
                {
                    int targetMode = shouldBeLoad ? _config.LoadModeValue : _config.IdleModeValue;
                    string reason = BuildReason(cpuUsage, hasGame, gameProcesses);

                    if (_powerSwitcher.SwitchTo(targetMode, out string output))
                    {
                        _isLoadMode = shouldBeLoad;
                        _currentModeValue = targetMode;
                        _lastSwitchReason = reason;
                        Logger.Info($"SWITCH: -> {(shouldBeLoad ? "LOAD" : "IDLE")} (value={targetMode})");
                        Logger.Info($"  Reason: {reason}");
                        Logger.Info($"  Result: {output}");
                    }
                }

                WriteStatusFile(cpuUsage, gameProcesses);
            }
            catch (Exception ex)
            {
                Logger.Error("Loop error", ex);
            }

            await Task.Delay(Math.Max(_config.PollIntervalMs, 500), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Logger.Info("BoostModeSvc shutting down...");
        await base.StopAsync(cancellationToken);
    }

    private bool DetermineMode(float cpu, bool hasGame)
    {
        int loadHoldTicks = (int)Math.Ceiling(_config.LoadHoldSeconds * 1000.0 / _config.PollIntervalMs);
        int idleHoldTicks = (int)Math.Ceiling(_config.IdleHoldSeconds * 1000.0 / _config.PollIntervalMs);

        bool isLoadCondition = hasGame || (_config.UseCpuDetection && cpu >= _config.CpuLoadThreshold);
        bool isIdleCondition = !hasGame && _config.UseCpuDetection && cpu < _config.CpuIdleThreshold;

        if (isLoadCondition) { _loadTickCount++; _idleTickCount = 0; }
        else if (isIdleCondition) { _idleTickCount++; _loadTickCount = 0; }

        if (_isLoadMode)
            return _idleTickCount < idleHoldTicks;
        else
            return _loadTickCount >= loadHoldTicks;
    }

    private string BuildReason(float cpu, bool hasGame, List<string> games)
    {
        if (hasGame)
            return $"GameProcess: {string.Join(", ", games)}";
        return $"CPU={cpu:F1}%, Ticks(Load={_loadTickCount}, Idle={_idleTickCount})";
    }

    private void WriteStatusFile(float cpu, List<string> gameProcesses)
    {
        var status = new StatusInfo
        {
            CurrentMode = _isLoadMode ? "LOAD" : "IDLE",
            CurrentModeValue = _currentModeValue,
            CpuUsage = Math.Round(cpu, 1),
            GameProcesses = gameProcesses,
            LastSwitchReason = _lastSwitchReason,
            ServiceStartTime = _startTime,
            LastUpdateTime = DateTime.Now
        };
        ConfigManager.SaveStatus(status);
    }
}
