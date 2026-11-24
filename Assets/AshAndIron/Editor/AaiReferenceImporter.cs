using System;
using System.IO;
using System.IO.Compression;
using Quantum;
using UnityEditor;
using UnityEngine;

namespace AshAndIron.Editor {
  public static class AaiReferenceImporter {
    private const string Input = "Assets/AshAndIron/Reference/Generated";
    private const string Output = "Assets/QuantumUser/Resources/AshAndIron";

    [Serializable] private class MapCatalog { public MapRecord[] Maps; }
    [Serializable] private class MapRecord {
      public string MapId, DisplayName;
      public bool HasSeed;
      public uint Seed;
      public AaiMode Mode;
      public int Width, Height, TileSize;
      public AaiTerrain[] Tiles;
      public long SeatOneXRaw, SeatOneYRaw, SeatTwoXRaw, SeatTwoYRaw;
      public AaiMapResourceSpec[] Sources;
    }

    [MenuItem("Ash & Iron/Import reference rules and maps")]
    public static void Import() {
      var rulesJson = File.ReadAllText(Input + "/rules.editor.json");
      MapRecord[] maps;
      using (var stream = File.OpenRead(Input + "/maps.editor.json.gz"))
      using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
      using (var reader = new StreamReader(gzip))
        maps = JsonUtility.FromJson<MapCatalog>(reader.ReadToEnd()).Maps;
      var check = AssetObject.Create<AaiRulesAsset>();
      try {
        JsonUtility.FromJsonOverwrite(rulesJson, check);
        Validate(check, maps);
      } finally {
        UnityEngine.Object.DestroyImmediate(check);
      }

      EnsureFolder(Output);
      var rules = GetOrCreate<AaiRulesAsset>(Output + "/Rules.asset");
      JsonUtility.FromJsonOverwrite(rulesJson, rules);
      EditorUtility.SetDirty(rules);
      foreach (var record in maps) {
        var map = GetOrCreate<AaiMapAsset>(Output + "/" + AssetName(record) + ".asset");
        map.MapId = record.MapId;
        map.DisplayName = record.DisplayName;
        map.HasSeed = record.HasSeed;
        map.Seed = record.Seed;
        map.Mode = record.Mode;
        map.Width = record.Width;
        map.Height = record.Height;
        map.TileSize = record.TileSize;
        map.Tiles = record.Tiles;
        map.SeatOneXRaw = record.SeatOneXRaw;
        map.SeatOneYRaw = record.SeatOneYRaw;
        map.SeatTwoXRaw = record.SeatTwoXRaw;
        map.SeatTwoYRaw = record.SeatTwoYRaw;
        map.Sources = record.Sources;
        EditorUtility.SetDirty(map);
      }
      AssetDatabase.SaveAssets();
      AssetDatabase.Refresh();
      Debug.Log($"Ash & Iron: imported {rules.Entities.Length} entity definitions and {maps.Length} maps from {rules.SourceRevision}.");
    }

    private static string AssetName(MapRecord record) => record.MapId + (record.HasSeed ?
      "-" + record.Seed + "-" + record.Mode.ToString().ToLowerInvariant() : "-base");

    private static void Validate(AaiRulesAsset rules, MapRecord[] maps) {
      if (rules.SchemaVersion != 1 || rules.Entities?.Length != 115 || rules.Training?.Length != 65 ||
          rules.Research?.Length != 31 || rules.Structures?.Length != 8 || rules.Abilities?.Length != 14 || maps?.Length != 42)
        throw new InvalidDataException("Reference schema or row counts are invalid. Run Reference/compile-assets.mjs.");
      for (int index = 0; index < rules.Entities.Length; index++) {
        var row = rules.Entities[index];
        if ((int)row.Race * 23 + (int)row.Kind != index || row.Health <= 0)
          throw new InvalidDataException("Entity rows are not in canonical order.");
      }
      for (int index = 0; index < rules.Training.Length; index++) {
        var row = rules.Training[index];
        if ((int)row.Race * 13 + (int)row.Kind != index || row.Population <= 0 || row.Ticks <= 0)
          throw new InvalidDataException("Invalid training row.");
      }
      var names = new System.Collections.Generic.HashSet<string>();
      foreach (var map in maps) {
        if (string.IsNullOrWhiteSpace(map.MapId) || !System.Text.RegularExpressions.Regex.IsMatch(map.MapId, "^[a-z0-9-]+$") ||
            map.Width <= 0 || map.Height <= 0 || map.TileSize != 32 || map.Tiles?.Length != map.Width * map.Height ||
            map.Sources?.Length != 3 || !names.Add(AssetName(map))) throw new InvalidDataException("Invalid map data.");
      }
    }

    private static T GetOrCreate<T>(string path) where T : AssetObject {
      var existing = AssetDatabase.LoadAssetAtPath<T>(path);
      if (existing != null) return existing;
      var item = AssetObject.Create<T>();
      AssetDatabase.CreateAsset(item, path);
      return item;
    }

    private static void EnsureFolder(string path) {
      if (AssetDatabase.IsValidFolder(path)) return;
      var parent = Path.GetDirectoryName(path).Replace('\\', '/');
      EnsureFolder(parent);
      AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
  }
}
