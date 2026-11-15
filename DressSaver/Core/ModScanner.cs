using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DressSaver.Core;

/// <summary>Un fichier de jeu remplacé par un mod installé.</summary>
public sealed record ModFile(string ModName, string ModFolder, string GamePath, string DiskPath, bool IsDressSaver);

/// <summary>
/// Parcourt le dossier des mods Penumbra pour savoir quels fichiers de jeu sont déjà
/// remplacés, par quels mods, et lesquels ont été créés par DressSaver.
/// </summary>
public sealed class ModScanner
{
    public const string Tag = "DressSaver";

    private static readonly Regex SetRegex = new(@"^chara/(equipment|accessory)/([ea])(\d{4})/", RegexOptions.Compiled);

    public List<ModFile> Files { get; } = new();

    /// <summary>Modèles touchés par au moins un mod : clé "e0205" ou "a0029".</summary>
    public HashSet<string> TouchedSets { get; } = new();

    public static ModScanner Scan(string modDirectory)
    {
        var scanner = new ModScanner();
        if (string.IsNullOrEmpty(modDirectory) || !Directory.Exists(modDirectory))
            return scanner;

        foreach (var folder in Directory.EnumerateDirectories(modDirectory))
        {
            try
            {
                scanner.ScanMod(folder);
            }
            catch (Exception)
            {
                // Un mod mal formé ne doit pas bloquer l'analyse.
            }
        }

        return scanner;
    }

    private void ScanMod(string folder)
    {
        var metaPath = Path.Combine(folder, "meta.json");
        if (!File.Exists(metaPath))
            return;

        var meta = JsonNode.Parse(File.ReadAllText(metaPath)) as JsonObject;
        var name = meta?["Name"]?.ToString() ?? Path.GetFileName(folder);
        var isOurs = false;
        if (meta?["ModTags"] is JsonArray tags)
            foreach (var t in tags)
                if (string.Equals(t?.ToString(), Tag, StringComparison.OrdinalIgnoreCase))
                    isOurs = true;

        var jsons = new List<string>();
        var defaultMod = Path.Combine(folder, "default_mod.json");
        if (File.Exists(defaultMod))
            jsons.Add(defaultMod);
        jsons.AddRange(Directory.EnumerateFiles(folder, "group_*.json"));

        foreach (var json in jsons)
        {
            var node = JsonNode.Parse(File.ReadAllText(json)) as JsonObject;
            if (node == null)
                continue;

            if (node["Files"] is JsonObject files)
                AddFiles(name, folder, files, isOurs);

            if (node["Options"] is JsonArray options)
                foreach (var opt in options)
                    if (opt?["Files"] is JsonObject optFiles)
                        AddFiles(name, folder, optFiles, isOurs);
        }
    }

    private void AddFiles(string modName, string folder, JsonObject files, bool isOurs)
    {
        foreach (var (gamePath, relative) in files)
        {
            if (relative == null)
                continue;
            var gp = gamePath.Replace('\\', '/').ToLowerInvariant();
            var disk = Path.Combine(folder, relative.ToString().Replace('/', Path.DirectorySeparatorChar));
            Files.Add(new ModFile(modName, folder, gp, disk, isOurs));

            var m = SetRegex.Match(gp);
            if (m.Success)
                TouchedSets.Add(m.Groups[2].Value + m.Groups[3].Value);
        }
    }

    public static string Hash(string diskPath)
    {
        using var sha = SHA1.Create();
        using var stream = File.OpenRead(diskPath);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    public static string Hash(byte[] data) => Convert.ToHexString(SHA1.HashData(data));
}
