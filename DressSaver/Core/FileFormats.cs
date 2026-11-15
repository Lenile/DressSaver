using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DressSaver.Core;

/// <summary>Petits utilitaires de lecture des fichiers .mtrl et .mdl.</summary>
public static class FileFormats
{
    private static readonly Regex MaterialName = new(@"/?mt_[A-Za-z0-9_]+\.mtrl", RegexOptions.Compiled);

    /// <summary>Chemins des textures référencées par un matériau (.mtrl).</summary>
    public static List<string> MaterialTextures(byte[] mtrl)
    {
        var result = new List<string>();
        if (mtrl.Length < 16)
            return result;

        var stringTableSize = BitConverter.ToUInt16(mtrl, 8);
        int texCount = mtrl[12], uvCount = mtrl[13], colorSetCount = mtrl[14];
        var stringsStart = 16 + texCount * 4 + uvCount * 4 + colorSetCount * 4;
        if (stringsStart + stringTableSize > mtrl.Length)
            return result;

        for (var i = 0; i < texCount; i++)
        {
            var offset = BitConverter.ToUInt16(mtrl, 16 + i * 4);
            var start = stringsStart + offset;
            var end = start;
            while (end < stringsStart + stringTableSize && mtrl[end] != 0)
                end++;
            result.Add(Encoding.UTF8.GetString(mtrl, start, end - start).ToLowerInvariant());
        }

        return result;
    }

    /// <summary>Noms des matériaux utilisés par un modèle (.mdl), ex. "mt_c0201e0205_glv_b.mtrl".</summary>
    public static HashSet<string> ModelMaterials(byte[] mdl)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var text = Encoding.ASCII.GetString(mdl);
        foreach (Match m in MaterialName.Matches(text))
            result.Add(m.Value.TrimStart('/').ToLowerInvariant());
        return result;
    }

    /// <summary>
    /// Remplace toutes les occurrences ASCII d'un identifiant (même longueur, ex. "e0205" → "e0360").
    /// Les longueurs étant identiques, les tables d'offsets des fichiers restent valides.
    /// </summary>
    public static byte[] ReplaceTag(byte[] data, string oldTag, string newTag)
    {
        if (oldTag.Length != newTag.Length)
            throw new ArgumentException("Les identifiants doivent avoir la même longueur.");

        var result = (byte[])data.Clone();
        var oldBytes = Encoding.ASCII.GetBytes(oldTag);
        var newBytes = Encoding.ASCII.GetBytes(newTag);
        for (var i = 0; i <= result.Length - oldBytes.Length; i++)
        {
            var match = true;
            for (var j = 0; j < oldBytes.Length; j++)
            {
                if (result[i + j] != oldBytes[j])
                {
                    match = false;
                    break;
                }
            }

            if (!match)
                continue;
            Buffer.BlockCopy(newBytes, 0, result, i, newBytes.Length);
            i += oldBytes.Length - 1;
        }

        return result;
    }

    /// <summary>Version d'un modèle : 0x1000006 = format Dawntrail.</summary>
    public static bool IsDawntrailModel(byte[] mdl) => mdl.Length >= 4 && BitConverter.ToUInt32(mdl, 0) == 0x1000006;
}
