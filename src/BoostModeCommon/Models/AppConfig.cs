namespace BoostModeCommon.Models;

public class AppConfig
{
    public int CpuLoadThreshold { get; set; } = 50;
    public int CpuIdleThreshold { get; set; } = 15;
    public int LoadHoldSeconds { get; set; } = 5;
    public int IdleHoldSeconds { get; set; } = 30;
    public int PollIntervalMs { get; set; } = 2000;
    public int IdleModeValueAc { get; set; } = 3;
    public int IdleModeValueDc { get; set; } = 3;
    public int LoadModeValueAc { get; set; } = 1;
    public int LoadModeValueDc { get; set; } = 1;
    public bool UseProcessDetection { get; set; } = true;
    public bool UseCpuDetection { get; set; } = true;
    public List<string> ProcessWhitelist { get; set; } = new()
    {
        "VALORANT.exe",
        "VALORANT-Win64-Shipping.exe",
        "RiotClientServices.exe",
        "eldenring.exe",
        "cyberpunk2077.exe",
        "dota2.exe",
        "cs2.exe",
        "gta5.exe",
        "apex.exe",
        "wow.exe",
        "r5apex.exe"
    };
}

public class StatusInfo
{
    public string CurrentMode { get; set; } = "Unknown";
    public int CurrentModeValueAc { get; set; }
    public int CurrentModeValueDc { get; set; }
    public double CpuUsage { get; set; }
    public List<string> GameProcesses { get; set; } = new();
    public string? LastSwitchReason { get; set; }
    public DateTime LastAutoSwitchTime { get; set; }
    public DateTime ServiceStartTime { get; set; }
    public DateTime LastUpdateTime { get; set; }
}
