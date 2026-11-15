using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DressSaver.Core;

/// <summary>Écrit une tenue sous forme de mod Penumbra (.pmp) avec ses options.</summary>
public static class PmpWriter
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static string Write(Outfit outfit, string modName, string author, string folder)
    {
        Directory.CreateDirectory(folder);
        var safe = string.Concat(modName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var path = Path.Combine(folder, safe + ".pmp");
        if (File.Exists(path))
            File.Delete(path);

        var accessorySets = outfit.Pieces.Where(p => p.Slot.Kind == PieceKind.Accessory).Select(p => p.Set).ToHashSet();
        bool IsAccessoryMeta(JsonObject m)
        {
            var slot = Slots.FromName(MetaCodec.SlotOf(m) ?? string.Empty);
            return slot is { Kind: PieceKind.Accessory } && accessorySets.Contains(MetaCodec.SetOf(m));
        }

        var tenueMeta = outfit.Meta.Where(m => !IsAccessoryMeta(m)).ToList();
        var accMeta = outfit.Meta.Where(IsAccessoryMeta).ToList();

        using var stream = File.Create(path);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);

        var groupNames = new List<string>();
        var description = Loc.T("mod_description");

        WriteJson(zip, "meta.json", new JsonObject
        {
            ["FileVersion"] = 3,
            ["Name"] = modName,
            ["Author"] = string.IsNullOrWhiteSpace(author) ? "DressSaver" : author,
            ["Description"] = description,
            ["Version"] = "1.0",
            ["Website"] = "",
            ["ModTags"] = new JsonArray(ModScanner.Tag, "Tenue", "Outfit"),
        });

        WriteJson(zip, "default_mod.json", new JsonObject
        {
            ["Version"] = 0,
            ["Files"] = WriteFiles(zip, outfit.InGroup(FileGroup.Tenue), "outfit"),
            ["FileSwaps"] = new JsonObject(),
            ["Manipulations"] = ToArray(tenueMeta),
        });

        var index = 1;
        void Group(string name, string desc, FileGroup group, string folderName, List<JsonObject> meta)
        {
            var files = outfit.InGroup(group).ToList();
            if (files.Count == 0 && meta.Count == 0)
                return;
            WriteJson(zip, $"group_{index:D3}_{folderName}.json", new JsonObject
            {
                ["Version"] = 0,
                ["Name"] = name,
                ["Description"] = desc,
                ["Image"] = "",
                ["Page"] = 0,
                ["Priority"] = 0,
                ["Type"] = "Multi",
                ["DefaultSettings"] = 1,
                ["Options"] = new JsonArray(new JsonObject
                {
                    ["Name"] = name,
                    ["Description"] = desc,
                    ["Priority"] = 0,
                    ["Files"] = WriteFiles(zip, files, folderName),
                    ["FileSwaps"] = new JsonObject(),
                    ["Manipulations"] = ToArray(meta),
                }),
            });
            index++;
        }

        Group(Loc.T("accessories"), Loc.T("grp_acc_desc"), FileGroup.Accessoires, "accessories", accMeta);
        Group(Loc.T("hair"), Loc.T("grp_hair_desc"), FileGroup.Cheveux, "hair", new List<JsonObject>());
        Group(Loc.T("makeup"), Loc.T("grp_makeup_desc"), FileGroup.Maquillage, "makeup", new List<JsonObject>());

        return path;
    }

    private static JsonObject WriteFiles(ZipArchive zip, IEnumerable<OutfitFile> files, string folder)
    {
        var map = new JsonObject();
        foreach (var f in files.OrderBy(f => f.GamePath))
        {
            var local = $"files/{folder}/{f.GamePath}";
            var entry = zip.CreateEntry(local, CompressionLevel.Optimal);
            using (var s = entry.Open())
                s.Write(f.Data, 0, f.Data.Length);
            map[f.GamePath] = local.Replace('/', '\\');
        }

        return map;
    }

    private static JsonArray ToArray(IEnumerable<JsonObject> items)
    {
        var arr = new JsonArray();
        foreach (var i in items)
            arr.Add(i.DeepClone());
        return arr;
    }

    private static void WriteJson(ZipArchive zip, string name, JsonObject content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var s = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content.ToJsonString(Indented));
        s.Write(bytes, 0, bytes.Length);
    }
}
