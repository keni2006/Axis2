using AxisSphere51.Server;

namespace AxisSphere51.Server.Tests;

public class ParserTests : IClassFixture<ScriptFixture>
{
    private readonly ScriptFixture _fx;
    public ParserTests(ScriptFixture fx) => _fx = fx;

    [Fact]
    public void Parses_legacy_and_modern_item_headers()
    {
        var items = SphereParser.ParseFile(_fx.FilePath(ScriptFixture.ItemFile));
        items.ForEach(SphereParser.Normalize);

        // 0002, 0003, 06030, i_test_sword + DUPELIST clones 03 and 04 = 6
        Assert.Equal(6, items.Count);
        Assert.Contains(items, i => i.Id == "0002" && i.Description == "Ankh (w) 1/2");
        Assert.Contains(items, i => i.Id == "i_test_sword" && i.Description == "Test Sword");
        Assert.All(items, i => Assert.Equal("item", i.Type));
    }

    [Fact]
    public void Dupelist_creates_a_clone_per_id_inheriting_parent_fields()
    {
        var items = SphereParser.ParseFile(_fx.FilePath(ScriptFixture.ItemFile));

        var clone03 = items.Single(i => i.Id == "03");
        var clone04 = items.Single(i => i.Id == "04");
        foreach (var c in new[] { clone03, clone04 })
        {
            Assert.Equal("Decoration - Miscellaneous", c.Category);
            Assert.Equal("Statues", c.Subsection);
            Assert.Equal(c.Id, c.DisplayId);
        }
    }

    [Fact]
    public void Dupeitem_inherits_missing_fields_hex_width_tolerant()
    {
        var items = SphereParser.ParseFile(_fx.FilePath(ScriptFixture.ItemFile));

        // [0003] DUPEITEM=002 -> resolves to [0002] despite "002" vs "0002"
        var ankh2 = items.Single(i => i.Id == "0003");
        Assert.Equal("Decoration - Miscellaneous", ankh2.Category); // inherited
        Assert.Equal("Ankh (w) 2/2", ankh2.Description);           // its own value kept
    }

    [Fact]
    public void Numeric_id_is_hex_parsed_from_display_id()
    {
        var items = SphereParser.ParseFile(_fx.FilePath(ScriptFixture.ItemFile));
        items.ForEach(SphereParser.Normalize);

        Assert.Equal(0x6030, items.Single(i => i.Id == "06030").NumericId);
        Assert.Equal(0x060a, items.Single(i => i.Id == "i_test_sword").NumericId); // from ID=060a
    }

    [Fact]
    public void Char_file_blocks_become_npcs_with_name_as_description()
    {
        var npcs = SphereParser.ParseFile(_fx.FilePath(ScriptFixture.CharFile));
        npcs.ForEach(SphereParser.Normalize);

        Assert.Equal(2, npcs.Count);
        Assert.All(npcs, n => Assert.Equal("npc", n.Type));
        var ogre = npcs.Single(n => n.Id == "0001");
        Assert.Equal("ogre", ogre.Description);
        Assert.Equal("078c7f", ogre.Anim);
        Assert.Equal(1, ogre.NumericId); // body id
    }

    [Theory]
    [InlineData("0002", 2)]
    [InlineData("0x2", 2)]
    [InlineData("2", 2)]
    [InlineData("EOF", null)]
    public void HexValue_parses_width_and_prefix_variants(string input, int? expected)
    {
        Assert.Equal(expected, (int?)SphereParser.HexValue(input));
    }

    [Fact]
    public void Missing_file_returns_empty_without_throwing()
    {
        Assert.Empty(SphereParser.ParseFile(_fx.FilePath("does_not_exist.scp")));
    }

    [Fact]
    public void Parses_skill_blocks_ignoring_skillclass_and_keyless_blocks()
    {
        var skills = TravelParser.ParseSkills(_fx.FilePath(ScriptFixture.SkillFile));

        // [SKILLCLASS 0] is not a skill; [SKILL 99] has no KEY -> only Alchemy (0) and Magery (25).
        Assert.Equal(2, skills.Count);
        Assert.Contains(skills, s => s.Index == 0 && s.Key == "Alchemy" && s.Title == "Alchemist");
        var magery = skills.Single(s => s.Index == 25);
        Assert.Equal("Magery", magery.Key);   // trailing "// wizardry" comment stripped
        Assert.Equal("Mage", magery.Title);
    }

    [Fact]
    public void ParseSkills_missing_file_returns_empty()
    {
        Assert.Empty(TravelParser.ParseSkills(_fx.FilePath("does_not_exist.scp")));
    }
}
