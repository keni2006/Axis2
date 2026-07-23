using Axis2.WPF.Models;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Axis2.WPF.Services
{
    /// <summary>
    /// Provides the reference data for the Player Tweak tab: the skill list (parsed from Sphere
    /// <c>[SKILL n]</c> blocks in spheretables.scp) plus the built-in stat and NPC-brain lists.
    /// </summary>
    public class SkillStatService
    {
        // A [SKILL n] block, e.g.:
        //   [SKILL 0]
        //   KEY=Alchemy
        //   TITLE=Alchemist
        //   ...
        // Capture the index and the block body up to the next [SECTION] header (or end of file).
        private static readonly Regex SkillBlock = new(
            @"(?m)^\s*\[SKILL\s+(\d+)\]\s*(?://.*)?$(.*?)(?=^\s*\[|\Z)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex SkillProp = new(
            @"^\s*(KEY|TITLE)\s*=\s*(.*)$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        /// <summary>Parses every <c>[SKILL n]</c> block found in the given script text.</summary>
        public List<SkillDef> ParseSkills(string scriptContent)
        {
            var skills = new List<SkillDef>();
            if (string.IsNullOrEmpty(scriptContent))
                return skills;

            foreach (Match block in SkillBlock.Matches(scriptContent))
            {
                if (!int.TryParse(block.Groups[1].Value, out int index))
                    continue;

                var skill = new SkillDef { Index = index };
                foreach (Match prop in SkillProp.Matches(block.Groups[2].Value))
                {
                    // Strip any trailing // comment and surrounding whitespace.
                    var value = prop.Groups[2].Value.Split(new[] { "//" }, System.StringSplitOptions.None)[0].Trim();
                    switch (prop.Groups[1].Value.ToUpperInvariant())
                    {
                        case "KEY": skill.Key = value; break;
                        case "TITLE": skill.Title = value; break;
                    }
                }

                if (!string.IsNullOrWhiteSpace(skill.Key))
                    skills.Add(skill);
            }

            return skills;
        }

        /// <summary>The character stats that can be set through the client console.</summary>
        public List<StatDef> GetStats() => new()
        {
            new("STR", "Strength (STR)"),
            new("DEX", "Dexterity (DEX)"),
            new("INT", "Intelligence (INT)"),
            new("HITS", "Hits"),
            new("MAXHITS", "Max Hits"),
            new("MANA", "Mana"),
            new("MAXMANA", "Max Mana"),
            new("STAM", "Stamina"),
            new("MAXSTAM", "Max Stamina"),
            new("FOOD", "Food"),
            new("KARMA", "Karma"),
            new("FAME", "Fame"),
            new("GOLD", "Gold"),
        };

        /// <summary>NPC AI brain types, mirroring the server's NPCBRAIN_TYPE enum.</summary>
        public List<BrainDef> GetBrains() => new()
        {
            new(1, "Animal"),
            new(2, "Human"),
            new(3, "Healer"),
            new(4, "Guard"),
            new(5, "Banker"),
            new(6, "Vendor"),
            new(7, "Beggar"),
            new(8, "Stable"),
            new(9, "Thief"),
            new(10, "Monster"),
            new(11, "Berserk"),
            new(12, "Undead"),
            new(13, "Dragon"),
            new(14, "Vendor (Off Duty)"),
            new(15, "Crier"),
            new(16, "Conjured"),
            new(17, "Stone Giant"),
            new(20, "Owner Healer"),
        };
    }
}
