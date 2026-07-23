using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AxisSphere51.Server;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AxisSphere51.Server.Tests;

/// <summary>Boots the real API host but points it at the throwaway fixture scripts.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public readonly ScriptFixture Fixture = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scripts:BaseDirectory"] = Fixture.Directory,
                ["Scripts:ItemFiles:0"] = ScriptFixture.ItemFile,
                ["Scripts:ItemFiles:1"] = "",
                ["Scripts:CharFiles:0"] = ScriptFixture.CharFile,
                ["Scripts:CharFiles:1"] = "",
                ["Scripts:MapFiles:0"] = ScriptFixture.MapFile,
                ["Scripts:MapFiles:1"] = "",
                ["Scripts:SpellFiles:0"] = ScriptFixture.SpellFile,
                ["Scripts:SpellFiles:1"] = "",
                ["Scripts:SkillFiles:0"] = ScriptFixture.SkillFile,
                ["Scripts:SkillFiles:1"] = "",
                ["Accounts:BaseDirectory"] = Fixture.Directory,
                ["Accounts:Files:0"] = ScriptFixture.AccountFile,
                ["Accounts:MinPlevel"] = "2",
            });
        });
    }

    /// <summary>A client pre-authenticated with the given Sphere login/password.</summary>
    public HttpClient ClientFor(string user, string password)
    {
        var client = CreateClient();
        var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{user}:{password}"));
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);
        return client;
    }

    /// <summary>The default GM (staff) client used by the functional tests.</summary>
    public HttpClient GmClient() => ClientFor("gm", "secret");

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) Fixture.Dispose();
    }
}

