using System.Collections.Generic;

namespace DressSaver.Core;

public enum PieceKind
{
    Equipment,
    Accessory,
}

/// <summary>Un emplacement d'équipement, avec tout ce qu'il faut pour le reconnaître et le déplacer.</summary>
public sealed record SlotInfo(
    string Name,          // nom Penumbra / Glamourer : "Body", "Ears"...
    string Label,         // nom affiché en français
    string Suffix,        // suffixe des fichiers : "top", "ear"...
    PieceKind Kind,
    byte GameSlot,        // valeur EquipSlot du jeu (format binaire des métadonnées)
    ushort EqdpBits,      // bits EQDP (modèle + matériau) pour cet emplacement
    uint EquipSlotCategory); // ligne EquipSlotCategory de la feuille Item

public static class Slots
{
    public static readonly SlotInfo[] All =
    {
        new("Head",    "Tête",            "met", PieceKind.Equipment, 3,  0x0003, 3),
        new("Body",    "Torse",           "top", PieceKind.Equipment, 4,  0x000C, 4),
        new("Hands",   "Mains",           "glv", PieceKind.Equipment, 5,  0x0030, 5),
        new("Legs",    "Jambes",          "dwn", PieceKind.Equipment, 7,  0x00C0, 7),
        new("Feet",    "Pieds",           "sho", PieceKind.Equipment, 8,  0x0300, 8),
        new("Ears",    "Boucles d'oreilles", "ear", PieceKind.Accessory, 9,  0x0003, 9),
        new("Neck",    "Collier",         "nek", PieceKind.Accessory, 10, 0x000C, 10),
        new("Wrists",  "Bracelet",        "wrs", PieceKind.Accessory, 11, 0x0030, 11),
        new("RFinger", "Bague droite",    "rir", PieceKind.Accessory, 12, 0x00C0, 12),
        new("LFinger", "Bague gauche",    "ril", PieceKind.Accessory, 14, 0x0300, 12),
    };

    private static readonly Dictionary<string, SlotInfo> BySuffix = new();
    private static readonly Dictionary<string, SlotInfo> ByName = new();
    private static readonly Dictionary<byte, SlotInfo> ByGameSlot = new();

    static Slots()
    {
        foreach (var s in All)
        {
            BySuffix[s.Suffix] = s;
            ByName[s.Name] = s;
            ByGameSlot[s.GameSlot] = s;
        }
    }

    public static SlotInfo? FromSuffix(string suffix) => BySuffix.GetValueOrDefault(suffix);
    public static SlotInfo? FromName(string name) => ByName.GetValueOrDefault(name);
    public static SlotInfo? FromGameSlot(byte slot) => ByGameSlot.GetValueOrDefault(slot);

    /// <summary>Préfixe du dossier d'un objet : "chara/equipment/e0205/" ou "chara/accessory/a0029/".</summary>
    public static string Folder(PieceKind kind, int set)
        => kind == PieceKind.Equipment ? $"chara/equipment/e{set:D4}/" : $"chara/accessory/a{set:D4}/";

    /// <summary>Identifiant court : "e0205" ou "a0029".</summary>
    public static string Tag(PieceKind kind, int set)
        => kind == PieceKind.Equipment ? $"e{set:D4}" : $"a{set:D4}";
}
