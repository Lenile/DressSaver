using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using DressSaver.Core;

namespace DressSaver.Windows;

public sealed class MainWindow : Window
{
    private static readonly Vector4 Red = new(1f, 0.45f, 0.45f, 1f);
    private static readonly Vector4 Green = new(0.5f, 0.9f, 0.5f, 1f);
    private static readonly Vector4 Grey = new(0.65f, 0.65f, 0.65f, 1f);

    private readonly Plugin plugin;

    private string outfitName = string.Empty;
    private bool includeAccessories = true;
    private bool includeHair = true;
    private bool includeMakeup = true;

    private Outfit? outfit;
    private ModScanner? scanner;
    private readonly Dictionary<OutfitPiece, List<GameItem>> freeItems = new();
    private readonly Dictionary<OutfitPiece, string> filters = new();
    private string status = string.Empty;
    private bool statusIsError;

    public MainWindow(Plugin plugin) : base("DressSaver##DressSaverMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 420),
            MaximumSize = new Vector2(1400, 1400),
        };
    }

    public override void Draw()
    {
        var config = plugin.Configuration;

        if (ImGui.CollapsingHeader("Réglages"))
        {
            var folder = config.OutputFolder;
            if (ImGui.InputText("Dossier de sortie", ref folder, 512))
            {
                config.OutputFolder = folder;
                plugin.SaveConfiguration();
            }

            var prefix = config.ModPrefix;
            if (ImGui.InputText("Préfixe des mods", ref prefix, 128))
            {
                config.ModPrefix = prefix;
                plugin.SaveConfiguration();
            }

            var author = config.Author;
            if (ImGui.InputText("Auteur", ref author, 128))
            {
                config.Author = author;
                plugin.SaveConfiguration();
            }

            var auto = config.AutoInstall;
            if (ImGui.Checkbox("Installer automatiquement dans Penumbra", ref auto))
            {
                config.AutoInstall = auto;
                plugin.SaveConfiguration();
            }

            ImGui.Separator();
        }

        ImGui.InputText("Nom de la tenue", ref outfitName, 128);
        ImGui.Checkbox("Accessoires", ref includeAccessories);
        ImGui.SameLine();
        ImGui.Checkbox("Coupe de cheveux", ref includeHair);
        ImGui.SameLine();
        ImGui.Checkbox("Maquillage", ref includeMakeup);

        if (ImGui.Button("Analyser la tenue portée"))
            Analyze();

        if (!string.IsNullOrEmpty(status))
            ImGui.TextColored(statusIsError ? Red : Green, status);

        if (outfit == null)
        {
            ImGui.Separator();
            ImGui.TextWrapped("Équipe la tenue sur ton personnage (avec Glamourer et tes mods actifs), puis clique sur « Analyser ».");
            return;
        }

        ImGui.Separator();
        DrawPieces(outfit);

        ImGui.Separator();
        ImGui.TextUnformatted($"Fichiers : tenue {outfit.InGroup(FileGroup.Tenue).Count()}, "
                              + $"accessoires {outfit.InGroup(FileGroup.Accessoires).Count()}, "
                              + $"cheveux {outfit.InGroup(FileGroup.Cheveux).Count()}, "
                              + $"maquillage {outfit.InGroup(FileGroup.Maquillage).Count()} — métadonnées {outfit.Meta.Count}");

        var blocked = outfit.HasConflicts || string.IsNullOrWhiteSpace(outfitName);
        if (blocked)
            ImGui.BeginDisabled();
        if (ImGui.Button("Créer le mod"))
            Create();
        if (blocked)
        {
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.TextColored(Grey, string.IsNullOrWhiteSpace(outfitName)
                ? "Donne un nom à la tenue."
                : "Choisis un objet libre pour chaque conflit.");
        }
    }

    private void DrawPieces(Outfit o)
    {
        if (o.Pieces.Count == 0)
        {
            ImGui.TextColored(Grey, "Aucune pièce modée trouvée sur le personnage.");
            return;
        }

        foreach (var piece in o.Pieces)
        {
            ImGui.PushID(piece.GetHashCode());
            ImGui.TextUnformatted($"{piece.Slot.Label} : {piece.ItemName} ({piece.Tag})");

            if (piece.Conflicts.Count > 0)
            {
                var mods = string.Join(", ", piece.Conflicts.Select(c => c.ModName).Distinct());
                ImGui.TextColored(Red, $"   Conflit avec : {mods}");
                DrawReplacementPicker(o, piece);
            }
            else if (piece.SharedWith.Count > 0)
            {
                ImGui.TextColored(Grey, $"   Partagé à l'identique avec : {string.Join(", ", piece.SharedWith)}");
            }

            ImGui.PopID();
        }

        if (o.HairConflicts.Count > 0)
        {
            var mods = string.Join(", ", o.HairConflicts.Select(c => c.ModName).Distinct());
            ImGui.TextColored(Red, $"Coiffure n°{o.HairStyle} différente dans : {mods}. Une coiffure ne peut pas être déplacée : "
                                   + "décoche « Coupe de cheveux » ou n'active qu'un des deux mods.");
        }
    }

    private void DrawReplacementPicker(Outfit o, OutfitPiece piece)
    {
        if (!freeItems.TryGetValue(piece, out var list))
        {
            list = plugin.Extractor.FreeItems(o, piece.Slot, scanner!);
            freeItems[piece] = list;
        }

        filters.TryGetValue(piece, out var filter);
        filter ??= string.Empty;
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputText("Filtrer", ref filter, 64))
            filters[piece] = filter;

        ImGui.SameLine();
        ImGui.SetNextItemWidth(320);
        var preview = piece.Replacement != null
            ? $"{piece.Replacement.Name} ({Slots.Tag(piece.Slot.Kind, piece.Replacement.Set)})"
            : "Objet libre de remplacement…";
        if (ImGui.BeginCombo("##replacement", preview))
        {
            foreach (var item in list.Where(i => filter.Length == 0 || i.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).Take(300))
            {
                var label = $"{item.Name}  —  {Slots.Tag(piece.Slot.Kind, item.Set)} v{item.Variant}##{item.Id}";
                if (ImGui.Selectable(label, piece.Replacement?.Id == item.Id))
                    piece.Replacement = item;
            }

            ImGui.EndCombo();
        }

        ImGui.TextColored(Grey, $"   {list.Count} objets libres selon Penumbra.");
    }

    private void Analyze()
    {
        try
        {
            outfit = plugin.Extractor.Analyze(includeAccessories, includeHair, includeMakeup);
            scanner = ModScanner.Scan(plugin.Extractor.ModDirectory);
            plugin.Extractor.FindConflicts(outfit, scanner);
            freeItems.Clear();
            filters.Clear();
            var conflicts = outfit.Pieces.Count(p => p.Conflicts.Count > 0);
            SetStatus($"{outfit.Pieces.Count} pièces trouvées, {conflicts} en conflit.", false);
        }
        catch (Exception e)
        {
            outfit = null;
            SetStatus($"Analyse impossible : {e.Message}", true);
            Plugin.Log.Error(e, "DressSaver : échec de l'analyse");
        }
    }

    private void Create()
    {
        if (outfit == null)
            return;

        try
        {
            foreach (var piece in outfit.Pieces.Where(p => p.Conflicts.Count > 0 && p.Replacement != null).ToList())
                plugin.Remapper.Apply(outfit, piece, piece.Replacement!);

            outfit.Design.RestrictToOutfit(includeHair, includeMakeup);

            var config = plugin.Configuration;
            var modName = (config.ModPrefix + " " + outfitName).Trim();
            var pmp = PmpWriter.Write(outfit, modName, config.Author, config.OutputFolder);

            var design = outfit.Design.ToBase64();
            File.WriteAllText(Path.ChangeExtension(pmp, null) + "_Glamourer.txt", design);
            ImGui.SetClipboardText(design);

            var msg = $"Mod créé : {Path.GetFileName(pmp)}. Design Glamourer copié dans le presse-papier.";
            if (config.AutoInstall)
                msg += $" Installation Penumbra : {plugin.Extractor.Install(pmp)}.";
            SetStatus(msg, false);

            // L'analyse a été modifiée par les déplacements : on repart de zéro pour la tenue suivante.
            outfit = null;
        }
        catch (Exception e)
        {
            SetStatus($"Création impossible : {e.Message}", true);
            Plugin.Log.Error(e, "DressSaver : échec de la création");
        }
    }

    private void SetStatus(string text, bool error)
    {
        status = text;
        statusIsError = error;
    }
}
