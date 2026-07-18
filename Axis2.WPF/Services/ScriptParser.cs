using Axis2.WPF.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Windows;

namespace Axis2.WPF.Services
{
    public class ScriptParser
    {
        private readonly object _mapLock = new object();

        // Serializes the (background) parse+categorize work across tabs so ScriptParser's shared
        // state (DefNameToObjectMap, parse cache) is never touched from two threads at once. Tabs
        // wrap their loading in Task.Run + lock(LoadGate) to keep the UI thread free while loading.
        public readonly object LoadGate = new object();

        public Dictionary<string, SObject> DefNameToObjectMap { get; private set; }

        // Item/Spawn/Travel tabs each parse the SAME selected scripts on every profile load, which
        // meant reading + string-splitting all ~66 files three times — the main source of the
        // transient-string memory storm. Cache the parsed result per (path, last-write-time) so the
        // file is read and parsed once; the tabs consume disjoint SObjectType subsets, so sharing the
        // list is safe. Invalidated automatically when a script is edited (mtime changes).
        private readonly Dictionary<string, (System.DateTime Mtime, List<SObject> Result)> _parseCache
            = new(StringComparer.OrdinalIgnoreCase);

        public List<string> ItemTypes { get; private set; }
        public List<string> ItemProps { get; private set; }
        public List<string> ItemTags { get; private set; }

        public ScriptParser()
        {
            DefNameToObjectMap = new Dictionary<string, SObject>(StringComparer.OrdinalIgnoreCase);

            // Initialisation des listes avec des valeurs par défaut ou extraites si possible
            ItemTypes = new List<string> { "ITEMDEF", "MULTIDEF", "TEMPLATE", "CHARDEF", "NPC" };
            ItemProps = new List<string> { "DAM", "ARMOR", "WEIGHT", "VALUE", "SKILL", "STR", "DEX", "INT" }; // Exemples de proprits
            ItemTags = new List<string> { "TAG.CUSTOM", "TAG.NPC", "TAG.QUEST" }; // Exemples de tags
        }

        public List<SObject> ParseFile(string filePath)
        {
            var items = new List<SObject>();
            if (!File.Exists(filePath))
            {
                return items;
            }

            // Sphere account / world-save files (e.g. anything under an "accounts" folder, or
            // sphereaccu*/sphereacct*/sphereb##a backups) are NOT builder definitions. Parsing them
            // creates tens of thousands of throwaway objects that get retained in DefNameToObjectMap
            // and balloon memory to gigabytes. Skip them entirely.
            if (IsNonDefinitionFile(filePath))
            {
                Logger.Log($"ScriptParser: skipping non-definition (account/save) file: {Path.GetFileName(filePath)}");
                return items;
            }

            // Return the cached parse if the file hasn't changed since — avoids re-reading and
            // re-parsing the same file for every tab.
            var mtime = File.GetLastWriteTimeUtc(filePath);
            lock (_mapLock)
            {
                if (_parseCache.TryGetValue(filePath, out var cached) && cached.Mtime == mtime)
                    return cached.Result;
            }

            var lines = File.ReadAllLines(filePath);
            AreaDefinition currentArea = null;

            // Legacy Sphere 0.51a bare-hex blocks ([0001]) carry no keyword, so item vs. character
            // is inferred from the file name: spherechar*.scp holds CHARDEFs, everything else ITEMDEFs.
            string fileName = Path.GetFileName(filePath).ToLowerInvariant();
            SObjectType legacyType = fileName.Contains("char") ? SObjectType.Npc : SObjectType.Item;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    // Sphere 0.51a ("The Abyss") stores regions as [AREA name] / [ROOM name]; newer
                    // scripts use [AREADEF] / [ROOMDEF]. Match both (longer keywords first).
                    var match = Regex.Match(line, @"\[(ITEMDEF|MULTIDEF|TEMPLATE|CHARDEF|SPAWN|AREADEF|ROOMDEF|AREA|ROOM)\s+([^\]]+)\]", RegexOptions.IgnoreCase);
                    // Sphere 0.51a (legacy "The Abyss") blocks use a bare hex id as the header,
                    // e.g. [0000] or [04FE], with no keyword. Treat those per the file's legacyType.
                    var legacyMatch = match.Success ? null : Regex.Match(line, @"^\[\s*(?:0x)?([0-9A-Fa-f]{1,5})\s*\]$", RegexOptions.IgnoreCase);
                    if (match.Success || (legacyMatch != null && legacyMatch.Success))
                    {
                        var objectType = match.Success ? GetObjectType(match.Groups[1].Value) : legacyType;

                        // If we encounter a new Area or any other main section, reset the current area context.
                        if (objectType != SObjectType.Room)
                        {
                            currentArea = null;
                        }

                        var item = new SObject
                        {
                            FileName = Path.GetFileName(filePath),
                            Type = objectType
                        };

                        string value = (match.Success ? match.Groups[2].Value : legacyMatch.Groups[1].Value).Trim();
                        item.Id = value; // DEFNAME from header
                        item.Value = value;
                        item.DisplayId = value; // Default DisplayId is the DEFNAME

                        var blockLines = new List<string>();
                        i++;
                        while (i < lines.Length && !lines[i].Trim().StartsWith("["))
                        {
                            blockLines.Add(lines[i]);
                            i++;
                        }
                        i--;

                        ReadBlock(item, blockLines);

                        bool isRoomInArea = false; // Flag to prevent adding room to main list
                        if (item.Type == SObjectType.Area && item.Region is AreaDefinition area)
                        {
                            currentArea = area;
                        }
                        else if (item.Type == SObjectType.Room && currentArea != null && item.Region is RoomDefinition room)
                        {
                            // This is a room inside an area, add it to the area's collection.
                            currentArea.Rooms.Add(room);
                            isRoomInArea = true; // Mark as handled
                            // We don't add this room to the main 'items' list to avoid duplication in the TreeView's top level.
                        }

                        lock (_mapLock)
                        {
                            if (!DefNameToObjectMap.ContainsKey(item.Id))
                            {
                                DefNameToObjectMap[item.Id] = item;
                            }

                            if (!string.IsNullOrEmpty(item.ExplicitDefName) && !DefNameToObjectMap.ContainsKey(item.ExplicitDefName))
                            {
                                DefNameToObjectMap[item.ExplicitDefName] = item;
                            }
                        }

                        if (!isRoomInArea && !item.Category.StartsWith("$"))
                        {
                            items.Add(item);
                        }
                    }
                }
            }

