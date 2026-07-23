using Microsoft.Extensions.Options;

namespace AxisSphere51.Server;

/// <summary>
/// Loads the configured Sphere 0.51a scripts once (thread-safe, cache-and-serve)
/// and answers queries for items, NPCs, areas and category trees.
/// </summary>
public class ScriptRepository
{
    private readonly ScriptOptions _options;
    private readonly ILogger<ScriptRepository> _logger;
    private readonly object _gate = new();

    private List<SObject> _items = new();
    private List<SObject> _npcs = new();
    private List<SObject> _areas = new();
    private List<RegionDto> _regions = new();
    private List<SpellDto> _spells = new();
    private List<SkillDto> _skills = new();
    private List<string> _loadedFiles = new();

    public ScriptRepository(IOptions<ScriptOptions> options, ILogger<ScriptRepository> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlyList<string> LoadedFiles => _loadedFiles;

    public void Reload()
    {
        var items = new List<SObject>();
        var npcs = new List<SObject>();
        var areas = new List<SObject>();
        var regions = new List<RegionDto>();
        var spells = new List<SpellDto>();
        var skills = new List<SkillDto>();
        var files = new List<string>();

        foreach (var (names, bucket) in new[]
        {
            (_options.ItemFiles, items),
            (_options.CharFiles, npcs),
            (_options.MapFiles, areas),
        })
        {
            foreach (var name in names)
            {
                var path = Path.IsPathRooted(name) ? name : Path.Combine(_options.BaseDirectory, name);
                if (!File.Exists(path))
                {
                    _logger.LogWarning("Script file not found: {Path}", path);
                    continue;
                }
                var parsed = SphereParser.ParseFile(path);
                foreach (var o in parsed)
                {
                    SphereParser.Normalize(o);
                    // route by parsed type so an item file with stray chardefs still lands right
                    switch (o.Type)
                    {
                        case "npc": npcs.Add(o); break;
                        case "area" or "room": areas.Add(o); break;
                        case "spawn": npcs.Add(o); break;
                        default: bucket.Add(o); break;
                    }
                }
                files.Add(name);
                _logger.LogInformation("Parsed {Count} objects from {Name}", parsed.Count, name);
            }
        }

        // Travel regions (with full geometry) come from the map files.
        foreach (var name in _options.MapFiles)
        {
            var path = Path.IsPathRooted(name) ? name : Path.Combine(_options.BaseDirectory, name);
            if (File.Exists(path)) regions.AddRange(TravelParser.ParseRegions(path));
        }

        // Spells come from the configured spell files.
        foreach (var name in _options.SpellFiles)
        {
            var path = Path.IsPathRooted(name) ? name : Path.Combine(_options.BaseDirectory, name);
            if (File.Exists(path))
            {
                spells.AddRange(TravelParser.ParseSpells(path));
                if (!files.Contains(name)) files.Add(name);
            }
            else
            {
                _logger.LogWarning("Spell file not found: {Path}", path);
            }
        }

        // Skills come from the configured skill files (spheretables.scp), de-duped by index.
        var skillByIndex = new Dictionary<int, SkillDto>();
        foreach (var name in _options.SkillFiles)
        {
            var path = Path.IsPathRooted(name) ? name : Path.Combine(_options.BaseDirectory, name);
            if (File.Exists(path))
            {
                foreach (var skill in TravelParser.ParseSkills(path))
                    skillByIndex[skill.Index] = skill;
                if (!files.Contains(name)) files.Add(name);
            }
            else
            {
                _logger.LogWarning("Skill file not found: {Path}", path);
            }
        }
        skills.AddRange(skillByIndex.Values.OrderBy(s => s.Index));

        lock (_gate)
        {
            _items = items;
            _npcs = npcs;
            _areas = areas;
            _regions = regions;
            _spells = spells;
            _skills = skills;
            _loadedFiles = files;
        }
    }

    public IReadOnlyList<RegionDto> Regions()
    {
        lock (_gate) return _regions.ToList();
    }

    public IReadOnlyList<SpellDto> Spells()
    {
        lock (_gate) return _spells.ToList();
    }

    public IReadOnlyList<SkillDto> Skills()
    {
        lock (_gate) return _skills.ToList();
    }

    private List<SObject> Bucket(string kind) => kind switch
    {
        "npc" => _npcs,
        "area" => _areas,
        _ => _items,
    };

    public PagedResult<SObject> Query(string kind, string? search, string? category,
        string? subsection, int offset, int limit)
    {
        IEnumerable<SObject> q;
        lock (_gate) q = Bucket(kind).ToList();

        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(o => string.Equals(o.Category, category, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(subsection))
            q = q.Where(o => string.Equals(o.Subsection, subsection, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(o =>
                (o.Description?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (o.Id?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var list = q.OrderBy(o => o.Description, StringComparer.OrdinalIgnoreCase).ToList();
        var page = list.Skip(Math.Max(0, offset)).Take(Math.Clamp(limit, 1, 5000)).ToList();
        return new PagedResult<SObject>(list.Count, offset, limit, page);
    }

    public SObject? ById(string kind, string id)
    {
        lock (_gate)
        {
            var bucket = Bucket(kind);
            var hit = bucket.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));
            if (hit is not null) return hit;
            var hv = SphereParser.HexValue(id);
            return hv is null ? null : bucket.FirstOrDefault(o => o.NumericId == hv);
        }
    }

    public IReadOnlyList<CategoryDto> Categories(string kind)
    {
        List<SObject> src;
        lock (_gate) src = Bucket(kind).ToList();

        return src.GroupBy(o => o.Category)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CategoryDto(
                g.Key,
                g.Count(),
                g.GroupBy(o => o.Subsection)
                 .OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase)
                 .Select(s => new SubCategoryDto(s.Key, s.Count()))
                 .ToList()))
            .ToList();
    }

    public StatsDto Stats()
    {
        lock (_gate)
        {
            var cats = _items.Select(i => i.Category)
                .Concat(_npcs.Select(n => n.Category))
                .Distinct(StringComparer.OrdinalIgnoreCase).Count();
            return new StatsDto(_items.Count, _npcs.Count, _areas.Count, cats, _loadedFiles);
        }
    }
}
