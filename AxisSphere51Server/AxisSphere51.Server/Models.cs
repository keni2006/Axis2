using System.Text.Json.Serialization;

namespace AxisSphere51.Server;

/// <summary>A parsed Sphere 0.51a object (item, NPC, area, …).</summary>
public class SObject
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "item";
    public string File { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string Subsection { get; set; } = "";
    public string DisplayId { get; set; } = "";
    public string? Color { get; set; }
    public string? Defname { get; set; }
    public string? ScriptType { get; set; }

    // character extras
    public string? Name { get; set; }
    public string? Anim { get; set; }
    public string? Icon { get; set; }
    public string? Sound { get; set; }

    /// <summary>Numeric hex value of the tile / body id, when applicable.</summary>
    public long? NumericId { get; set; }

    // parse-only helpers (never serialised)
    [JsonIgnore] public string? DupeList { get; set; }
    [JsonIgnore] public string? DupeItem { get; set; }

    public SObject Clone() => (SObject)MemberwiseClone();
}

public record SubCategoryDto(string Name, int Count);

public record CategoryDto(string Name, int Count, IReadOnlyList<SubCategoryDto> Subsections);

public record PagedResult<T>(int Total, int Offset, int Limit, IReadOnlyList<T> Items);

public record StatsDto(int Items, int Npcs, int Areas, int Categories, IReadOnlyList<string> Files);

/// <summary>An axis-relative rectangle (top-left + size), as the Travel tab consumes it.</summary>
public record RectDto(int X, int Y, int W, int H);

/// <summary>
/// A parsed Sphere region (AREADEF/ROOMDEF) with the same geometry the desktop Travel tab
/// builds locally: group, map, centre point, Z, rectangles and (for areas) nested rooms.
/// </summary>
public record RegionDto(
    string Kind,        // "area" or "room"
    string Name,
    string Group,
    string DefName,
    int Map,
    int X,              // centre point X (P)
    int Y,              // centre point Y (P)
    int Z,
    IReadOnlyList<RectDto> Rects,
    IReadOnlyList<RegionDto> Rooms);   // populated for areas, empty for rooms

/// <summary>A parsed [SPELL n] block for the Misc tab.</summary>
public record SpellDto(int Id, string DefName, string Name, string Resources);

/// <summary>Options bound from configuration ("Scripts" section).</summary>
public class ScriptOptions
{
    public string BaseDirectory { get; set; } = "";
    public List<string> ItemFiles { get; set; } = new();
    public List<string> CharFiles { get; set; } = new();
    public List<string> MapFiles { get; set; } = new();
    /// <summary>Files scanned for [SPELL n] blocks (default: spherespell.scp).</summary>
    public List<string> SpellFiles { get; set; } = new() { "spherespell.scp" };
}