            // Process DUPELIST and DUPEITEM after all main items are parsed.
            // Build a hex-value index once so DUPEITEM resolution is O(1) instead of scanning
            // the whole map per reference (the legacy Sphere 0.51a files are dupe-heavy).
            var hexIndex = new Dictionary<uint, SObject>();
            lock (_mapLock)
            {
                foreach (var kvp in DefNameToObjectMap)
                {
                    if (TryParseHex(kvp.Key, out uint hv) && !hexIndex.ContainsKey(hv))
                        hexIndex[hv] = kvp.Value;
                }
            }

            var additionalItems = new List<SObject>();
            foreach (var obj in items.ToList()) // Iterate over a copy to allow modification of original list
            {
                if (!string.IsNullOrEmpty(obj.DupeList))
                {
                    var dupeIds = obj.DupeList.Split(',').Select(id => id.Trim()).ToList();
                    foreach (var dupeId in dupeIds)
                    {
                        var dupeObject = obj.Clone() as SObject;
                        if (dupeObject != null)
                        {
                            dupeObject.Id = dupeId; // Set the new ID
                            dupeObject.DisplayId = dupeId; // Set the new DisplayId
                            additionalItems.Add(dupeObject);
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(obj.DupeItem))
                {
                    // Direct string lookup first; fall back to hex-value match so Sphere 0.51a
                    // references like DUPEITEM=002 resolve to the [0002] block despite differing width.
                    if (!DefNameToObjectMap.TryGetValue(obj.DupeItem, out var originalItem))
                    {
                        if (TryParseHex(obj.DupeItem, out uint dupeHex))
                            hexIndex.TryGetValue(dupeHex, out originalItem);
                    }
                    if (originalItem != null)
                    {
                        var dupeObject = originalItem.Clone() as SObject;
                        if (dupeObject != null)
                        {
                            dupeObject.Id = obj.Id; // Keep the ID from the DUPEITEM block
                            dupeObject.DisplayId = obj.Id; // Keep the DisplayId from the DUPEITEM block
                            additionalItems.Add(dupeObject);
                        }
                    }
                }
            }
            items.AddRange(additionalItems);

            lock (_mapLock)
            {
                _parseCache[filePath] = (mtime, items);
            }
            return items;
        }

        public List<Category> Categorize(List<SObject> items)
        {
            ResolveObjectDisplayIds(items);

            var categoryDict = new Dictionary<string, Category>();

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Category) || item.Category == "<none>")
                {
                    item.Category = "<uncategorized Items>";
                }
                if (string.IsNullOrEmpty(item.SubSection) || item.SubSection == "<none>")
                {
                    item.SubSection = item.FileName;
                }
                if (string.IsNullOrEmpty(item.Description) || item.Description == "<unnamed>")
                {
                    item.Description = item.Id;
                }

                item.Category = Capitalize(item.Category);
                item.SubSection = Capitalize(item.SubSection);
                item.Description = Capitalize(item.Description);