public class ApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public ApiTests(ApiFactory factory) => _client = factory.GmClient();

    [Fact]
    public async Task Health_returns_ok()
    {
        var res = await _client.GetAsync("/health");
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("ok", body);
    }

    [Fact]
    public async Task Stats_reports_fixture_counts()
    {
        var stats = await _client.GetFromJsonAsync<StatsDto>("/api/stats", Json);
        Assert.NotNull(stats);
        Assert.Equal(6, stats!.Items);
        Assert.Equal(2, stats.Npcs);
        Assert.Equal(3, stats.Areas);   // 1 AREADEF + 2 ROOMDEF land in the flat areas bucket
    }

    [Fact]
    public async Task Items_search_filters_by_description()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<SObject>>("/api/items?search=broadsword", Json);
        Assert.NotNull(page);
        Assert.Equal(1, page!.Total);
        Assert.Equal("Crystal broadsword", page.Items[0].Description);
    }

    [Fact]
    public async Task Item_by_id_supports_exact_and_hex_tolerant_lookup()
    {
        var exact = await _client.GetFromJsonAsync<SObject>("/api/items/0002", Json);
        Assert.Equal("Ankh (w) 1/2", exact!.Description);

        // "2" should resolve to 0002 via numeric fallback
        var tolerant = await _client.GetFromJsonAsync<SObject>("/api/items/2", Json);
        Assert.Equal("0002", tolerant!.Id);
    }

    [Fact]
    public async Task Unknown_item_returns_404()
    {
        var res = await _client.GetAsync("/api/items/zzzzz_nope");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Npcs_search_and_categories()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<SObject>>("/api/npcs?search=ogre", Json);
        Assert.Equal(1, page!.Total);
        Assert.Equal("ogre", page.Items[0].Description);

        var cats = await _client.GetFromJsonAsync<List<CategoryDto>>("/api/categories?kind=npc", Json);
        Assert.Contains(cats!, c => c.Name == "Monsters" && c.Count == 2);
    }

    [Fact]
    public async Task Paging_limits_the_result_window()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<SObject>>("/api/items?limit=2", Json);
        Assert.Equal(6, page!.Total);      // total unaffected by paging
        Assert.Equal(2, page.Items.Count); // window respected
    }

    [Fact]
    public async Task Swagger_ui_is_served()
    {
        var res = await _client.GetAsync("/swagger/v1/swagger.json");
        res.EnsureSuccessStatusCode();
        Assert.Contains("Axis Sphere51", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Areas_are_served_from_the_map_file()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<SObject>>("/api/areas", Json);
        Assert.Equal(3, page!.Total);   // AREADEF + 2 ROOMDEF (flat list)
        Assert.Contains(page.Items, a => a.Description == "Test Town");
    }

    [Fact]
    public async Task Item_categories_group_and_count_subsections()
    {
        var cats = await _client.GetFromJsonAsync<List<CategoryDto>>("/api/categories?kind=item", Json);
        // 0002 + DUPEITEM 0003 + DUPELIST clones 03,04 all land in Decoration - Miscellaneous
        var deco = cats!.Single(c => c.Name == "Decoration - Miscellaneous");
        Assert.Equal(4, deco.Count);
        Assert.Contains(deco.Subsections, s => s.Name == "Statues" && s.Count == 4);

        var weapons = cats!.Single(c => c.Name == "Provisions - weapons");
        Assert.Equal(2, weapons.Count);
    }

    [Fact]
    public async Task Paging_offset_skips_the_leading_window()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<SObject>>("/api/items?offset=5&limit=10", Json);
        Assert.Equal(6, page!.Total);        // total is the full set
        Assert.Equal(5, page.Offset);
        Assert.Single(page.Items);           // only one item left past offset 5
    }

    [Fact]
    public async Task Reload_reparses_and_returns_fresh_stats()
    {
        var res = await _client.PostAsync("/api/reload", null);
        res.EnsureSuccessStatusCode();
        var stats = await res.Content.ReadFromJsonAsync<StatsDto>(Json);
        Assert.Equal(6, stats!.Items);
        Assert.Equal(2, stats.Npcs);
        Assert.Equal(3, stats.Areas);
        Assert.NotEmpty(stats.Files);
    }

    [Fact]
    public async Task Regions_return_area_with_nested_room_and_standalone_room()
    {
        var regions = await _client.GetFromJsonAsync<List<RegionDto>>("/api/regions", Json);
        Assert.NotNull(regions);

        // One area (Test Town) with the inn nested inside it, plus one standalone room.
        var area = regions!.Single(r => r.Kind == "area" && r.Name == "Test Town");
        Assert.Equal("Towns", area.Group);
        Assert.Single(area.Rects);
        Assert.Equal(new RectDto(100, 200, 40, 40), area.Rects[0]); // normalised x,y,w,h
        Assert.Equal(120, area.X);                                  // centre = 100 + 40/2
        Assert.Equal(220, area.Y);                                  // centre = 200 + 40/2
        var inn = Assert.Single(area.Rooms);
        Assert.Equal("Test Inn", inn.Name);
        Assert.Equal("r_test_inn", inn.DefName);

        var lonely = regions!.Single(r => r.Kind == "room" && r.Name == "Lonely Room");
        Assert.Equal("Misc", lonely.Group);
        Assert.Equal(1, lonely.Map);   // from P=500,600,5,1
        Assert.Equal(5, lonely.Z);
        Assert.Equal(500, lonely.X);
    }

    [Fact]
    public async Task Regions_filter_by_map_number()
    {
        var onMap1 = await _client.GetFromJsonAsync<List<RegionDto>>("/api/regions?map=1", Json);
        Assert.Single(onMap1!);
        Assert.Equal("Lonely Room", onMap1![0].Name);
    }

    [Fact]
    public async Task Spells_are_parsed_with_defname_and_resources()
    {
        var spells = await _client.GetFromJsonAsync<List<SpellDto>>("/api/spells", Json);
        Assert.Equal(2, spells!.Count);
        var clumsy = spells!.Single(s => s.Id == 1);
        Assert.Equal("s_clumsy", clumsy.DefName);
        Assert.Equal("Clumsy", clumsy.Name);
        Assert.Equal("1 i_reag_bone", clumsy.Resources);

        var filtered = await _client.GetFromJsonAsync<List<SpellDto>>("/api/spells?search=food", Json);
        Assert.Single(filtered!);
        Assert.Equal("Create Food", filtered![0].Name);
    }

    [Fact]
    public async Task Skills_are_parsed_with_key_and_title()
    {
        var skills = await _client.GetFromJsonAsync<List<SkillDto>>("/api/skills", Json);
        Assert.NotNull(skills);

        // [SKILLCLASS 0] is ignored; [SKILL 99] has no KEY so it's skipped -> 2 real skills.
        Assert.Equal(2, skills!.Count);
        Assert.DoesNotContain(skills, s => s.Title == "NoKey");

        var magery = skills!.Single(s => s.Index == 25);
        Assert.Equal("Magery", magery.Key);   // trailing // comment stripped
        Assert.Equal("Mage", magery.Title);
    }

    [Fact]
    public async Task Skills_filter_by_search()
    {
        var filtered = await _client.GetFromJsonAsync<List<SkillDto>>("/api/skills?search=alch", Json);
        Assert.Single(filtered!);
        Assert.Equal("Alchemy", filtered![0].Key);
    }
}

public class AuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public AuthTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_is_public()
    {
        var res = await _factory.CreateClient().GetAsync("/health");
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task No_credentials_are_rejected_401()
    {
        var res = await _factory.CreateClient().GetAsync("/api/stats");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_rejected_401()
    {
        var res = await _factory.ClientFor("gm", "WRONG").GetAsync("/api/stats");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Player_below_min_plevel_is_forbidden_403()
    {
        // "player" account has no PLEVEL line -> Player (1), below the MinPlevel of 2
        var res = await _factory.ClientFor("player", "pw").GetAsync("/api/stats");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Gm_above_min_plevel_is_allowed_and_me_reports_plevel()
    {
        var client = _factory.ClientFor("gm", "secret");

        var stats = await client.GetAsync("/api/stats");
        Assert.Equal(HttpStatusCode.OK, stats.StatusCode);

        var me = await client.GetFromJsonAsync<MeDto>("/api/me",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal("gm", me!.Name);
        Assert.Equal(4, me.Plevel);
    }

    private record MeDto(string Name, int Plevel);
}
