using System.Diagnostics;
using BoostModeCommon;

namespace BoostModeService;

public class CpuMonitor : IDisposable
{
    private PerformanceCounter? _counter;
    private bool _initialized;

    public bool Initialize()
    {
        try
        {
            _counter = TryGetBestCounter();
            if (_counter != null)
            {
                _counter.NextValue();
                Thread.Sleep(500);
                _counter.NextValue();
                _initialized = true;
                Logger.Info("CPU monitor initialized successfully");
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"CPU monitor init failed: {ex.Message}");
        }
        return false;
    }

    public float GetCpuUsage()
    {
        if (!_initialized || _counter == null)
            return 0f;

        try
        {
            return Math.Clamp(_counter.NextValue(), 0f, 100f);
        }
        catch
        {
            return 0f;
        }
    }

    private static PerformanceCounter? TryGetBestCounter()
    {
        var candidates = new (string category, string counter, string instance)[]
        {
            ("Processor Information", "% Processor Utility", "_Total"),
            ("Processor Information", "% Processor Time", "_Total"),
            ("Processor", "% Processor Time", "_Total"),
        };

        foreach (var (category, counter, instance) in candidates)
        {
            try
            {
                var pc = new PerformanceCounter(category, counter, instance);
                pc.NextValue();
                Logger.Info($"CPU counter: {category}\\{instance}\\{counter}");
                return pc;
            }
            catch { }
        }
        return null;
    }

    public void Dispose() => _counter?.Dispose();
}
