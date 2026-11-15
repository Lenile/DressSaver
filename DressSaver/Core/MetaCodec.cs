using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;

namespace DressSaver.Core;

/// <summary>
/// Décode les métadonnées renvoyées par Penumbra (GetPlayerMetaManipulations) vers le
/// format JSON des fichiers de mod (.pmp). Gère l'ancien format JSON (version 0) et le
/// format binaire « META0001 » (version 1). Seuls IMC, EQP et EQDP sont convertis : ce sont
/// les seuls utiles pour une tenue.
/// </summary>
public static class MetaCodec
{
    private static readonly Dictionary<byte, string> ObjectTypes = new()
    {
        [2] = "DemiHuman", [3] = "Accessory", [6] = "Monster", [11] = "Equipment", [13] = "Weapon",
    };

    private static readonly Dictionary<ushort, (string Gender, string Race)> GenderRaces = new()
    {
        [101] = ("Male", "Midlander"),   [201] = ("Female", "Midlander"),
        [301] = ("Male", "Highlander"),  [401] = ("Female", "Highlander"),
        [501] = ("Male", "Elezen"),      [601] = ("Female", "Elezen"),
        [701] = ("Male", "Miqote"),      [801] = ("Female", "Miqote"),
        [901] = ("Male", "Roegadyn"),    [1001] = ("Female", "Roegadyn"),
        [1101] = ("Male", "Lalafell"),   [1201] = ("Female", "Lalafell"),
        [1301] = ("Male", "AuRa"),       [1401] = ("Female", "AuRa"),
        [1501] = ("Male", "Hrothgar"),   [1601] = ("Female", "Hrothgar"),
        [1701] = ("Male", "Viera"),      [1801] = ("Female", "Viera"),
        [104] = ("MaleNpc", "Midlander"), [204] = ("FemaleNpc", "Midlander"),
    };

    public static List<JsonObject> Decode(string? base64)
    {
        var result = new List<JsonObject>();
        if (string.IsNullOrEmpty(base64))
            return result;

        byte[] raw;
        using (var input = new MemoryStream(Convert.FromBase64String(base64)))
        using (var gz = new GZipStream(input, CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
            gz.CopyTo(output);
            raw = output.ToArray();
        }

        if (raw.Length == 0)
            return result;

        if (raw[0] == 0)
        {
            var arr = JsonNode.Parse(Encoding.UTF8.GetString(raw, 1, raw.Length - 1)) as JsonArray;
            if (arr != null)
                foreach (var n in arr)
                    if (n is JsonObject o)
                        result.Add((JsonObject)o.DeepClone());
            return result;
        }

        if (raw[0] == 1)
            DecodeBinary(raw, result);

        return result;
    }

    private static void DecodeBinary(byte[] raw, List<JsonObject> result)
    {
        // 1 octet de version + "META0001"
        if (raw.Length < 9 || Encoding.ASCII.GetString(raw, 1, 8) != "META0001")
            throw new InvalidDataException("Format de métadonnées Penumbra inconnu.");

        using var br = new BinaryReader(new MemoryStream(raw, 9, raw.Length - 9));

        // IMC : identifiant 8 octets + entrée 6 octets
        var count = br.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var primary = br.ReadUInt16();
            var variant = br.ReadByte();
            var objType = br.ReadByte();
            var secondary = br.ReadUInt16();
            var equipSlot = br.ReadByte();
            br.ReadByte(); // BodySlot
            var materialId = br.ReadByte();
            var decalId = br.ReadByte();
            var attrSound = br.ReadUInt16();
            var vfxId = br.ReadByte();
            var matAnim = br.ReadByte();

            result.Add(new JsonObject
            {
                ["Type"] = "Imc",
                ["Manipulation"] = new JsonObject
                {
                    ["Entry"] = new JsonObject
                    {
                        ["MaterialId"] = materialId,
                        ["DecalId"] = decalId,
                        ["VfxId"] = vfxId,
                        ["MaterialAnimationId"] = matAnim,
                        ["AttributeMask"] = attrSound & 0x3FF,
                        ["SoundId"] = attrSound >> 10,
                    },
                    ["ObjectType"] = ObjectTypes.GetValueOrDefault(objType, objType.ToString()),
                    ["PrimaryId"] = primary,
                    ["SecondaryId"] = secondary,
                    ["Variant"] = variant,
                    ["EquipSlot"] = Slots.FromGameSlot(equipSlot)?.Name ?? "Unknown",
                    ["BodySlot"] = "Unknown",
                },
            });
        }

        // EQP : SetId u16, Slot u16, Entry u64
        count = br.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var set = br.ReadUInt16();
            var slot = (byte)br.ReadUInt16();
            var entry = br.ReadUInt64();
            var slotName = Slots.FromGameSlot(slot)?.Name;
            if (slotName == null)
                continue;
            result.Add(new JsonObject
            {
                ["Type"] = "Eqp",
                ["Manipulation"] = new JsonObject { ["Entry"] = entry, ["SetId"] = set, ["Slot"] = slotName },
            });
        }

        // EQDP : SetId u16, Slot u16, GenderRace u16, Entry u16
        count = br.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var set = br.ReadUInt16();
            var slot = (byte)br.ReadUInt16();
            var genderRace = br.ReadUInt16();
            var entry = br.ReadUInt16();
            var slotName = Slots.FromGameSlot(slot)?.Name;
            if (slotName == null || !GenderRaces.TryGetValue(genderRace, out var gr))
                continue;
            result.Add(new JsonObject
            {
                ["Type"] = "Eqdp",
                ["Manipulation"] = new JsonObject
                {
                    ["Entry"] = entry, ["Gender"] = gr.Gender, ["Race"] = gr.Race, ["SetId"] = set, ["Slot"] = slotName,
                },
            });
        }
        // Les sections suivantes (EST, RSP, GMP...) ne concernent pas les tenues : ignorées.
    }

    /// <summary>Numéro d'objet visé par une manipulation (SetId ou PrimaryId), -1 si absent.</summary>
    public static int SetOf(JsonObject m)
    {
        var man = m["Manipulation"] as JsonObject;
        var node = man?["SetId"] ?? man?["PrimaryId"];
        if (node == null)
            return -1;
        return int.TryParse(node.ToString(), out var v) ? v : -1;
    }

    /// <summary>Emplacement visé par une manipulation ("Body", "Ears"...), null si absent.</summary>
    public static string? SlotOf(JsonObject m)
    {
        var man = m["Manipulation"] as JsonObject;
        return (man?["Slot"] ?? man?["EquipSlot"])?.ToString();
    }

    public static string? TypeOf(JsonObject m) => m["Type"]?.ToString();

    public static string? ObjectTypeOf(JsonObject m) => (m["Manipulation"] as JsonObject)?["ObjectType"]?.ToString();
}
