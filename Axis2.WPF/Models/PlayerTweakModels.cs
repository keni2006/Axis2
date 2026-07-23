namespace Axis2.WPF.Models
{
    /// <summary>
    /// A skill parsed from a Sphere <c>[SKILL n]</c> block (found in spheretables.scp).
    /// Only KEY (the command/property name, e.g. "Magery") and TITLE are needed to drive the picker.
    /// </summary>
    public class SkillDef
    {
        public int Index { get; set; }
        public string Key { get; set; } = "";
        public string Title { get; set; } = "";

        public override string ToString() => Key;
    }

    /// <summary>A settable character stat (STR/DEX/INT/…): the Sphere command key + a friendly label.</summary>
    public class StatDef
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";

        public StatDef() { }
        public StatDef(string key, string label) { Key = key; Label = label; }

        public override string ToString() => Label;
    }

    /// <summary>An NPC AI brain type (mirrors the server's NPCBRAIN_TYPE enum).</summary>
    public class BrainDef
    {
        public int Value { get; set; }
        public string Name { get; set; } = "";

        public BrainDef() { }
        public BrainDef(int value, string name) { Value = value; Name = name; }

        public override string ToString() => Name;
    }
}
