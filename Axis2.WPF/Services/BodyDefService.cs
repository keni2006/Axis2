using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows; // Import pour MessageBox

namespace Axis2.WPF.Services
{
    public class BodyDef
    {
        public ushort OriginalId { get; set; }
        public ushort NewId { get; set; }
        public int Hue { get; set; }
        public int MulFile { get; set; }
    }

    public class BodyDefService
    {
        private readonly List<BodyDef> _bodyDefs = new List<BodyDef>();
        private readonly List<BodyDef> _bodyConv = new List<BodyDef>();

        public BodyDefService()
        {
            // Constructeur vide
        }

        public void Load(string bodyDefPath, string bodyConvPath)
        {
            _bodyDefs.Clear();
            _bodyConv.Clear();
            LoadBodyConv(bodyConvPath);
            LoadBodyDef(bodyDefPath);
        }

        public BodyDef? GetBodyDef(ushort id)
        {
            // Si l'ID existe dans bodyconv.def, on ignore body.def
            var bodyConvEntry = _bodyConv.FirstOrDefault(d => d.OriginalId == id);
            if (bodyConvEntry != null)
            {
                Logger.Log($"DEBUG: ID {id} exists in bodyconv.def, ignoring body.def");
                return null;  // Force à ignorer body.def
            }

            // Sinon on peut utiliser body.def
            return _bodyDefs.FirstOrDefault(d => d.OriginalId == id);
        }

        public BodyDef? GetBodyConv(ushort id)
        {
            Logger.Log($"DEBUG: GetBodyConv called for ID: {id}");
            foreach (var entry in _bodyConv)
            {
                Logger.Log($"DEBUG:   _bodyConv contains: OriginalId={entry.OriginalId}, NewId={entry.NewId}, MulFile={entry.MulFile}");
            }
            var result = _bodyConv.FirstOrDefault(d => d.OriginalId == id);
            if (result == null)
            {
                Logger.Log($"DEBUG: GetBodyConv for ID {id} returned NULL.");
            }
            else
            {
                Logger.Log($"DEBUG: GetBodyConv for ID {id} returned: NewId={result.NewId}, MulFile={result.MulFile}");
            }
            return result;
        }

        public ushort GetOriginalId(ushort transformedId)
        {
            Logger.Log($"DEBUG: GetOriginalId called with ID: {transformedId}");

            // 1. D'abord chercher dans la première colonne du bodyconv.def
            var bodyConvEntry = _bodyConv.FirstOrDefault(d => d.OriginalId == transformedId);
            if (bodyConvEntry != null)
            {
                Logger.Log($"DEBUG: ID {transformedId} found in bodyconv.def, ignoring body.def");
                return transformedId;  // On retourne l'ID tel quel et on IGNORE body.def
            }

            // 2. Seulement si pas dans bodyconv.def, chercher dans body.def
            var bodyDefEntry = _bodyDefs.FirstOrDefault(d => d.OriginalId == transformedId);
            if (bodyDefEntry != null)
            {
                Logger.Log($"DEBUG: ID {transformedId} found only in body.def, transforming to {bodyDefEntry.NewId}");
                return bodyDefEntry.NewId;
            }

            // 3. Sinon c'est un ID direct
            Logger.Log($"DEBUG: ID {transformedId} is a direct animation ID");
            return transformedId;
        }

        private void LoadBodyDef(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            if (!File.Exists(path))
            {
                return;
            }

            var lines = File.ReadAllLines(path);
            foreach (var line in lines)
            {
                var cleanLine = line.Split('#')[0].Trim();
                if (string.IsNullOrEmpty(cleanLine))
                {
                    continue;
                }

                var parts = cleanLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    continue;
                }

                if (ushort.TryParse(parts[0], out ushort originalId))
                {
                    string newIdString = parts[1].Trim();
                    // Handle cases like {200, 226}
                    if (newIdString.StartsWith("{") && newIdString.EndsWith("}"))
                    {
                        newIdString = newIdString.Trim('{', '}').Split(',')[0].Trim();
                    }

                    if (ushort.TryParse(newIdString, out ushort newId))
                    {
                        int hue = 0;
                        if (parts.Length > 2)
                        {
                            int.TryParse(parts[2], out hue);
                        }
                        _bodyDefs.Add(new BodyDef { OriginalId = originalId, NewId = newId, Hue = hue });
                    }
                }
            }
        }

        private void LoadBodyConv(string path)
        {
            Logger.Log($"DEBUG: Loading bodyconv from: {path}");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Logger.Log($"WARNING: bodyconv file not found or path is empty: {path}");
                return;
            }

            var lines = File.ReadAllLines(path);
            Logger.Log($"DEBUG: Found {lines.Length} lines in bodyconv file.");
            foreach (var line in lines)
            {
                var cleanLine = line.Split('#')[0].Trim();
                if (string.IsNullOrEmpty(cleanLine))
                {
                    continue;
                }

                var parts = cleanLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    Logger.Log($"WARNING: Skipping malformed line in bodyconv: {line}");
                    continue;
                }

                if (ushort.TryParse(parts[0], out ushort originalId))
                {
                    bool foundValidEntry = false;

                    // Parcourir les colonnes pour trouver la première entrée valide non -1
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string newIdString = parts[i].Trim();
                        int mulFileId = i + 1; // anim2.mul = 2, anim3.mul = 3, etc.

                        // Ne pas s'arrêter sur -1, continuer à chercher
                        if (newIdString == "-1" || newIdString == "0xFFFF" || newIdString == "-1}")
                        {
                            continue; // Passer à la colonne suivante
                        }

                        // Si on trouve une valeur non -1, c'est celle-là qu'on utilise
                        if (ushort.TryParse(newIdString, out ushort newId))
                        {
                            _bodyConv.Add(new BodyDef
                            {
                                OriginalId = originalId,
                                NewId = newId,
                                MulFile = mulFileId
                            });
                            Logger.Log($"DEBUG: Added bodyconv entry: OriginalId={originalId}, NewId={newId}, MulFile={mulFileId}");
                            foundValidEntry = true;
                            break; // On a trouvé une entrée valide, on peut sortir
                        }
                    }

                    // Si on n'a trouvé aucune entrée valide, on garde l'ID original dans le premier fichier qui n'a pas -1
                    if (!foundValidEntry)
                    {
                        for (int i = 1; i < parts.Length; i++)
                        {
                            string idString = parts[i].Trim();
                            if (idString == "-1" || idString == "0xFFFF" || idString == "-1}")
                                continue;

                            _bodyConv.Add(new BodyDef
                            {
                                OriginalId = originalId,
                                NewId = originalId, // On garde l'ID original
                                MulFile = i + 1
                            });
                            Logger.Log($"DEBUG: Added bodyconv entry with original ID: OriginalId={originalId}, NewId={originalId}, MulFile={i + 1}");
                            break;
                        }
                    }
                }
                else
                {
                    Logger.Log($"WARNING: Skipping line with invalid OriginalId in bodyconv: {line}");
                }
            }
            Logger.Log($"DEBUG: Finished loading bodyconv. Total loaded: {_bodyConv.Count}");
        }
    }
}