                if (!categoryDict.TryGetValue(item.Category, out var category))
                {
                    category = new Category { Name = item.Category };
                    categoryDict[item.Category] = category;
                }

                var subCategory = category.SubSections.FirstOrDefault(s => s.Name == item.SubSection);
                if (subCategory == null)
                {
                    subCategory = new SubCategory { Name = item.SubSection };
                    category.SubSections.Add(subCategory);
                }

                subCategory.Items.Add(item);
            }

            foreach (var category in categoryDict.Values)
            {
                var sortedSubSections = new ObservableCollection<SubCategory>(category.SubSections.OrderBy(s => s.Name));
                category.SubSections = sortedSubSections;
                foreach (var subCategory in category.SubSections)
                {
                    var sortedItems = new ObservableCollection<SObject>(subCategory.Items.OrderBy(i => i.Description));
                    subCategory.Items = sortedItems;
                }
            }

            return categoryDict.Values.OrderBy(c => c.Name).ToList();
        }

        private void ReadBlock(SObject item, List<string> blockLines)
        {
            if (item.Type == SObjectType.SpawnGroup)
            {
                item.Group ??= new SpawnGroup();
                item.Group.Name = item.Id;
            }
            else if (item.Type == SObjectType.Area)
            {
                item.Region = new AreaDefinition();
                item.Region.Name = item.Id;
            }
            else if (item.Type == SObjectType.Room)
            {
                item.Region = new RoomDefinition();
                item.Region.Name = item.Id;
            }

            foreach (var line in blockLines)
            {
                var cleanLine = line.Split(new[] { "//" }, StringSplitOptions.None)[0];
                var parts = cleanLine.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                {
                    if (item.Type == SObjectType.SpawnGroup)
                    {
                        // Handle bare IDs for spawn groups
                        var trimmedLine = cleanLine.Trim();
                        if (!string.IsNullOrEmpty(trimmedLine))
                        {
                            var idParts = trimmedLine.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                            if (idParts.Length > 0)
                            {
                                var entry = new SpawnEntry { DefName = idParts[0] };
                                if (idParts.Length > 1 && int.TryParse(idParts[1], out int amount))
                                {
                                    entry.Amount = amount;
                                }
                                else
                                {
                                    entry.Amount = 1;
                                }
                                item.Group.SpawnEntries.Add(entry);
                            }
                        }
                    }
                    continue;
                }

                var key = parts[0].Trim().ToUpper();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "CATEGORY":
                        item.Category = value;
                        break;
                    case "SUBSECTION":
                        item.SubSection = value;
                        break;
                    case "DESCRIPTION":
                        item.Description = value;
                        break;
                    case "DUPEITEM":
                        item.DupeItem = value;
                        break;
                    case "DUPELIST":
                        item.DupeList = value;
                        break;
                    case "ID":
                        if (item.Type == SObjectType.SpawnGroup)
                        {
                            var idParts = value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                            if (idParts.Length > 0)
                            {
                                var entry = new SpawnEntry { DefName = idParts[0] };
                                if (idParts.Length > 1 && int.TryParse(idParts[1], out int amount))
                                {
                                    entry.Amount = amount;
                                }
                                else
                                {
                                    entry.Amount = 1;
                                }
                                item.Group.SpawnEntries.Add(entry);
                            }
                        }
                        else
                        {
                            item.DisplayId = value;
                        }
                        break;
                    case "COLOR":
                        item.Color = value;
                        break;
                    case "NAME":
                        if (item.Region != null)
                        {
                            item.Region.Name = value;
                        }
                        item.Description = value;
                        break;
                    case "DEFNAME":
                        if (item.Region is RoomDefinition roomDef)
                        {
                            roomDef.DefName = value;
                        }
                        item.ExplicitDefName = value;
                        break;
                    case "TYPE":
                        item.ScriptType = value;
                        break;
                    case "GROUP":
                        if (item.Region != null)
                        {
                            item.Region.Group = value;
                        }
                        break;
                    case "RECT":
                        if (item.Region != null)
                        {
                            var rectParts = value.Split(',');
                            if (rectParts.Length >= 4)
                            {
                                int x1 = int.Parse(rectParts[0]);
                                int y1 = int.Parse(rectParts[1]);
                                int x2 = int.Parse(rectParts[2]);
                                int y2 = int.Parse(rectParts[3]);
                                item.Region.Map = (rectParts.Length >= 5 && int.TryParse(rectParts[4], out int parsedMap)) ? parsedMap : item.Region.Map;

                                int rectX = Math.Min(x1, x2);
                                int rectY = Math.Min(y1, y2);
                                int rectWidth = Math.Abs(x2 - x1);
                                int rectHeight = Math.Abs(y2 - y1);
                                item.Region.Rects.Add(new System.Windows.Rect(rectX, rectY, rectWidth, rectHeight));

                                // Populate P and Z from RECT
                                item.Region.P = new System.Windows.Point(rectX + rectWidth / 2, rectY + rectHeight / 2);
                                item.Region.Z = (rectParts.Length >= 5 && int.TryParse(rectParts[4], out int parsedZ)) ? parsedZ : 0;
                            }
                        }
                        break;
                    case "P":
                        if (item.Region != null)
                        {
                            var pParts = value.Split(',');
                            if (pParts.Length >= 2)
                            {
                                item.Region.P = new System.Windows.Point(int.Parse(pParts[0]), int.Parse(pParts[1]));
                                item.Region.Z = (pParts.Length >= 3 && int.TryParse(pParts[2], out int parsedZ)) ? parsedZ : 0;
                                item.Region.Map = (pParts.Length >= 4 && int.TryParse(pParts[3], out int parsedMap)) ? parsedMap : 0;
                            }
                        }
                        break;
                }
            }
        }

        // Sphere SAVE files (accounts, world, data, multis and the sphereb## backup family) are NOT
        // builder definitions — they're dumps of live server state, often huge (each can take many
        // seconds to parse and produces zero item/char defs). Skipping them is what keeps loading
        // fast and memory low. Real definition files (sphereitem*, spherechar*, spheremap*,
        // spherespell*, spheredef*, …) are never matched here.
        private static bool IsNonDefinitionFile(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath) ?? string.Empty;
            foreach (var seg in dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (string.Equals(seg, "accounts", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(seg, "save", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(seg, "saves", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            var name = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
            if (name.StartsWith("sphereworld") || name.StartsWith("spheredata") ||
                name.StartsWith("spheremultis") || name.StartsWith("sphereaccu") ||
                name.StartsWith("sphereacct") || name.StartsWith("spheregmpage"))
                return true;

            // "sphereb" followed by a digit = a save backup (sphereb01a = accounts, sphereb01w = world…).
            if (name.StartsWith("sphereb") && name.Length > 7 && char.IsDigit(name[7]))
                return true;

            return false;
        }

        // Parses a Sphere 0.51a bare-hex tile reference (variable width, optional 0x) to its numeric value.
        private static bool TryParseHex(string reference, out uint value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(reference))
                return false;
            string cleaned = reference.Trim();
            if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(2);
            return uint.TryParse(cleaned, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        private SObjectType GetObjectType(string typeStr)
        {
            return typeStr.ToUpper() switch
            {
                "ITEMDEF" => SObjectType.Item,
                "MULTIDEF" => SObjectType.Multi,
                "TEMPLATE" => SObjectType.Template,
                "CHARDEF" => SObjectType.Npc,
                "NPC" => SObjectType.Npc,
                "SPAWN" => SObjectType.SpawnGroup,
                "AREADEF" => SObjectType.Area,
                "ROOMDEF" => SObjectType.Room,
                "AREA" => SObjectType.Area,
                "ROOM" => SObjectType.Room,
                _ => SObjectType.None,
            };
        }

        private string Capitalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            return char.ToUpper(text[0]) + text.Substring(1).ToLower();
        }

        private void ResolveObjectDisplayIds(List<SObject> items)
        {
            foreach (var obj in items)
            {
                if (!IsNumericalId(obj.DisplayId))
                {
                    string resolvedId = ResolveChainedDefname(obj.DisplayId);
                    if (!string.IsNullOrEmpty(resolvedId))
                    {
                        obj.DisplayId = resolvedId;
                    }
                }
            }
        }

        private bool IsNumericalId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            return uint.TryParse(id, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint numId) && numId > 0;
        }

        private string ResolveChainedDefname(string defname)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var currentDefname = defname;

            while (currentDefname != null && visited.Add(currentDefname))
            {
                SObject obj;
                lock (_mapLock)
                {
                    if (!DefNameToObjectMap.TryGetValue(currentDefname, out obj))
                    {
                        // This link in the chain does not exist in our map
                        return null;
                    }
                }

                // The 'Id' property holds the value from the [SECTION Id] header.
                // If this value is a number, we've found the end of the chain.
                if (IsNumericalId(obj.Id))
                {
                    return obj.Id;
                }

                // If the Id is not numerical (e.g., [chardef c_guerrier]),
                // we must follow the 'id=' property, which is stored in DisplayId.
                if (obj.DisplayId.Equals(currentDefname, StringComparison.OrdinalIgnoreCase))
                {
                    // This object either has no 'id=' tag, or it points to itself.
                    // Since its own Id is not numerical, this is a dead end.
                    return null;
                }

                // Follow the chain to the next defname specified by the 'id=' tag.
                currentDefname = obj.DisplayId;
            }

            // If we exit the loop, it means we detected a circular reference (e.g. A->B, B->A).
            return null;
        }
    }
}
