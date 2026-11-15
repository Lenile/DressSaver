using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace DressSaver.Core;

public enum FileGroup
{
    Tenue,
    Accessoires,
    Cheveux,
    Maquillage,
}

/// <summary>Un fichier de jeu remplacé, avec son contenu.</summary>
public sealed class OutfitFile
{
    public required string GamePath { get; set; }
    public required byte[] Data { get; set; }
    public FileGroup Group { get; set; }
}

/// <summary>Un mod installé qui fournit des fichiers à la tenue.</summary>
public sealed class SourceMod
{
    public required string Directory { get; init; }
    public required string Name { get; init; }
    public string Author { get; init; } = string.Empty;

    /// <summary>Pièces de la tenue fournies par ce mod (noms d'objets, coiffure…).</summary>
    public List<string> Pieces { get; } = new();

    /// <summary>Nombre de fichiers de ce mod utilisés par la tenue.</summary>
    public int UsedFiles { get; set; }

    /// <summary>Nombre total de fichiers du mod (toutes options confondues).</summary>
    public int TotalFiles { get; set; }

    /// <summary>Le mod ne contient que des fichiers de la tenue.</summary>
    public bool OnlyOutfit => TotalFiles > 0 && UsedFiles >= TotalFiles;

    /// <summary>Cochée = à désactiver après création.</summary>
    public bool Selected { get; set; }
}

/// <summary>Un conflit :un autre mod DressSaver remplace le même fichier avec un contenu différent.</summary>
public sealed record Conflict(string ModName, string GamePath);

/// <summary>Une pièce de la tenue : un objet du jeu remplacé sur un emplacement.</summary>
public sealed class OutfitPiece
{
    public required SlotInfo Slot { get; init; }
    public required int Set { get; set; }
    public uint ItemId { get; set; }
    public string ItemName { get; set; } = "?";

    /// <summary>Conflits réels (contenu différent).</summary>
    public List<Conflict> Conflicts { get; } = new();

    /// <summary>Mods DressSaver qui partagent cet objet avec un contenu identique (sans gravité).</summary>
    public HashSet<string> SharedWith { get; } = new();

    /// <summary>Un modèle de la pièce est encore au format d'avant Dawntrail (risque d'être invisible).</summary>
    public bool OldModel { get; set; }

    /// <summary>L'utilisateur garde l'objet malgré le conflit (ex. conflit avec un vieux mod inutilisé).</summary>
    public bool IgnoreConflict { get; set; }

    /// <summary>Objet de remplacement choisi par l'utilisateur pour résoudre un conflit.</summary>
    public GameItem? Replacement { get; set; }

    public string Tag => Slots.Tag(Slot.Kind, Set);
    public string Folder => Slots.Folder(Slot.Kind, Set);
}

/// <summary>Résultat de l'analyse de la tenue portée.</summary>
public sealed class Outfit
{
    public Dictionary<string, OutfitFile> Files { get; } = new();

    /// <summary>Emplacement sur le disque de chaque fichier lu (chemin de jeu → fichier du mod).</summary>
    public Dictionary<string, string> DiskPaths { get; } = new();
    public List<OutfitPiece> Pieces { get; } = new();
    public List<JsonObject> Meta { get; } = new();
    public required GlamourerDesign Design { get; init; }

    /// <summary>Coiffures en conflit avec un autre mod DressSaver (ne peuvent pas être déplacées).</summary>
    public List<Conflict> HairConflicts { get; } = new();

    public int HairStyle { get; set; }

    /// <summary>Le modèle de la coiffure est encore au format d'avant Dawntrail.</summary>
    public bool OldHairModel { get; set; }

    /// <summary>Mods installés d'où viennent les fichiers de la tenue.</summary>
    public List<SourceMod> SourceMods { get; } = new();

    /// <summary>Pièces dont l'origine n'est pas un mod installé (synchro, jeu de base).</summary>
    public List<string> UnknownSourcePieces { get; } = new();

    /// <summary>Diagnostic de la comparaison avec les autres mods de tenue.</summary>
    public int DiagPieceFiles { get; set; }
    public int DiagSameFiles { get; set; }
    public int DiagDifferentFiles { get; set; }

    public IEnumerable<OutfitFile> InGroup(FileGroup group) => Files.Values.Where(f => f.Group == group);

    public bool HasConflicts => Pieces.Any(p => p.Conflicts.Count > 0 && p.Replacement == null && !p.IgnoreConflict);
}
