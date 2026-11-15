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

/// <summary>Un conflit : un autre mod DressSaver remplace le même fichier avec un contenu différent.</summary>
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

    /// <summary>Objet de remplacement choisi par l'utilisateur pour résoudre un conflit.</summary>
    public GameItem? Replacement { get; set; }

    public string Tag => Slots.Tag(Slot.Kind, Set);
    public string Folder => Slots.Folder(Slot.Kind, Set);
}

/// <summary>Résultat de l'analyse de la tenue portée.</summary>
public sealed class Outfit
{
    public Dictionary<string, OutfitFile> Files { get; } = new();
    public List<OutfitPiece> Pieces { get; } = new();
    public List<JsonObject> Meta { get; } = new();
    public required GlamourerDesign Design { get; init; }

    /// <summary>Coiffures en conflit avec un autre mod DressSaver (ne peuvent pas être déplacées).</summary>
    public List<Conflict> HairConflicts { get; } = new();

    public int HairStyle { get; set; }

    public IEnumerable<OutfitFile> InGroup(FileGroup group) => Files.Values.Where(f => f.Group == group);

    public bool HasConflicts => Pieces.Any(p => p.Conflicts.Count > 0 && p.Replacement == null);
}
