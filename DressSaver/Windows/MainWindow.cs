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
    private static readonly Vector4 Yellow = new(1f, 0.85f, 0.4f, 1f);

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

    // Activation du nouveau mod et désactivation des mods d'origine, une fois que Penumbra a fini l'installation.
    private string? pendingModName;
    private List<SourceMod> pendingSources = new();
    private DateTime pendingDeadline;
    private DateTime nextPoll;

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
        PollPendingActivation();

        DrawSettings();

        ImGui.InputText(Loc.T("outfit_name"), ref outfitName, 128);
        ImGui.Checkbox(Loc.T("accessories"), ref includeAccessories);
        ImGui.SameLine();
        ImGui.Checkbox(Loc.T("hair"), ref includeHair);
        ImGui.SameLine();
        ImGui.Checkbox(Loc.T("makeup"), ref includeMakeup);

        if (ImGui.Button(Loc.T("analyze")))
            Analyze();

        if (!string.IsNullOrEmpty(status))
            ImGui.TextColored(statusIsError ? Red : Green, status);

        if (outfit == null)
        {
            ImGui.Separator();
            ImGui.TextWrapped(Loc.T("hint_equip"));
            return;
        }

        ImGui.Separator();
        DrawPieces(outfit);

        ImGui.Separator();
        ImGui.TextUnformatted(Loc.T("files_summary",
            outfit.InGroup(FileGroup.Tenue).Count(),
            outfit.InGroup(FileGroup.Accessoires).Count(),
            outfit.InGroup(FileGroup.Cheveux).Count(),
            outfit.InGroup(FileGroup.Maquillage).Count(),
            outfit.Meta.Count));

        DrawSources(outfit);
        DrawCredits(outfit);
        DrawDiagnostics(outfit);

        var blocked = outfit.HasConflicts || string.IsNullOrWhiteSpace(outfitName);
        if (blocked)
            ImGui.BeginDisabled();
        if (ImGui.Button(Loc.T("create")))
            Create();
        if (blocked)
        {
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.TextColored(Grey, string.IsNullOrWhiteSpace(outfitName) ? Loc.T("need_name") : Loc.T("need_replacement"));
        }
    }

    private void DrawSettings()
    {
        var config = plugin.Configuration;
        if (!ImGui.CollapsingHeader(Loc.T("settings") + "###settings"))
            return;

        // Langue
        var currentName = Loc.Languages.First(l => l.Code == Loc.Current).Name;
        ImGui.SetNextItemWidth(200);
        if (ImGui.BeginCombo(Loc.T("language"), currentName))
        {
            foreach (var (code, name) in Loc.Languages)
            {
                if (ImGui.Selectable(name, code == Loc.Current))
                {
                    Loc.Current = code;
                    config.Language = code;
                    plugin.SaveConfiguration();
                }
            }

            ImGui.EndCombo();
        }

        var folder = config.OutputFolder;
        if (ImGui.InputText(Loc.T("output_folder"), ref folder, 512))
        {
            config.OutputFolder = folder;
            plugin.SaveConfiguration();
        }

        var prefix = config.ModPrefix;
        if (ImGui.InputText(Loc.T("mod_prefix"), ref prefix, 128))
        {
            config.ModPrefix = prefix;
            plugin.SaveConfiguration();
        }

        var author = config.Author;
        if (ImGui.InputText(Loc.T("author"), ref author, 128))
        {
            config.Author = author;
            plugin.SaveConfiguration();
        }

        var compareAll = config.CompareAllMods;
        if (ImGui.Checkbox(Loc.T("compare_all"), ref compareAll))
        {
            config.CompareAllMods = compareAll;
            plugin.SaveConfiguration();
        }

        var auto = config.AutoInstall;
        if (ImGui.Checkbox(Loc.T("auto_install"), ref auto))
        {
            config.AutoInstall = auto;
            plugin.SaveConfiguration();
        }

        if (!config.AutoInstall)
            ImGui.BeginDisabled();
        var disable = config.DisableSourceMods;
        if (ImGui.Checkbox(Loc.T("disable_sources"), ref disable))
        {
            config.DisableSourceMods = disable;
            plugin.SaveConfiguration();
        }

        if (!config.AutoInstall)
            ImGui.EndDisabled();

        ImGui.TextColored(Grey, "   " + Loc.T("disable_info1"));
        ImGui.TextColored(Grey, "   " + Loc.T("disable_info2"));
        ImGui.TextColored(Yellow, "   " + Loc.T("freeze_warning"));
        ImGui.Separator();
    }

    private void DrawPieces(Outfit o)
    {
        if (o.Pieces.Count == 0)
        {
            ImGui.TextColored(Grey, Loc.T("no_pieces"));
            return;
        }

        foreach (var piece in o.Pieces)
        {
            ImGui.PushID(piece.GetHashCode());
            ImGui.TextUnformatted($"{Loc.Slot(piece.Slot.Name)} : {piece.ItemName} ({piece.Tag})");

            if (piece.Conflicts.Count > 0)
            {
                var mods = string.Join(", ", piece.Conflicts.Select(c => c.ModName).Distinct());
                ImGui.TextColored(Red, "   " + Loc.T("conflict_with", mods));
                var ignore = piece.IgnoreConflict;
                if (ImGui.Checkbox(Loc.T("ignore_conflict"), ref ignore))
                    piece.IgnoreConflict = ignore;
                if (!piece.IgnoreConflict)
                    DrawReplacementPicker(o, piece);
            }
            else if (piece.SharedWith.Count > 0)
            {
                ImGui.TextColored(Grey, "   " + Loc.T("shared_with", string.Join(", ", piece.SharedWith)));
            }

            ImGui.PopID();
        }

        if (o.HairConflicts.Count > 0)
        {
            var mods = string.Join(", ", o.HairConflicts.Select(c => c.ModName).Distinct());
            ImGui.PushTextWrapPos();
            ImGui.TextColored(Red, Loc.T("hair_conflict", o.HairStyle, mods));
            ImGui.PopTextWrapPos();
        }
    }

    private void DrawSources(Outfit o)
    {
        var config = plugin.Configuration;
        if (!config.AutoInstall || !config.DisableSourceMods)
            return;

        ImGui.Separator();
        if (o.SourceMods.Count == 0)
        {
            ImGui.TextColored(Grey, Loc.T("no_sources"));
            return;
        }

        ImGui.TextUnformatted(Loc.T("sources_title"));
        foreach (var src in o.SourceMods)
        {
            ImGui.PushID(src.Directory);
            var selected = src.Selected;
            if (ImGui.Checkbox(src.Name, ref selected))
                src.Selected = selected;
            ImGui.SameLine();
            if (src.OnlyOutfit)
                ImGui.TextColored(Grey, Loc.T("source_only", src.UsedFiles));
            else
                ImGui.TextColored(Yellow, Loc.T("source_mixed", src.UsedFiles, src.TotalFiles));
            ImGui.PopID();
        }

        ImGui.TextColored(Yellow, Loc.T("freeze_short"));
    }

    private static void DrawCredits(Outfit o)
    {
        var credits = PmpWriter.BuildCredits(o).Trim();
        if (credits.Length == 0 || !ImGui.CollapsingHeader(Loc.T("credits_preview") + "###credits"))
            return;
        ImGui.TextWrapped(credits);
    }

    private void DrawDiagnostics(Outfit o)
    {
        if (scanner == null || !ImGui.CollapsingHeader(Loc.T("diagnostic") + "###diagnostic"))
            return;

        ImGui.TextWrapped(Loc.T("diag_folder", scanner.ScannedDirectory));
        ImGui.TextUnformatted(Loc.T("diag_mods", scanner.ModCount, scanner.Files.Count));
        ImGui.TextUnformatted(Loc.T("diag_outfit_mods", scanner.OutfitMods.Count));
        foreach (var name in scanner.OutfitMods.OrderBy(n => n).Take(30))
            ImGui.TextColored(Grey, $"   {name}");
        ImGui.TextUnformatted(Loc.T("diag_files", o.DiagPieceFiles, o.DiagSameFiles, o.DiagDifferentFiles));

        if (scanner.Errors.Count > 0)
        {
            ImGui.TextColored(Red, Loc.T("diag_errors", scanner.Errors.Count));
            foreach (var err in scanner.Errors.Take(8))
                ImGui.TextColored(Grey, $"   {err}");
        }

        if (ImGui.Button(Loc.T("diag_copy")))
        {
            var text = $"Folder: {scanner.ScannedDirectory}\nMods: {scanner.ModCount}\nFiles: {scanner.Files.Count}\n"
                       + $"Outfit mods: {string.Join(", ", scanner.OutfitMods)}\n"
                       + $"Pieces: {o.DiagPieceFiles} / same {o.DiagSameFiles} / different {o.DiagDifferentFiles}\n"
                       + $"Errors: {string.Join(" | ", scanner.Errors.Take(20))}\n"
                       + $"Sample paths: {string.Join(", ", o.Files.Keys.Where(k => k.EndsWith(".mdl")).Take(5))}";
            ImGui.SetClipboardText(text);
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
        if (ImGui.InputText(Loc.T("filter"), ref filter, 64))
            filters[piece] = filter;

        ImGui.SameLine();
        ImGui.SetNextItemWidth(320);
        var preview = piece.Replacement != null
            ? $"{piece.Replacement.Name} ({Slots.Tag(piece.Slot.Kind, piece.Replacement.Set)})"
            : Loc.T("replacement_placeholder");
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

        ImGui.TextColored(Grey, "   " + Loc.T("free_count", list.Count));
    }

    private void Analyze()
    {
        try
        {
            outfit = plugin.Extractor.Analyze(includeAccessories, includeHair, includeMakeup);
            scanner = ModScanner.Scan(plugin.Extractor.ModDirectory);
            plugin.Extractor.FindConflicts(outfit, scanner, plugin.Configuration.CompareAllMods);
            plugin.Extractor.FindSources(outfit, scanner);
            freeItems.Clear();
            filters.Clear();
            var conflicts = outfit.Pieces.Count(p => p.Conflicts.Count > 0);
            var compared = plugin.Configuration.CompareAllMods ? scanner.ModCount : scanner.OutfitMods.Count;
            SetStatus(Loc.T("status_analyzed", outfit.Pieces.Count, conflicts, compared), false);
        }
        catch (Exception e)
        {
            outfit = null;
            SetStatus(Loc.T("status_analyze_failed", e.Message), true);
            Plugin.Log.Error(e, "DressSaver: analysis failed");
        }
    }

    private void Create()
    {
        if (outfit == null)
            return;

        try
        {
            foreach (var piece in outfit.Pieces.Where(p => p.Conflicts.Count > 0 && p.Replacement != null && !p.IgnoreConflict).ToList())
                plugin.Remapper.Apply(outfit, piece, piece.Replacement!);

            outfit.Design.RestrictToOutfit(includeHair, includeMakeup);

            var config = plugin.Configuration;
            var modName = (config.ModPrefix + " " + outfitName).Trim();
            var pmp = PmpWriter.Write(outfit, modName, config.Author, config.OutputFolder);

            var design = outfit.Design.ToBase64();
            File.WriteAllText(Path.ChangeExtension(pmp, null) + "_Glamourer.txt", design);
            ImGui.SetClipboardText(design);

            var msg = Loc.T("status_created", Path.GetFileName(pmp));
            if (config.AutoInstall)
            {
                msg += Loc.T("status_install", plugin.Extractor.Install(pmp));
                if (config.DisableSourceMods)
                {
                    pendingModName = modName;
                    pendingSources = outfit.SourceMods.Where(m => m.Selected).ToList();
                    pendingDeadline = DateTime.Now.AddSeconds(30);
                    nextPoll = DateTime.Now;
                    msg += Loc.T("status_activating");
                }
            }

            SetStatus(msg, false);

            // L'analyse a été modifiée par les déplacements : on repart de zéro pour la tenue suivante.
            outfit = null;
        }
        catch (Exception e)
        {
            SetStatus(Loc.T("status_create_failed", e.Message), true);
            Plugin.Log.Error(e, "DressSaver: creation failed");
        }
    }

    /// <summary>
    /// Penumbra installe le mod en arrière-plan : on attend qu'il apparaisse, puis on l'active
    /// et on désactive les mods d'origine cochés. Abandon au bout de 30 secondes.
    /// </summary>
    private void PollPendingActivation()
    {
        if (pendingModName == null || DateTime.Now < nextPoll)
            return;
        nextPoll = DateTime.Now.AddMilliseconds(500);

        try
        {
            var dir = plugin.Extractor.FindModDirectory(pendingModName);
            if (dir == null)
            {
                if (DateTime.Now > pendingDeadline)
                {
                    SetStatus(Loc.T("status_not_appeared", pendingModName), true);
                    pendingModName = null;
                }

                return;
            }

            var results = new List<string> { Loc.T("status_enabled", pendingModName, plugin.Extractor.SetModEnabled(dir, true)) };
            var disabled = 0;
            foreach (var src in pendingSources)
            {
                if (string.Equals(src.Directory, dir, StringComparison.OrdinalIgnoreCase))
                    continue;
                var r = plugin.Extractor.SetModEnabled(src.Directory, false);
                if (r is "Success" or "NothingChanged")
                    disabled++;
                else
                    results.Add($"{src.Name} : {r}");
            }

            results.Add(Loc.T("status_disabled_count", disabled));
            SetStatus(string.Join(" — ", results) + ".", false);
        }
        catch (Exception e)
        {
            SetStatus(Loc.T("status_activation_failed", e.Message), true);
            Plugin.Log.Error(e, "DressSaver: activation failed");
        }

        pendingModName = null;
    }

    private void SetStatus(string text, bool error)
    {
        status = text;
        statusIsError = error;
    }
}
