using System.Diagnostics;

namespace BoostModeService;

public class ProcessDetector
{
    private HashSet<string> _whitelist = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lockObj = new();

    public ProcessDetector(IEnumerable<string> whitelist)
    {
        UpdateWhitelist(whitelist);
    }

    public void UpdateWhitelist(IEnumerable<string> whitelist)
    {
        lock (_lockObj)
        {
            _whitelist = new HashSet<string>(whitelist, StringComparer.OrdinalIgnoreCase);
        }
    }

    public bool HasGameRunning()
    {
        return GetCurrentGameProcesses().Count > 0;
    }

    public List<string> GetCurrentGameProcesses()
    {
        var result = new List<string>();
        lock (_lockObj)
        {
            if (_whitelist.Count == 0) return result;

            try
            {
                var allProcesses = Process.GetProcesses();
                foreach (var proc in allProcesses)
                {
                    try
                    {
                        var procName = proc.ProcessName;
                        foreach (var target in _whitelist)
                        {
                            if (procName.Equals(target, StringComparison.OrdinalIgnoreCase) ||
                                procName.Equals(Path.GetFileNameWithoutExtension(target), StringComparison.OrdinalIgnoreCase))
                            {
                                result.Add(proc.ProcessName + ".exe");
                                break;
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
            catch { }
        }
        return result;
    }
}
