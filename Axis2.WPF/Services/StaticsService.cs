using Axis2.WPF.Models;
using System.Collections.Generic;
using System.IO;
using System;
using System.IO.MemoryMappedFiles;

namespace Axis2.WPF.Services
{
    public class StaticsService : IDisposable
    {
        private StaticIndex[] _indices = Array.Empty<StaticIndex>();
        private ushort[] _radarcol = Array.Empty<ushort>();
        private MemoryMappedFileService _staidxFileService;
        private MemoryMappedFileService _staticsFileService;

        public StaticsService(string indexPath, string staticsPath, string radarcolPath)
        {

            LoadRadarcol(radarcolPath);

            _staidxFileService = new MemoryMappedFileService(indexPath);
            _staticsFileService = new MemoryMappedFileService(staticsPath);

            LoadIndices(indexPath);
        }

        public void Dispose()
        {
            _staidxFileService?.Dispose();
            _staticsFileService?.Dispose();
        }

        private void LoadRadarcol(string path)
        {
            _radarcol = new ushort[0x10000];
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(fs))
                {
                    for (int i = 0; i < _radarcol.Length; i++)
                    {
                        _radarcol[i] = reader.ReadUInt16();
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void LoadIndices(string path)
        {
            if (!_staidxFileService.IsOpen)
            {
                return;
            }

            try
            {
                long count = _staidxFileService.Capacity / 12;
                _indices = new StaticIndex[count];

                for (int i = 0; i < count; i++)
                {
                    long offset = i * 12;
                    _indices[i] = new StaticIndex
                    {
                        Lookup = _staidxFileService.ReadInt32(offset),
                        Size = _staidxFileService.ReadInt32(offset + 4),
                        Unknown = _staidxFileService.ReadInt32(offset + 8)
                    };
                }
            }
            catch (Exception ex)
            {
            }
        }

        public List<StaticTile> GetStaticsForBlock(int blockX, int blockY, int mapWidthInBlocks)
        {
            var tiles = new List<StaticTile>();
            int blockIndex = blockX * mapWidthInBlocks + blockY;

            if (blockIndex < 0 || blockIndex >= _indices.Length)
            {
                return tiles;
            }

            StaticIndex index = _indices[blockIndex];

            if (index.Lookup == -1 || index.Size <= 0)
            {
                return tiles;
            }

            if (!_staticsFileService.IsOpen)
            {
                return tiles;
            }

            try
            {
                int count = index.Size / 7;
                StaticTile?[] tempTiles = new StaticTile?[64]; // 8x8 cells in a block

                for (int i = 0; i < count; i++)
                {
                    long offset = index.Lookup + (long)i * 7;

                    // Lecture des coordonnes d'abord pour validation
                    byte x = _staticsFileService.ReadByte(offset + 2);
                    byte y = _staticsFileService.ReadByte(offset + 3);

                    // Validation des coordonnes avant de crer l'objet
                    if (x >= 8 || y >= 8)
                    {
                        continue; // Coordonnes invalides, passer au suivant
                    }

                    int cellIndex = x + (y * 8);

                    StaticTile currentTile = new StaticTile
                    {
                        TileID = _staticsFileService.ReadUInt16(offset),
                        X = x,
                        Y = y,
                        Z = _staticsFileService.ReadSByte(offset + 4),
                        Hue = _staticsFileService.ReadUInt16(offset + 5)
                    };

                    // Garder seulement le static avec la plus haute valeur Z pour chaque cellule
                    if (tempTiles[cellIndex] == null || currentTile.Z >= tempTiles[cellIndex].Value.Z)

                    {
                        tempTiles[cellIndex] = currentTile;
                    }
                }

                // Ajouter seulement les tuiles statiques slectionnes  la liste finale
                for (int i = 0; i < tempTiles.Length; i++)
                {
                    if (tempTiles[i].HasValue)
                    {
                        tiles.Add(tempTiles[i].Value);
                    }
                }
            }
            catch (Exception ex)
            {
            }

            return tiles;
        }

        public ushort GetRadarcolColor(int tileId)
        {
            if (tileId >= 0 && tileId < _radarcol.Length)
            {
                return _radarcol[tileId];
            }
            return 0;
        }
    }
}