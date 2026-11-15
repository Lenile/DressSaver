using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Penumbra.Api.IpcSubscribers;

namespace DressSaver.Core;

/// <summary>
/// Analyse la tenue que porte le personnage du joueur, à partir de ce que Penumbra a
/// réellement chargé (fichiers de mods installés, métadonnées) et du design Glamourer actuel.
/// </summary>
public sealed class Extractor
{
    private static readonly Regex EquipModel = new(@"^chara/(equipment|accessory)/([ea])(\d{4})/.*?[ea]\d{4}_(met|top|glv|dwn|sho|ear|nek|wrs|rir|ril)", RegexOptions.Compiled);
    private static readonly Regex SetFolder = new(@"^chara/(equipment|accessory)/([ea])(\d{4})/", RegexOptions.Compiled);

    // Modèles qui font partie du corps (Bibo+ / TBSE...) et non de la tenue.
    private static readonly HashSet<string> BodySets = new() { "e0000", "e0279" };

    private readonly ItemCatalog items;
    private readonly IPluginLog log;

    private readonly GetGameObjectResourcePaths getResourcePaths;
    private readonly GetPlayerMetaManipulations getMeta;
    private readonly ResolvePlayerPath resolvePlayerPath;
    private readonly GetModDirectory getModDirectory;
    private readonly InstallMod installMod;
    private readonly GetCollectionForObject getCollection;
    private readonly TrySetMod trySetMod;
    private readonly GetModList getModList;
    private readonly Glamourer.Api.IpcSubscribers.GetStateBase64 getGlamourerState;

    public Extractor(IDalamudPluginInterface pi, ItemCatalog items, IPluginLog log)
    {
        this.items = items;
        this.log = log;
        getResourcePaths = new GetGameObjectResourcePaths(pi);
        getMeta = new GetPlayerMetaManipulations(pi);
        resolvePlayerPath = new ResolvePlayerPath(pi);
        getModDirectory = new GetModDirectory(pi);
        installMod = new InstallMod(pi);
        getCollection = new GetCollectionForObject(pi);
        trySetMod = new TrySetMod(pi);
        getModList = new GetModList(pi);
        getGlamourerState = new Glamourer.Api.IpcSubscribers.GetStateBase64(pi);
    }

    public string ModDirectory => getModDirectory.Invoke();

    public string Install(string pmpPath) => installMod.Invoke(pmpPath).ToString();

    /// <summary>Dossier (identifiant Penumbra) d'un mod installé à partir de son nom affiché, null s'il n'est pas (encore) là.</summary>
    public string? FindModDirectory(string modName)
    {
        foreach (var (dir, name) in getModList.Invoke())
            if (string.Equals(name, modName, StringComparison.OrdinalIgnoreCase))
                return dir;
        return null;
    }

    /// <summary>Active ou désactive un mod dans la collection utilisée par le personnage. Ne supprime jamais rien.</summary>
    public string SetModEnabled(string modDirectory, bool enabled)
    {
        var (valid, _, collection) = getCollection.Invoke(0);
        if (!valid)
            return Loc.T("err_no_character");
        return trySetMod.Invoke(collection.Id, modDirectory, enabled).ToString();
    }

