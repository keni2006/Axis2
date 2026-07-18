using System.Globalization;
using System.Text.RegularExpressions;

namespace AxisSphere51.Server;

/// <summary>
/// Parses legacy Sphere 0.51a ("The Abyss") text scripts. Same semantics as the
/// Axis Sphere51 desktop parser: bracket headers (modern [ITEMDEF name] or legacy
/// bare-hex [0000]); legacy blocks are items unless the file name contains "char";
/// KEY=VALUE body lines; DUPELIST clones and hex-tolerant DUPEITEM inheritance.
/// </summary>
public static partial class SphereParser
{
    [GeneratedRegex(@"\[(ITEMDEF|MULTIDEF|TEMPLATE|CHARDEF|NPC|SPAWN|AREADEF|ROOMDEF)\s+([^\]]+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex HeaderRegex();

    [GeneratedRegex(@"^\[\s*(?:0x)?([0-9A-Fa-f]{1,5})\s*\]$", RegexOptions.IgnoreCase)]
    private static partial Regex LegacyRegex();

    private static readonly Dictionary<string, string> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ITEMDEF"] = "item",
        ["MULTIDEF"] = "multi",
        ["TEMPLATE"] = "template",
        ["CHARDEF"] = "npc",
        ["NPC"] = "npc",
        ["SPAWN"] = "spawn",
        ["AREADEF"] = "area",
        ["ROOMDEF"] = "room",
    };

    public static long? HexValue(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    public static List<SObject> ParseFile(string path)
    {
        var result = new List<SObject>();
        if (!File.Exists(path)) return result;

        var fileName = Path.GetFileName(path);
        var legacyType = fileName.Contains("char", StringComparison.OrdinalIgnoreCase) ? "npc" : "item";
        var lines = File.ReadAllLines(path);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!(line.StartsWith('[') && line.EndsWith(']'))) continue;

            var m = HeaderRegex().Match(line);
            var legacy = m.Success ? null : LegacyRegex().Match(line);
            if (!m.Success && (legacy is null || !legacy.Success)) continue;

            string type = m.Success ? TypeMap.GetValueOrDefault(m.Groups[1].Value.ToUpperInvariant(), "item") : legacyType;
            string value = (m.Success ? m.Groups[2].Value : legacy!.Groups[1].Value).Trim();

            var obj = new SObject { Id = value, Type = type, File = fileName, DisplayId = value };
            var block = new List<string>();
            i++;
            while (i < lines.Length && !lines[i].TrimStart().StartsWith('['))
            {
                block.Add(lines[i]);
                i++;
            }
            i--;
            ReadBlock(obj, block);
            result.Add(obj);
        }

        ResolveDupes(result);
        return result;
    }

    private static void ReadBlock(SObject obj, List<string> block)
    {
        foreach (var raw in block)
        {
            var clean = raw.Split("//", 2)[0];
            var idx = clean.IndexOf('=');
            if (idx < 0) continue;
            var key = clean[..idx].Trim().ToUpperInvariant();
            var value = clean[(idx + 1)..].Trim();
            switch (key)
            {
                case "CATEGORY": obj.Category = value; break;
                case "SUBSECTION": obj.Subsection = value; break;
                case "DESCRIPTION": obj.Description = value; break;
                case "DUPEITEM": obj.DupeItem = value; break;
                case "DUPELIST": obj.DupeList = value; break;
                case "ID": obj.DisplayId = value; break;
                case "COLOR": obj.Color = value; break;
                case "NAME":
                    obj.Name = value;
                    if (string.IsNullOrEmpty(obj.Description)) obj.Description = value;
                    break;
                case "DEFNAME": obj.Defname = value; break;
                case "TYPE": obj.ScriptType = value; break;
                case "ANIM": obj.Anim = value; break;
                case "ICON": obj.Icon = value; break;
                case "SOUND": obj.Sound = value; break;
            }
        }
    }

    private static void ResolveDupes(List<SObject> objects)
    {
        var byId = new Dictionary<string, SObject>(StringComparer.OrdinalIgnoreCase);
        var byHex = new Dictionary<long, SObject>();
        foreach (var o in objects)
        {
            byId.TryAdd(o.Id, o);
            var hv = HexValue(o.Id);
            if (hv is not null) byHex.TryAdd(hv.Value, o);
        }

        var extra = new List<SObject>();
        foreach (var o in objects)
        {
            if (!string.IsNullOrEmpty(o.DupeList))
            {
                foreach (var part in o.DupeList.Split(','))
                {
                    var dupeId = part.Trim();
                    if (dupeId.Length == 0) continue;
                    var clone = o.Clone();
                    clone.Id = dupeId;
                    clone.DisplayId = dupeId;
                    clone.DupeList = null;
                    extra.Add(clone);
                }
            }
            else if (!string.IsNullOrEmpty(o.DupeItem))
            {
                if (!byId.TryGetValue(o.DupeItem, out var parent))
                {
                    var hv = HexValue(o.DupeItem);
                    if (hv is not null) byHex.TryGetValue(hv.Value, out parent);
                }
                if (parent is not null)
                {
                    if (string.IsNullOrEmpty(o.Category)) o.Category = parent.Category;
                    if (string.IsNullOrEmpty(o.Subsection)) o.Subsection = parent.Subsection;
                    if (string.IsNullOrEmpty(o.Description)) o.Description = parent.Description;
                }
            }
        }
        objects.AddRange(extra);
    }

    /// <summary>Applies display defaults and computes the numeric id.</summary>
    public static void Normalize(SObject o)
    {
        if (string.IsNullOrEmpty(o.Category) || o.Category == "<none>") o.Category = "<uncategorized>";
        if (string.IsNullOrEmpty(o.Subsection) || o.Subsection == "<none>")
            o.Subsection = Path.GetFileNameWithoutExtension(o.File);
        if (string.IsNullOrEmpty(o.Description) || o.Description == "<unnamed>") o.Description = o.Id;
        o.NumericId = HexValue(string.IsNullOrEmpty(o.DisplayId) ? o.Id : o.DisplayId);
    }
}
