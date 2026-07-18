using Axis2.WPF.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;


namespace Axis2.WPF.Services
{
    public class MobTypesService
    {
        private readonly Dictionary<int, MobTypeInfo> _mobTypes = new Dictionary<int, MobTypeInfo>();

        // Regex to parse lines like: 689MONSTER0 #Time Lord# PUb 39 Fixes
        private static readonly Regex MobTypeRegex = new Regex(@"^(\d+)\s*([A-Z]+)\s*([A-Z0-9]+)(?:\s+\d+)?(?:\s*#(.+))?$", RegexOptions.Compiled);

        public void LoadMobTypes(string filePath)
        {
            _mobTypes.Clear();

            if (!File.Exists(filePath))
            {
                return;
            }

            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";"))
                {
                    continue; // Skip comments and empty lines
                }

                var match = MobTypeRegex.Match(line.Trim());
                if (match.Success)
                {
                    // Only show for the first few successful matches to avoid spamming
                    if (matchCount < 10)
                    {
                        matchCount++;
                    }
                    try
                    {
                        int id = int.Parse(match.Groups[1].Value);
                        string category = match.Groups[2].Value;
                        string subId = match.Groups[3].Value;
                        string name = match.Groups.Count > 4 && match.Groups[4].Success ? match.Groups[4].Value.Trim() : string.Empty;
                        string comment = string.Empty; // No separate comment group in new regex

                        var mobTypeInfo = new MobTypeInfo(id, category, subId, name, comment);
                        if (!_mobTypes.ContainsKey(id))
                        {
                            _mobTypes.Add(id, mobTypeInfo);
                        }
                    }
                    catch (FormatException)
                    {
                    }
                }
            }
        }

        private int matchCount = 0;

        public MobTypeInfo? GetMobType(int id)
        {
            _mobTypes.TryGetValue(id, out var mobTypeInfo);
            return mobTypeInfo;
        }

        public bool IsUopAnimation(int id)
        {
            var mobType = GetMobType(id);
            bool isUop = mobType?.IsUop ?? false;
            return isUop;
        }
    }
}