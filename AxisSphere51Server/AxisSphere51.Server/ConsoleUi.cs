using System.Text;

namespace AxisSphere51.Server;

/// <summary>A small, clean startup/shutdown banner for the console (in place of framework log noise).</summary>
public static class ConsoleUi
{
    private const string Line = "  ────────────────────────────────────────────────────────";

    public static void Banner(string url, string localUrl, int items, int npcs, int regions,
        int accounts, int minPlevel, string logPath)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* redirected output */ }

        Console.WriteLine();
        Colored(ConsoleColor.Cyan, "  AXIS SPHERE51  ·  DATA SERVER");
        Console.WriteLine(Line);
        Field("Status", "running", ConsoleColor.Green);
        Field("Listening", url);
        Field("Swagger", $"{localUrl}/swagger");
        Field("Data", $"{items:N0} items  ·  {npcs:N0} NPCs  ·  {regions:N0} regions");
        Field("Accounts", $"{accounts:N0}  (min PLEVEL {minPlevel} — players blocked)");
        Field("Log", logPath);
        Console.WriteLine(Line);
        Colored(ConsoleColor.Green, "  Ready — press Ctrl+C to stop.");
        Console.WriteLine();
    }

    public static void Stopped() => Colored(ConsoleColor.DarkGray, "  Server stopped.");

    private static void Field(string label, string value, ConsoleColor valueColor = ConsoleColor.Gray)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"   {label,-11}");
        Console.ForegroundColor = valueColor;
        Console.WriteLine(value);
        Console.ForegroundColor = prev;
    }

    private static void Colored(ConsoleColor color, string text)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = prev;
    }
}
