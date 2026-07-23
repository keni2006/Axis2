using System.Text.RegularExpressions;

namespace AxisSphere51.Server;

/// <summary>
/// Parses Sphere 0.51a map/region scripts (AREADEF / ROOMDEF blocks) and [SPELL] blocks,
/// producing exactly the same geometry the desktop Axis Travel/Misc tabs build from local
/// scripts. Rooms declared directly under an AREADEF are nested inside that area; rooms that
/// stand on their own become top-level regions — mirroring the desktop ScriptParser.
/// </summary>
public static partial class TravelParser
{
    // Sphere 0.51a ("The Abyss") stores regions as [AREA name] / [ROOM name]; newer scripts use
    // [AREADEF name] / [ROOMDEF name]. Accept both. (Longer keywords first so AREADEF wins over AREA.)
    [GeneratedRegex(@"^\[(AREADEF|ROOMDEF|AREA|ROOM)\s+([^\]]+)\]$", RegexOptions.IgnoreCase)]
    private static partial Regex RegionHeader();

    [GeneratedRegex(@"(?m)^\s*\[SPELL\s*(\d+)\]\s*$(.*?)(?=^\s*\[SPELL\s*\d+\]|\Z)", RegexOptions.Singleline)]
    private static partial Regex SpellBlock();

    [GeneratedRegex(@"^\s*(DEFNAME|NAME|RESOURCES)=(.*)$\s*", RegexOptions.Multiline)]
    private static partial Regex SpellProp();

    [GeneratedRegex(@"(?m)^\s*\[SKILL\s+(\d+)\]\s*(?://.*)?$(.*?)(?=^\s*\[|\Z)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex SkillBlock();

    [GeneratedRegex(@"^\s*(KEY|TITLE)\s*=\s*(.*)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex SkillProp();

    private sealed class Region
    {
        public string Kind = "area";
        public string Name = "";
        public string Group = "";
        public string DefName = "";
        public int Map;
        public int X, Y, Z;
        public List<RectDto> Rects = new();
        public List<Region> Rooms = new();

        public RegionDto ToDto() => new(
            Kind, Name, Group, DefName, Map, X, Y, Z, Rects,
            Rooms.Select(r => r.ToDto()).ToList());
    }

    /// <summary>Parses one map script into areas (with nested rooms) and top-level rooms.</summary>
    public static List<RegionDto> ParseRegions(string path)
    {
        var result = new List<Region>();
        if (!File.Exists(path)) return new List<RegionDto>();

        var lines = File.ReadAllLines(path);
        Region? currentArea = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!(line.StartsWith('[') && line.EndsWith(']'))) continue;

            var m = RegionHeader().Match(line);
            if (!m.Success)
            {
                // Any other section header ends the current area context, just like the desktop.
                currentArea = null;
                continue;
            }

            var kind = m.Groups[1].Value.ToUpperInvariant().StartsWith("AREA") ? "area" : "room";
            var region = new Region { Kind = kind, Name = m.Groups[2].Value.Trim() };

            var block = new List<string>();
            i++;
            while (i < lines.Length && !lines[i].TrimStart().StartsWith('[')) { block.Add(lines[i]); i++; }
            i--;

            ReadRegionBlock(region, block);

            if (kind == "area")
            {
                currentArea = region;
                result.Add(region);
            }
            else if (currentArea != null)
            {
                currentArea.Rooms.Add(region); // room inside the current area (not a top-level region)
            }
            else
            {
                result.Add(region); // standalone room
            }
        }

        return result.Select(r => r.ToDto()).ToList();
    }

    private static void ReadRegionBlock(Region region, List<string> block)
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
                case "NAME": region.Name = value; break;
                case "GROUP": region.Group = value; break;
                case "DEFNAME": region.DefName = value; break;
                case "RECT":
                    {
                        var p = value.Split(',');
                        if (p.Length >= 4
                            && int.TryParse(p[0], out var x1) && int.TryParse(p[1], out var y1)
                            && int.TryParse(p[2], out var x2) && int.TryParse(p[3], out var y2))
                        {
                            if (p.Length >= 5 && int.TryParse(p[4], out var pm)) region.Map = pm;
                            int rx = Math.Min(x1, x2), ry = Math.Min(y1, y2);
                            int rw = Math.Abs(x2 - x1), rh = Math.Abs(y2 - y1);
                            region.Rects.Add(new RectDto(rx, ry, rw, rh));
                            // Centre point + Z, matching the desktop RECT handling.
                            region.X = rx + rw / 2;
                            region.Y = ry + rh / 2;
                            region.Z = (p.Length >= 5 && int.TryParse(p[4], out var pz)) ? pz : 0;
                        }
                        break;
                    }
                case "P":
                    {
                        var p = value.Split(',');
                        if (p.Length >= 2 && int.TryParse(p[0], out var px) && int.TryParse(p[1], out var py))
                        {
                            region.X = px;
                            region.Y = py;
                            region.Z = (p.Length >= 3 && int.TryParse(p[2], out var pz)) ? pz : 0;
                            region.Map = (p.Length >= 4 && int.TryParse(p[3], out var pm)) ? pm : 0;
                        }
                        break;
                    }
            }
        }
    }

    /// <summary>Parses [SPELL n] blocks (DEFNAME/NAME/RESOURCES) — same shape as the desktop SpellService.</summary>
    public static List<SpellDto> ParseSpells(string path)
    {
        var spells = new List<SpellDto>();
        if (!File.Exists(path)) return spells;

        var content = File.ReadAllText(path);
        foreach (Match block in SpellBlock().Matches(content))
        {
            if (!int.TryParse(block.Groups[1].Value, out var id)) continue;
            string defName = "", name = "", resources = "";
            foreach (Match prop in SpellProp().Matches(block.Groups[2].Value))
            {
                var v = prop.Groups[2].Value.Trim();
                switch (prop.Groups[1].Value.ToUpperInvariant())
                {
                    case "DEFNAME": defName = v; break;
                    case "NAME": name = v; break;
                    case "RESOURCES": resources = v; break;
                }
            }
            spells.Add(new SpellDto(id, defName, name, resources));
        }
        return spells;
    }

    /// <summary>Parses [SKILL n] blocks (KEY/TITLE) — same shape as the desktop SkillStatService.</summary>
    public static List<SkillDto> ParseSkills(string path)
    {
        var skills = new List<SkillDto>();
        if (!File.Exists(path)) return skills;

        var content = File.ReadAllText(path);
        foreach (Match block in SkillBlock().Matches(content))
        {
            if (!int.TryParse(block.Groups[1].Value, out var index)) continue;
            string key = "", title = "";
            foreach (Match prop in SkillProp().Matches(block.Groups[2].Value))
            {
                var v = prop.Groups[2].Value.Split("//", StringSplitOptions.None)[0].Trim();
                switch (prop.Groups[1].Value.ToUpperInvariant())
                {
                    case "KEY": key = v; break;
                    case "TITLE": title = v; break;
                }
            }
            if (!string.IsNullOrWhiteSpace(key))
                skills.Add(new SkillDto(index, key, title));
        }
        return skills;
    }
}