    /// <summary>Liste les mods installés qui fournissent des fichiers à la tenue.</summary>
    public void FindSources(Outfit outfit, ModScanner scanner)
    {
        outfit.SourceMods.Clear();
        var root = Normalize(scanner.ScannedDirectory).TrimEnd('/') + "/";
        var used = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (gp, disk) in outfit.DiskPaths)
        {
            var d = Normalize(disk);
            if (!d.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                continue; // fichier venant d'une synchro (cache), pas d'un mod installé
            var rest = d[root.Length..];
            var slash = rest.IndexOf('/');
            if (slash <= 0)
                continue;
            var dir = rest[..slash];
            if (!used.TryGetValue(dir, out var set))
                used[dir] = set = new HashSet<string>();
            set.Add(gp);
        }

        foreach (var (dir, files) in used)
        {
            var modFiles = scanner.Files.Where(f => string.Equals(Path.GetFileName(f.ModFolder), dir, StringComparison.OrdinalIgnoreCase)).ToList();
            var source = new SourceMod
            {
                Directory = modFiles.Count > 0 ? Path.GetFileName(modFiles[0].ModFolder) : dir,
                Name = modFiles.Count > 0 ? modFiles[0].ModName : dir,
                UsedFiles = files.Count,
                TotalFiles = modFiles.Select(f => f.GamePath).Distinct().Count(),
            };
            source.Selected = source.OnlyOutfit;
            outfit.SourceMods.Add(source);
        }

        outfit.SourceMods.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>Le chemin du jeu est-il remplacé pour le joueur (par un mod installé ou une synchro) ?</summary>
    public bool IsModdedForPlayer(string gamePath)
    {
        var resolved = resolvePlayerPath.Invoke(gamePath);
        return !string.IsNullOrEmpty(resolved) && !string.Equals(Normalize(resolved), gamePath, StringComparison.OrdinalIgnoreCase);
    }

    public Outfit Analyze(bool includeAccessories, bool includeHair, bool includeMakeup)
    {
        // 1. Design Glamourer actuel
        var (ec, state) = getGlamourerState.Invoke(0);
        if (string.IsNullOrEmpty(state))
            throw new InvalidOperationException(Loc.T("err_glamourer", ec));
        var outfit = new Outfit { Design = GlamourerDesign.Parse(state) };

        // 2. Fichiers réellement chargés par Penumbra pour le joueur (objet 0)
        var trees = getResourcePaths.Invoke(0);
        var paths = trees.Length > 0 ? trees[0] : null;
        if (paths == null)
            throw new InvalidOperationException(Loc.T("err_penumbra"));

        var diskByGamePath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (actual, gamePaths) in paths)
        {
            var disk = StripPrefix(actual);
            if (!Path.IsPathRooted(disk) || !File.Exists(disk))
                continue; // fichier du jeu non modifié
            foreach (var gp in gamePaths)
            {
                var n = Normalize(gp);
                if (!string.Equals(n, Normalize(disk), StringComparison.OrdinalIgnoreCase))
                    diskByGamePath[n] = disk;
            }
        }

        // 3. Pièces de la tenue (objets dont un modèle est remplacé)
        var pieceByKey = new Dictionary<string, OutfitPiece>();
        foreach (var gp in diskByGamePath.Keys)
        {
            // Une pièce = un modèle ou un matériau remplacé (les retextures gardent le modèle d'origine).
            if (!gp.EndsWith(".mdl") && !gp.EndsWith(".mtrl"))
                continue;
            var m = EquipModel.Match(gp);
            if (!m.Success)
                continue;
            var tag = m.Groups[2].Value + m.Groups[3].Value;
            if (BodySets.Contains(tag))
                continue;
            var slot = Slots.FromSuffix(m.Groups[4].Value);
            if (slot == null)
                continue;
            if (slot.Kind == PieceKind.Accessory && !includeAccessories)
                continue;
            var key = tag + "/" + slot.Name;
            if (pieceByKey.ContainsKey(key))
                continue;

            var set = int.Parse(m.Groups[3].Value);
            var itemId = outfit.Design.ItemOf(slot.Name);
            var piece = new OutfitPiece
            {
                Slot = slot,
                Set = set,
                ItemId = itemId,
                ItemName = items.Get(itemId)?.Name ?? $"{tag} ({Loc.Slot(slot.Name)})",
            };
            pieceByKey[key] = piece;
            outfit.Pieces.Add(piece);
        }

        // 4. Fichiers par groupe
        var activeTags = outfit.Pieces.Select(p => p.Tag).ToHashSet();
        foreach (var (gp, disk) in diskByGamePath)
        {
            FileGroup? group = null;
            var f = SetFolder.Match(gp);
            if (f.Success)
            {
                var tag = f.Groups[2].Value + f.Groups[3].Value;
                if (activeTags.Contains(tag))
                    group = f.Groups[1].Value == "equipment" ? FileGroup.Tenue : FileGroup.Accessoires;
            }
            else if (includeHair && gp.Contains("/obj/hair/"))
            {
                group = FileGroup.Cheveux;
            }
            else if (includeMakeup && gp.StartsWith("chara/common/texture/decal_face/"))
            {
                group = FileGroup.Maquillage;
            }

            if (group != null)
            {
                outfit.Files[gp] = new OutfitFile { GamePath = gp, Data = File.ReadAllBytes(disk), Group = group.Value };
                outfit.DiskPaths[gp] = disk;
            }
        }

        // 5. Textures rangées ailleurs (ex. chara/nyaughty/...) : rattachées via les matériaux
        foreach (var mtrl in outfit.Files.Values.Where(x => x.GamePath.EndsWith(".mtrl")).ToList())
        {
            foreach (var tex in FileFormats.MaterialTextures(mtrl.Data))
            {
                var candidates = new[] { tex, InsertDashes(tex) };
                foreach (var c in candidates)
                {
                    if (outfit.Files.ContainsKey(c) || !diskByGamePath.TryGetValue(c, out var disk))
                        continue;
                    outfit.Files[c] = new OutfitFile { GamePath = c, Data = File.ReadAllBytes(disk), Group = mtrl.Group };
                    outfit.DiskPaths[c] = disk;
                }
            }
        }

        // 6. Métadonnées utiles à la tenue
        var gender = outfit.Design.PenumbraGender;
        foreach (var entry in MetaCodec.Decode(getMeta.Invoke()))
        {
            var type = MetaCodec.TypeOf(entry);
            var set = MetaCodec.SetOf(entry);
            var slotName = MetaCodec.SlotOf(entry);
            var piece = outfit.Pieces.FirstOrDefault(p => p.Set == set && p.Slot.Name == slotName);
            if (piece == null)
                continue;

            if (type == "Imc")
            {
                var objType = MetaCodec.ObjectTypeOf(entry);
                var expected = piece.Slot.Kind == PieceKind.Equipment ? "Equipment" : "Accessory";
                if (objType != expected)
                    continue;
            }
            else if (type == "Eqdp")
            {
                var g = (entry["Manipulation"] as System.Text.Json.Nodes.JsonObject)?["Gender"]?.ToString();
                if (g != gender)
                    continue;
            }
            else if (type != "Eqp")
            {
                continue;
            }

            outfit.Meta.Add(entry);
        }

        log.Information($"DressSaver: {outfit.Pieces.Count} pieces, {outfit.Files.Count} files, {outfit.Meta.Count} metadata entries.");
        outfit.HairStyle = int.TryParse((outfit.Design.Root["Customize"]?["Hairstyle"]?["Value"])?.ToString(), out var h) ? h : 0;
        return outfit;
    }

