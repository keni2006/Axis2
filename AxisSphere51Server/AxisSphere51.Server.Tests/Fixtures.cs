namespace AxisSphere51.Server.Tests;

/// <summary>
/// Creates a throwaway directory of known Sphere 0.51a fixture scripts so the
/// tests are deterministic and don't depend on any real shard installation.
/// </summary>
public sealed class ScriptFixture : IDisposable
{
    public string Directory { get; }

    public const string ItemFile = "sphereitem_test.scp";
    public const string CharFile = "spherechar_test.scp";
    public const string MapFile = "spheremap_test.scp";
    public const string AccountFile = "accounts_test.scp";
    public const string SpellFile = "spherespell_test.scp";

    private const string Items = """
        // fixture items
        [0002]
        CATEGORY=Decoration - Miscellaneous
        SUBSECTION=Statues
        DESCRIPTION=Ankh (w) 1/2
        DUPELIST=03,04

        [0003]
        DUPEITEM=002
        DESCRIPTION=Ankh (w) 2/2

        [06030]
        CATEGORY=Provisions - weapons
        SUBSECTION=Swords
        DESCRIPTION=Crystal broadsword

        [ITEMDEF i_test_sword]
        CATEGORY=Provisions - weapons
        SUBSECTION=Swords
        DESCRIPTION=Test Sword
        ID=060a
        """;

    private const string Chars = """
        // fixture chars
        [0001]
        NAME=ogre
        CATEGORY=Monsters
        ANIM=078c7f

        [0100]
        NAME=orc
        CATEGORY=Monsters
        """;

    // A standalone room must precede any AREADEF, otherwise (like the desktop parser) it nests
    // into the current area. r_test_inn follows the AREADEF, so it nests into a_test_town.
    private const string Maps = """
        [ROOMDEF r_lonely_room]
        NAME=Lonely Room
        GROUP=Misc
        P=500,600,5,1

        [AREADEF a_test_town]
        NAME=Test Town
        GROUP=Towns
        RECT=100,200,140,240,0

        [ROOMDEF r_test_inn]
        NAME=Test Inn
        DEFNAME=r_test_inn
        RECT=110,210,120,220
        """;

    private const string Spells = """
        [SPELL 1]
        DEFNAME=s_clumsy
        NAME=Clumsy
        RESOURCES=1 i_reag_bone

        [SPELL 2]
        DEFNAME=s_create_food
        NAME=Create Food
        """;

    // gm = staff (PLEVEL 4); player = no PLEVEL line -> Player (1)
    private const string Accounts = """
        [gm]
        PASSWORD=secret
        PLEVEL=4

        [player]
        PASSWORD=pw
        """;

    public ScriptFixture()
    {
        Directory = Path.Combine(Path.GetTempPath(), "axis_sphere51_tests_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(Path.Combine(Directory, ItemFile), Items);
        File.WriteAllText(Path.Combine(Directory, CharFile), Chars);
        File.WriteAllText(Path.Combine(Directory, MapFile), Maps);
        File.WriteAllText(Path.Combine(Directory, SpellFile), Spells);
        File.WriteAllText(Path.Combine(Directory, AccountFile), Accounts);
    }

    public string FilePath(string file) => System.IO.Path.Combine(Directory, file);

    public void Dispose()
    {
        try { System.IO.Directory.Delete(Directory, recursive: true); } catch { }
    }
}
