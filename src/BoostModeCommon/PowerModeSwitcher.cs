using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace BoostModeCommon;

public partial class PowerModeSwitcher
{
    private const string SubProcessorGuid = "54533251-82be-4824-96c1-47b60b740d00";
    private const string SettingGuid = "be337238-0d82-4146-a960-4f3749d470c7";

    private string _cachedSchemeGuid = "";
    private int _currentMode = -1;

    public string GetActiveSchemeGuid()
    {
        if (!string.IsNullOrEmpty(_cachedSchemeGuid))
            return _cachedSchemeGuid;

        try
        {
            var psi = new ProcessStartInfo("powercfg", "/GETACTIVESCHEME")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);

            var match = SchemeGuidRegex().Match(output);
            if (match.Success)
            {
                _cachedSchemeGuid = match.Groups[1].Value;
                Logger.Info($"Active scheme detected: {_cachedSchemeGuid}");
                return _cachedSchemeGuid;
            }

            Logger.Error($"Could not parse active scheme from: {output.Trim()[..Math.Min(output.Length, 100)]}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to detect active scheme: {ex.Message}");
        }
        return "";
    }

    public int ReadCurrentMode()
    {
        var scheme = GetActiveSchemeGuid();
        if (string.IsNullOrEmpty(scheme))
            return -1;

        try
        {
            var psi = new ProcessStartInfo("powercfg", $"/Q {scheme} {SubProcessorGuid} {SettingGuid}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);

            foreach (var line in output.Split('\n', '\r'))
            {
                if (line.Contains("0x", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(':');
                    var hexPart = parts.Length > 1 ? parts.Last().Trim() : line.Trim();
                    var hex = hexPart.Replace("0x", "").Replace("0X", "");
                    _currentMode = Convert.ToInt32(hex, 16);
                    return _currentMode;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to read current mode: {ex.Message}");
            _cachedSchemeGuid = "";
        }
        return -1;
    }

    public bool SwitchTo(int modeValue, out string output)
    {
        return SwitchToSeparate(modeValue, modeValue, out output);
    }

    public bool SwitchToSeparate(int acValue, int dcValue, out string output)
    {
        var scheme = GetActiveSchemeGuid();
        if (string.IsNullOrEmpty(scheme))
        {
            output = "Failed to detect active power scheme";
            return false;
        }

        var sb = new StringBuilder();

        try
        {
            var acResult = RunPowerCfg($"/SETACVALUEINDEX {scheme} {SubProcessorGuid} {SettingGuid} {acValue}");
            sb.Append($"AC:{acResult}; ");

            var dcResult = RunPowerCfg($"/SETDCVALUEINDEX {scheme} {SubProcessorGuid} {SettingGuid} {dcValue}");
            sb.Append($"DC:{dcResult}; ");

            var activeResult = RunPowerCfg($"/SETACTIVE {scheme}");
            sb.Append($"Active:{activeResult}");

            _currentMode = acValue;
            _cachedSchemeGuid = scheme;
            output = sb.ToString();
            return true;
        }
        catch (Exception ex)
        {
            output = $"Exception: {ex.Message}";
            return false;
        }
    }

    private static string RunPowerCfg(string args)
    {
        var psi = new ProcessStartInfo("powercfg", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit(5000);

        if (p.ExitCode == 0)
            return "OK";

        var err = p.StandardError.ReadToEnd().Trim();
        if (err.Length > 80) err = err[..80];
        return $"FAIL({p.ExitCode}): {err}";
    }

    public Dictionary<int, string> ReadModeNames()
    {
        var result = new Dictionary<int, string>();
        var scheme = GetActiveSchemeGuid();
        if (string.IsNullOrEmpty(scheme)) return result;

        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var psi = new ProcessStartInfo("powercfg", $"/Q {scheme} {SubProcessorGuid} {SettingGuid}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.GetEncoding(936)
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);

            var lines = output.Split('\n', '\r')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToArray();

            for (int i = 0; i < lines.Length - 1; i++)
            {
                var valMatch = Regex.Match(lines[i], @":\s*(\d{3})\s*$");
                if (!valMatch.Success) continue;

                var nameMatch = Regex.Match(lines[i + 1], @":\s*(.+)\s*$");
                if (!nameMatch.Success) continue;

                int val = int.Parse(valMatch.Groups[1].Value);
                if (val >= 0 && val <= 6)
                    result[val] = nameMatch.Groups[1].Value.Trim();
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to read mode names: {ex.Message}");
        }

        return result;
    }

    [GeneratedRegex(@"GUID:\s*([a-fA-F0-9\-]{36})")]
    private static partial Regex SchemeGuidRegex();
}