    /// <summary>Compare la tenue aux mods déjà créés par DressSaver.</summary>
    public void FindConflicts(Outfit outfit, ModScanner scanner)
    {
        var ours = scanner.Files.Where(f => f.IsDressSaver).ToList();
        outfit.DiagPieceFiles = outfit.DiagSameFiles = outfit.DiagDifferentFiles = 0;
        foreach (var piece in outfit.Pieces)
        {
            piece.Conflicts.Clear();
            piece.SharedWith.Clear();
            var pieceFiles = PieceFiles(outfit, piece);
            var mine = outfit.Files.Values.Where(f => pieceFiles.Contains(f.GamePath)).ToList();
            outfit.DiagPieceFiles += mine.Count;
            foreach (var file in mine)
            {
                foreach (var other in ours.Where(o => o.GamePath == file.GamePath))
                {
                    if (!File.Exists(other.DiskPath))
                        continue;
                    if (ModScanner.Hash(other.DiskPath) == ModScanner.Hash(file.Data))
                    {
                        piece.SharedWith.Add(other.ModName);
                        outfit.DiagSameFiles++;
                    }
                    else
                    {
                        piece.Conflicts.Add(new Conflict(other.ModName, file.GamePath));
                        outfit.DiagDifferentFiles++;
                    }
                }
            }

            foreach (var c in piece.Conflicts)
                piece.SharedWith.Remove(c.ModName);
        }

        outfit.HairConflicts.Clear();
        foreach (var file in outfit.InGroup(FileGroup.Cheveux))
            foreach (var other in ours.Where(o => o.GamePath == file.GamePath && File.Exists(o.DiskPath)))
                if (ModScanner.Hash(other.DiskPath) != ModScanner.Hash(file.Data))
                    outfit.HairConflicts.Add(new Conflict(other.ModName, file.GamePath));
    }

