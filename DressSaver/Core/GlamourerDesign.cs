using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;

namespace DressSaver.Core;

/// <summary>
/// Lecture/écriture des designs Glamourer au format presse-papier :
/// base64( version + gzip(JSON) ).
/// </summary>
public sealed class GlamourerDesign
{
    private static readonly string[] HairKeys = { "Hairstyle", "HairColor", "Highlights", "HighlightsColor" };
    private static readonly string[] MakeupKeys = { "FacePaint", "FacePaintReversed", "FacePaintColor", "Lipstick", "LipColor" };

    public JsonObject Root { get; }
    private readonly byte version;

    private GlamourerDesign(JsonObject root, byte version)
    {
        Root = root;
        this.version = version;
    }

    public static GlamourerDesign Parse(string base64)
    {
        var data = Convert.FromBase64String(base64);
        var ver = data[0];
        using var input = new MemoryStream(data, 1, data.Length - 1);
        using var gz = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        try
        {
            gz.CopyTo(output);
        }
        catch (InvalidDataException)
        {
            // Certaines chaînes ont un flux gzip tronqué : on garde ce qui a été lu.
        }

        var json = JsonNode.Parse(Encoding.UTF8.GetString(output.ToArray())) as JsonObject
                   ?? throw new InvalidDataException("Unreadable Glamourer design.");
        return new GlamourerDesign(json, ver);
    }

    public string ToBase64()
    {
        var json = Encoding.UTF8.GetBytes(Root.ToJsonString());
        using var output = new MemoryStream();
        output.WriteByte(version);
        using (var gz = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            gz.Write(json, 0, json.Length);
        return Convert.ToBase64String(output.ToArray());
    }

    public int Race => ReadCustomize("Race");
    public int Clan => ReadCustomize("Clan");
    public int Gender => ReadCustomize("Gender");

    private int ReadCustomize(string key)
    {
        var v = (Root["Customize"] as JsonObject)?[key]?["Value"];
        return v != null && int.TryParse(v.ToString(), out var r) ? r : 0;
    }

    /// <summary>Numéro d'objet porté sur un emplacement, 0 si inconnu.</summary>
    public uint ItemOf(string slot)
    {
        var v = (Root["Equipment"] as JsonObject)?[slot]?["ItemId"];
        return v != null && uint.TryParse(v.ToString(), out var r) ? r : 0;
    }

    public void SetItem(string slot, uint itemId)
    {
        if ((Root["Equipment"] as JsonObject)?[slot] is JsonObject e)
            e["ItemId"] = itemId;
    }

    /// <summary>
    /// Ne garde actifs que l'équipement (hors armes), et selon les options la coiffure et le maquillage.
    /// Le reste du personnage n'est pas touché quand on applique le design.
    /// </summary>
    public void RestrictToOutfit(bool hair, bool makeup)
    {
        if (Root["Customize"] is JsonObject customize)
        {
            foreach (var (key, value) in customize)
            {
                if (value is not JsonObject c || !c.ContainsKey("Apply"))
                    continue;
                var keep = (hair && Array.IndexOf(HairKeys, key) >= 0) || (makeup && Array.IndexOf(MakeupKeys, key) >= 0);
                c["Apply"] = keep;
            }
        }

        if (Root["Parameters"] is JsonObject parameters)
            foreach (var (_, value) in parameters)
                if (value is JsonObject p && p.ContainsKey("Apply"))
                    p["Apply"] = false;

        if (Root["Equipment"] is JsonObject equipment)
        {
            foreach (var (key, value) in equipment)
            {
                if (value is not JsonObject e || !e.ContainsKey("Apply"))
                    continue;
                var keep = Slots.FromName(key) != null;
                e["Apply"] = keep;
                if (e.ContainsKey("ApplyStain"))
                    e["ApplyStain"] = keep;
            }
        }
    }

    /// <summary>Nom Penumbra de la race du personnage ("AuRa", "Midlander"...).</summary>
    public string PenumbraRace => Race switch
    {
        1 => Clan == 2 ? "Highlander" : "Midlander",
        2 => "Elezen",
        3 => "Lalafell",
        4 => "Miqote",
        5 => "Roegadyn",
        6 => "AuRa",
        7 => "Hrothgar",
        8 => "Viera",
        _ => "Midlander",
    };

    public string PenumbraGender => Gender == 1 ? "Female" : "Male";
}
