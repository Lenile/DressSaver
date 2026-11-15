using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Dalamud.Plugin.Services;

namespace DressSaver.Core;

/// <summary>
/// Déplace une pièce de tenue d'un objet du jeu vers un autre (ex. e0205 → e0360) :
/// renomme les fichiers, met à jour leurs références internes, réécrit les métadonnées
/// et change l'objet dans le design Glamourer.
/// </summary>
public sealed class Remapper
{
    private static readonly Regex MaterialFolder = new(@"/material/v(\d{4})/", RegexOptions.Compiled);

    private readonly IDataManager data;
    private readonly ItemCatalog items;

    public Remapper(IDataManager data, ItemCatalog items)
    {
        this.data = data;
        this.items = items;
    }

    public void Apply(Outfit outfit, OutfitPiece piece, GameItem target)
    {
        var slot = piece.Slot;
        var oldSet = piece.Set;
        var oldTag = piece.Tag;
        var newTag = Slots.Tag(slot.Kind, target.Set);
        var oldVariant = items.Get(piece.ItemId)?.Variant ?? 1;
        var raceCode = Extractor.RaceModelCode(outfit.Design);

        // 1. Fichiers de la pièce. Si seul le matériau est modé (retexture), le modèle d'origine du jeu
        //    est copié pour être déplacé avec. Idem pour les textures d'origine encore référencées.
        var group = slot.Kind == PieceKind.Equipment ? FileGroup.Tenue : FileGroup.Accessoires;
        var letter = slot.Kind == PieceKind.Equipment ? "e" : "a";
        var oldModelDir = Slots.Folder(slot.Kind, oldSet) + "model/";
        var baseCode = outfit.Design.PenumbraGender == "Female" ? "c0201" : "c0101";
        if (!outfit.Files.Keys.Any(k => k.StartsWith(oldModelDir) && k.EndsWith($"_{slot.Suffix}.mdl")))
        {
            foreach (var code in new[] { baseCode, raceCode }.Distinct())
            {
                var modelPath = $"{oldModelDir}{code}{letter}{oldSet:D4}_{slot.Suffix}.mdl";
                var vanillaModel = data.GetFile(modelPath);
                if (vanillaModel != null)
                    outfit.Files[modelPath] = new OutfitFile { GamePath = modelPath, Data = vanillaModel.Data, Group = group };
            }
        }

        var pieceFiles = Extractor.PieceFiles(outfit, piece);
        foreach (var mtrl in pieceFiles.Where(p => p.EndsWith(".mtrl")).ToList())
        {
            foreach (var tex in FileFormats.MaterialTextures(outfit.Files[mtrl].Data))
            {
                if (!tex.Contains(oldTag) || outfit.Files.ContainsKey(tex))
                    continue;
                var vanilla = data.GetFile(tex);
                if (vanilla == null)
                    continue;
                outfit.Files[tex] = new OutfitFile { GamePath = tex, Data = vanilla.Data, Group = outfit.Files[mtrl].Group };
                pieceFiles.Add(tex);
            }
        }

        // Textures encore utilisées par des matériaux qui ne bougent pas : on les garde aussi à l'ancien chemin.
        var stillUsed = new HashSet<string>();
        foreach (var f in outfit.Files.Values.Where(f => f.GamePath.EndsWith(".mtrl") && !pieceFiles.Contains(f.GamePath)))
            stillUsed.UnionWith(FileFormats.MaterialTextures(f.Data));

        var materialId = 1;
        foreach (var gp in pieceFiles)
        {
            if (!gp.Contains(oldTag))
                continue; // texture rangée hors du dossier de l'objet : inchangée

            var file = outfit.Files[gp];
            var newGp = gp.Replace(oldTag, newTag);
            var newData = gp.EndsWith(".mdl") || gp.EndsWith(".mtrl")
                ? FileFormats.ReplaceTag(file.Data, oldTag, newTag)
                : file.Data;
            outfit.Files[newGp] = new OutfitFile { GamePath = newGp, Data = newData, Group = file.Group };
            if (!stillUsed.Contains(gp))
                outfit.Files.Remove(gp);

            var mf = MaterialFolder.Match(gp);
            if (mf.Success)
                materialId = int.Parse(mf.Groups[1].Value);
        }

        // 2. Métadonnées
        bool Matches(JsonObject m) => MetaCodec.SetOf(m) == oldSet && MetaCodec.SlotOf(m) == slot.Name;
        var oldImc = outfit.Meta.FirstOrDefault(m => MetaCodec.TypeOf(m) == "Imc" && Matches(m)
                                                    && (m["Manipulation"]?["Variant"]?.ToString() == oldVariant.ToString()))
                     ?? outfit.Meta.FirstOrDefault(m => MetaCodec.TypeOf(m) == "Imc" && Matches(m));
        var oldEqp = outfit.Meta.FirstOrDefault(m => MetaCodec.TypeOf(m) == "Eqp" && Matches(m));
        outfit.Meta.RemoveAll(Matches);

        var imcEntry = oldImc?["Manipulation"]?["Entry"]?.DeepClone() as JsonObject ?? new JsonObject
        {
            ["MaterialId"] = 1, ["DecalId"] = 0, ["VfxId"] = 0, ["MaterialAnimationId"] = 0, ["AttributeMask"] = 1023, ["SoundId"] = 0,
        };
        imcEntry["MaterialId"] = materialId;
        outfit.Meta.Add(new JsonObject
        {
            ["Type"] = "Imc",
            ["Manipulation"] = new JsonObject
            {
                ["Entry"] = imcEntry,
                ["ObjectType"] = slot.Kind == PieceKind.Equipment ? "Equipment" : "Accessory",
                ["PrimaryId"] = target.Set,
                ["SecondaryId"] = 0,
                ["Variant"] = target.Variant,
                ["EquipSlot"] = slot.Name,
                ["BodySlot"] = "Unknown",
            },
        });

        if (oldEqp != null)
        {
            var eqp = (JsonObject)oldEqp.DeepClone();
            eqp["Manipulation"]!["SetId"] = target.Set;
            outfit.Meta.Add(eqp);
        }

        // EQDP : modèle chargé pour la race du joueur (son modèle à elle s'il existe, sinon celui de base)
        var gender = outfit.Design.PenumbraGender;
        var modelDir = Slots.Folder(slot.Kind, target.Set) + "model/";
        bool HasModel(string code) => outfit.Files.ContainsKey($"{modelDir}{code}{letter}{target.Set:D4}_{slot.Suffix}.mdl");

        void AddEqdp(string race, bool hasModel) => outfit.Meta.Add(new JsonObject
        {
            ["Type"] = "Eqdp",
            ["Manipulation"] = new JsonObject
            {
                ["Entry"] = hasModel ? slot.EqdpBits : (ushort)0,
                ["Gender"] = gender,
                ["Race"] = race,
                ["SetId"] = target.Set,
                ["Slot"] = slot.Name,
            },
        });

        AddEqdp("Midlander", HasModel(baseCode));
        if (outfit.Design.PenumbraRace != "Midlander")
            AddEqdp(outfit.Design.PenumbraRace, HasModel(raceCode));

        // 3. Design Glamourer et pièce
        outfit.Design.SetItem(slot.Name, target.Id);
        piece.Set = target.Set;
        piece.ItemId = target.Id;
        piece.ItemName = target.Name;
        piece.Conflicts.Clear();
        piece.SharedWith.Clear();
    }
}
