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
    /// Règle les cases « appliquer » du design.
    /// - Équipement (hors armes) : toujours coché, teintures comprises.
    /// - Avec <paramref name="fullAppearance"/> : toute l'apparence du perso est cochée (visage, yeux, peau, corps…).
    /// - Sinon : seulement la coiffure et le maquillage, selon les options ; le reste du perso n'est pas touché.
    /// </summary>
    public void RestrictToOutfit(bool hair, bool makeup, bool fullAppearance)
    {
        if (Root["Customize"] is JsonObject customize)
        {
            foreach (var (key, value) in customize)
            {
                if (value is not JsonObject c || !c.ContainsKey("Apply"))
                    continue;
                var keep = fullAppearance
                           || (hair && Array.IndexOf(HairKeys, key) >= 0)
                           || (makeup && Array.IndexOf(MakeupKeys, key) >= 0);
                c["Apply"] = keep;
            }
        }

        if (Root["Parameters"] is JsonObject parameters)
            foreach (var (_, value) in parameters)
                if (value is JsonObject p && p.ContainsKey("Apply"))
                    p["Apply"] = fullAppearance;

        if (Root["Equipment"] is JsonObject equipment)
        {
            foreach (var (key, value) in equipment)
            {
                if (value is not JsonObject e)
                    continue;

                if (Slots.FromName(key) != null)
                {
                    // Pièce de la tenue : toujours appliquée, même si Glamourer n'a pas écrit la case.
                    e["Apply"] = true;
                    e["ApplyStain"] = true;
                    continue;
                }

                // Armes : jamais. Chapeau / visière : seulement avec toute l'apparence.
                if (e.ContainsKey("Apply"))
                    e["Apply"] = fullAppearance && (key == "Hat" || key == "Visor");
                if (e.ContainsKey("ApplyStain"))
                    e["ApplyStain"] = false;
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
