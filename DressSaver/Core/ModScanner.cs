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
    private readonly HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Diagnostic : dossier lu, nombre de mods, mods de tenue reconnus, erreurs de lecture.</summary>
    public string ScannedDirectory { get; private set; } = string.Empty;
    public int ModCount { get; private set; }
    public HashSet<string> OutfitMods { get; } = new();

    /// <summary>Mods de tenue : nom du dossier → nom affiché.</summary>
    public Dictionary<string, string> OutfitModFolders { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Tous les mods : nom affiché → nom du dossier (pour repérer les noms déjà pris).</summary>
    public Dictionary<string, string> FoldersByName { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Auteur de chaque mod, par nom de dossier.</summary>
    public Dictionary<string, string> Authors { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Errors { get; } = new();

    /// <summary>Modèles touchés par au moins un mod : clé "e0205" ou "a0029".</summary>
    public HashSet<string> TouchedSets { get; } = new();

    public static ModScanner Scan(string modDirectory)
    {
        var scanner = new ModScanner { ScannedDirectory = modDirectory ?? string.Empty };
        if (string.IsNullOrEmpty(modDirectory) || !Directory.Exists(modDirectory))
        {
            scanner.Errors.Add(Loc.T("err_mod_folder", modDirectory ?? string.Empty));
            return scanner;
        }

        foreach (var folder in Directory.EnumerateDirectories(modDirectory))
        {
            try
            {
                scanner.ScanMod(folder);
            }
            catch (Exception e)
            {
                // Un mod mal formé ne doit pas bloquer l'analyse, mais on le note.
                scanner.Errors.Add($"{Path.GetFileName(folder)} : {e.Message}");
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
        Authors[Path.GetFileName(folder)] = meta?["Author"]?.ToString() ?? string.Empty;
        var isOurs = false;
        if (meta?["ModTags"] is JsonArray tags)
            foreach (var t in tags)
                if (string.Equals(t?.ToString(), Tag, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t?.ToString(), "Tenue", StringComparison.OrdinalIgnoreCase))
                    isOurs = true;
        ModCount++;
        FoldersByName[name] = Path.GetFileName(folder);
        if (isOurs)
        {
            OutfitMods.Add(name);
            OutfitModFolders[Path.GetFileName(folder)] = name;
        }

        // Les versions récentes de Penumbra regroupent tout dans meta.json (les anciens fichiers
        // deviennent des .bak) ; les anciennes utilisent default_mod.json et group_*.json.
        // On cherche donc tous les objets « Files » où qu'ils soient, dans tous ces fichiers.
        if (meta != null)
            CollectFiles(meta, name, folder, isOurs);

        var jsons = new List<string>();
        var defaultMod = Path.Combine(folder, "default_mod.json");
        if (File.Exists(defaultMod))
            jsons.Add(defaultMod);
        jsons.AddRange(Directory.EnumerateFiles(folder, "group_*.json"));

        foreach (var json in jsons)
            if (JsonNode.Parse(File.ReadAllText(json)) is JsonNode node)
                CollectFiles(node, name, folder, isOurs);
    }

    /// <summary>Parcourt tout le JSON et ajoute chaque table « Files » (chemin de jeu → fichier du mod).</summary>
    private void CollectFiles(JsonNode node, string modName, string folder, bool isOurs)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (value == null)
                        continue;
                    if (key == "Files" && value is JsonObject files)
                        AddFiles(modName, folder, files, isOurs);
                    else
                        CollectFiles(value, modName, folder, isOurs);
                }

                break;
            case JsonArray arr:
                foreach (var item in arr)
                    if (item != null)
                        CollectFiles(item, modName, folder, isOurs);
                break;
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
            if (!seen.Add(folder + "|" + gp + "|" + disk))
                continue; // déjà vu (même fichier présent dans l'ancien et le nouveau format)
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