    /// <summary>
    /// Objets libres pour un emplacement : aucun mod installé ne touche leur modèle, ils ne
    /// sont pas modifiés pour le joueur, et la tenue ne les utilise pas déjà.
    /// </summary>
    public List<GameItem> FreeItems(Outfit outfit, SlotInfo slot, ModScanner scanner)
    {
        var used = outfit.Pieces.Select(p => p.Tag).ToHashSet();
        var raceModel = RaceModelCode(outfit.Design);
        var result = new List<GameItem>();
        var checkedSets = new Dictionary<int, bool>();
        foreach (var item in items.ForSlot(slot))
        {
            var tag = Slots.Tag(slot.Kind, item.Set);
            if (used.Contains(tag) || scanner.TouchedSets.Contains(tag))
                continue;

            if (!checkedSets.TryGetValue(item.Set, out var free))
            {
                var kind = slot.Kind == PieceKind.Equipment ? "equipment" : "accessory";
                var letter = slot.Kind == PieceKind.Equipment ? "e" : "a";
                var basePath = $"chara/{kind}/{letter}{item.Set:D4}/model/";
                free = !IsModdedForPlayer($"{basePath}c0201{letter}{item.Set:D4}_{slot.Suffix}.mdl")
                       && !IsModdedForPlayer($"{basePath}{raceModel}{letter}{item.Set:D4}_{slot.Suffix}.mdl");
                checkedSets[item.Set] = free;
            }

            if (free)
                result.Add(item);
        }

        return result;
    }

    /// <summary>Fichiers propres à une pièce : ses modèles, leurs matériaux, et les textures de ces matériaux.</summary>
    public static HashSet<string> PieceFiles(Outfit outfit, OutfitPiece piece)
    {
        var result = new HashSet<string>();
        var suffix = $"{piece.Tag}_{piece.Slot.Suffix}";
        var models = outfit.Files.Values.Where(f => f.GamePath.StartsWith(piece.Folder) && f.GamePath.EndsWith(".mdl") && f.GamePath.Contains(suffix)).ToList();
        var materialNames = new HashSet<string>();
        foreach (var mdl in models)
        {
            result.Add(mdl.GamePath);
            materialNames.UnionWith(FileFormats.ModelMaterials(mdl.Data));
        }

        foreach (var f in outfit.Files.Values.Where(f => f.GamePath.EndsWith(".mtrl") && f.GamePath.StartsWith(piece.Folder)))
        {
            var name = Path.GetFileName(f.GamePath);
            if (!materialNames.Contains(name) && !name.Contains(suffix))
                continue;
            result.Add(f.GamePath);
            foreach (var tex in FileFormats.MaterialTextures(f.Data))
            {
                if (outfit.Files.ContainsKey(tex))
                    result.Add(tex);
                else if (outfit.Files.ContainsKey(InsertDashes(tex)))
                    result.Add(InsertDashes(tex));
            }
        }

        return result;
    }

    /// <summary>Code modèle de la race du joueur, ex. "c1401" pour une Au Ra femme.</summary>
    public static string RaceModelCode(GlamourerDesign d)
    {
        var baseCode = d.PenumbraRace switch
        {
            "Midlander" => 1, "Highlander" => 3, "Elezen" => 5, "Miqote" => 7, "Roegadyn" => 9,
            "Lalafell" => 11, "AuRa" => 13, "Hrothgar" => 15, "Viera" => 17, _ => 1,
        };
        if (d.PenumbraGender == "Female")
            baseCode++;
        return $"c{baseCode:D2}01";
    }

    private static string StripPrefix(string path)
    {
        // Penumbra peut préfixer certains chemins : "|clé|C:\...". On garde la partie fichier.
        if (path.StartsWith('|'))
        {
            var idx = path.LastIndexOf('|');
            path = path[(idx + 1)..];
        }

        return path;
    }

    private static string Normalize(string path) => path.Replace('\\', '/').ToLowerInvariant();

    /// <summary>Variante d'un chemin de texture avec le préfixe « -- » utilisé par certains mods.</summary>
    private static string InsertDashes(string texPath)
    {
        var idx = texPath.LastIndexOf('/');
        return idx < 0 ? texPath : texPath[..(idx + 1)] + "--" + texPath[(idx + 1)..];
    }
}
