namespace BoostModeCommon;

public static class Logger
{
    private static readonly string LogDir;
    private static readonly object LockObj = new();

    static Logger()
    {
        LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "BoostModeSvc", "logs");
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);
    public static void Error(string message, Exception ex) => Write("ERROR", $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

        try
        {
            Directory.CreateDirectory(LogDir);
            var logFile = Path.Combine(LogDir, $"{DateTime.Now:yyyy-MM-dd}.log");
            File.AppendAllText(logFile, line + Environment.NewLine);
        }
        catch { }

        System.Diagnostics.Debug.WriteLine(line);
    }
}
