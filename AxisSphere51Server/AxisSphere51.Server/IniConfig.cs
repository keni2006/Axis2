namespace AxisSphere51.Server;

/// <summary>
/// Loads the human-friendly <c>axisserver.ini</c> (plain key = value, "#"/";" comments,
/// comma-separated file lists) and feeds it into the app configuration, so the rest of the server
/// still binds ScriptOptions/AccountOptions the normal way. No JSON, no extra packages.
/// </summary>
public static class IniConfig
{
    public static void AddAxisIni(this IConfigurationBuilder config, string baseDir)
    {
        var path = Path.Combine(baseDir, "axisserver.ini");
        if (!File.Exists(path))
            return;

        var kv = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#' || line[0] == ';' || line[0] == '[')
                continue;

            var eq = line.IndexOf('=');
            if (eq < 0)
                continue;

            var key = line[..eq].Trim();
            var val = line[(eq + 1)..].Trim();

            switch (key.ToLowerInvariant())
            {
                case "url":
                case "urls":
                    kv["Urls"] = val;
                    break;
                case "basedirectory":
                    kv["Scripts:BaseDirectory"] = val;
                    if (!kv.ContainsKey("Accounts:BaseDirectory"))
                        kv["Accounts:BaseDirectory"] = val;
                    break;
                case "itemfiles": AddList(kv, "Scripts:ItemFiles", val); break;
                case "charfiles": AddList(kv, "Scripts:CharFiles", val); break;
                case "mapfiles": AddList(kv, "Scripts:MapFiles", val); break;
                case "spellfiles": AddList(kv, "Scripts:SpellFiles", val); break;
                case "accountdirectory": kv["Accounts:BaseDirectory"] = val; break;
                case "accountfile":
                case "accountfiles": AddList(kv, "Accounts:Files", val); break;
                case "minplevel": kv["Accounts:MinPlevel"] = val; break;
            }
        }

        config.AddInMemoryCollection(kv);
    }

    private static void AddList(Dictionary<string, string?> kv, string prefix, string csv)
    {
        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < parts.Length; i++)
            kv[$"{prefix}:{i}"] = parts[i];
    }
}
