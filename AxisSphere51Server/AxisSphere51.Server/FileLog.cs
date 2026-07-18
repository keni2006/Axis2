namespace AxisSphere51.Server;

/// <summary>
/// Minimal dependency-free file logger. Writes one daily file under a "logs" folder next to the
/// executable, capturing server start/stop, connections, every API request (IP + user + route +
/// status) and authentication results. Thread-safe; never throws.
/// </summary>
public static class FileLog
{
    private static readonly object _gate = new();
    private static string _dir = "logs";

    public static void Init(string baseDir)
    {
        try
        {
            _dir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(_dir);
        }
        catch { /* ignore */ }
    }

    public static void Write(string category, string message)
    {
        try
        {
            var now = DateTime.Now;
            var line = $"{now:yyyy-MM-dd HH:mm:ss} [{category,-5}] {message}";
            var path = Path.Combine(_dir, $"server-{now:yyyy-MM-dd}.log");
            lock (_gate)
            {
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch { /* logging must never break the server */ }
    }
}
