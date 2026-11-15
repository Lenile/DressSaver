using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using DressSaver.Core;

namespace DressSaver.Windows;

public sealed class MainWindow : Window
{
    // Palette
    private static readonly Vector4 Accent = new(0.78f, 0.62f, 1f, 1f);
    private static readonly Vector4 Red = new(1f, 0.45f, 0.45f, 1f);
    private static readonly Vector4 Green = new(0.45f, 0.85f, 0.55f, 1f);
    private static readonly Vector4 Grey = new(0.62f, 0.62f, 0.66f, 1f);
    private static readonly Vector4 Yellow = new(1f, 0.82f, 0.4f, 1f);
    private static readonly Vector4 Blue = new(0.5f, 0.75f, 1f, 1f);

    private static readonly Vector4 CreateButton = new(0.22f, 0.55f, 0.32f, 1f);
    private static readonly Vector4 CreateButtonHover = new(0.28f, 0.66f, 0.39f, 1f);
    private static readonly Vector4 CreateButtonActive = new(0.18f, 0.46f, 0.27f, 1f);

    private readonly Plugin plugin;

    // Onglet Tenue
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

    /// <summary>Nom de la tenue existante à remplacer (mode « mise à jour » lancé depuis Mes tenues).</summary>
    private string? updateTarget;
    private bool selectOutfitTab;

    // Dépendances
    private List<DependencyState> dependencies = new();
    private bool DependenciesOk => dependencies.Count > 0 && dependencies.All(d => d.Level != DependencyLevel.Missing);

    // Onglet Mes tenues
    private ModScanner? wardrobe;
    private string wardrobeFilter = string.Empty;
    private string wardrobeStatus = string.Empty;
    private Dictionary<Guid, string> designList = new();
    private string designFilter = string.Empty;
    private bool wardrobeStatusIsError;

    // Activation du nouveau mod (puis désactivation des mods d'origine et application du design),
    // une fois que Penumbra a fini l'installation. Géré par Tick(), appelé à chaque image même fenêtre fermée.
    private string? pendingModName;
    private List<SourceMod> pendingSources = new();
    private bool pendingDisableSources;
    private Guid pendingDesignId = Guid.Empty;
    private Guid pendingLinkDesignId = Guid.Empty;
    private DateTime pendingDeadline;
    private DateTime nextPoll;

    public MainWindow(Plugin plugin) : base("DressSaver##DressSaverMain")
    {
        this.plugin = plugin;
        Size = new Vector2(660, 640);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(540, 440),
            MaximumSize = new Vector2(1600, 1600),
        };
    }

    public override void OnOpen()
    {
        CheckDependencies();
        wardrobe = null; // relu à la prochaine ouverture de l'onglet
    }

    private void CheckDependencies()
    {
        try
        {
            dependencies = Dependencies.Check(Plugin.PluginInterface);
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "DressSaver: dependency check failed");
            dependencies = new List<DependencyState>();
        }
    }

    // ------------------------------------------------------------------ Mise en page

    public override void Draw()
    {
        ImGui.TextColored(Accent, "DressSaver");
        ImGui.SameLine();
        ImGui.TextColored(Grey, Loc.T("subtitle"));
        ImGui.Spacing();

        DrawDependencies();

        if (!ImGui.BeginTabBar("##dresssaver_tabs"))
            return;

        var outfitFlags = selectOutfitTab ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        selectOutfitTab = false;
        if (ImGui.BeginTabItem(Loc.T("tab_outfit") + "###tab_outfit", outfitFlags))
        {
            DrawOutfitTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(Loc.T("tab_wardrobe") + "###tab_wardrobe"))
        {
            DrawWardrobeTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(Loc.T("settings") + "###tab_settings"))
        {
            DrawSettingsTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(Loc.T("diagnostic") + "###tab_diag"))
        {
            DrawDiagnosticsTab();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private static void Heading(string text)
    {
        ImGui.Spacing();
        ImGui.TextColored(Accent, text);
        ImGui.Separator();
        ImGui.Spacing();
    }

    private static void Icon(FontAwesomeIcon icon, Vector4 color)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(color, icon.ToIconString());
        ImGui.PopFont();
    }

    private static void Tooltip(string text)
    {
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(text);
    }

    private static void Wrapped(Vector4 color, string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    private void DrawDependencies()
    {
        var problems = dependencies.Where(d => d.Level != DependencyLevel.Ok).ToList();
        if (dependencies.Count > 0 && problems.Count == 0)
            return;

        var blocking = !DependenciesOk;
        if (blocking)
            Wrapped(Red, Loc.T("dep_blocking"));

        foreach (var d in problems)
        {
            Icon(d.Level == DependencyLevel.Missing ? FontAwesomeIcon.TimesCircle : FontAwesomeIcon.ExclamationTriangle,
                 d.Level == DependencyLevel.Missing ? Red : Yellow);
            ImGui.SameLine();
            Wrapped(d.Level == DependencyLevel.Missing ? Red : Yellow, $"{d.Name} : {d.Detail}");
        }

        if (ImGui.Button(Loc.T("dep_recheck") + "##recheck"))
            CheckDependencies();
        ImGui.Separator();
    }

    // ------------------------------------------------------------------ Onglet Tenue

    private void DrawOutfitTab()
    {
        ImGui.Spacing();
        var fullWidth = ImGui.GetContentRegionAvail().X;

        if (updateTarget != null)
        {
            Icon(FontAwesomeIcon.SyncAlt, Blue);
            ImGui.SameLine();
            Wrapped(Blue, Loc.T("update_banner", Names.Display(updateTarget)));
            if (ImGui.SmallButton(Loc.T("update_cancel") + "##cancel_update"))
            {
                updateTarget = null;
                outfit = null;
                status = string.Empty;
            }

            ImGui.Spacing();
        }
        else
        {
            ImGui.SetNextItemWidth(fullWidth);
            ImGui.InputTextWithHint("##outfit_name", Loc.T("name_hint"), ref outfitName, 128);
        }

        ImGui.TextColored(Grey, Loc.T("include_label"));
        ImGui.SameLine();
        ImGui.Checkbox(Loc.T("accessories"), ref includeAccessories);
        ImGui.SameLine();
        ImGui.Checkbox(Loc.T("hair"), ref includeHair);
        ImGui.SameLine();
        ImGui.Checkbox(Loc.T("makeup"), ref includeMakeup);

        ImGui.Spacing();
        if (!DependenciesOk)
            ImGui.BeginDisabled();
        if (ImGui.Button(Loc.T("analyze") + "##analyze", new Vector2(fullWidth, 30)))
            Analyze();
        if (!DependenciesOk)
            ImGui.EndDisabled();

        if (!string.IsNullOrEmpty(status))
        {
            ImGui.Spacing();
            Wrapped(statusIsError ? Red : Green, status);
        }

        if (outfit == null)
        {
            ImGui.Spacing();
            Wrapped(Grey, Loc.T("hint_equip"));
            return;
        }

        Heading(Loc.T("pieces_title"));
        DrawPiecesTable(outfit);

        if (outfit.Pieces.Any(p => p.OldModel) || outfit.OldHairModel)
        {
            ImGui.Spacing();
            if (outfit.OldHairModel)
                Wrapped(Yellow, Loc.T("old_model_hair"));
            Wrapped(Yellow, Loc.T("old_model_banner"));
        }

        DrawConflicts(outfit);

        if (outfit.HairConflicts.Count > 0)
        {
            var mods = string.Join(", ", outfit.HairConflicts.Select(c => c.ModName).Distinct());
            ImGui.Spacing();
            Wrapped(Red, Loc.T("hair_conflict", outfit.HairStyle, mods));
        }

        DrawSources(outfit);

        var credits = PmpWriter.BuildCredits(outfit).Trim();
        if (credits.Length > 0 && ImGui.CollapsingHeader(Loc.T("credits_preview") + "###credits"))
            Wrapped(Grey, credits);

        DrawCreateButton(outfit);
    }

    private void DrawPiecesTable(Outfit o)
    {
        if (o.Pieces.Count == 0)
        {
            Wrapped(Grey, Loc.T("no_pieces"));
            return;
        }

        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX;
        if (!ImGui.BeginTable("##pieces", 3, flags))
            return;

        ImGui.TableSetupColumn(Loc.T("col_slot"), ImGuiTableColumnFlags.WidthStretch, 1.1f);
        ImGui.TableSetupColumn(Loc.T("col_item"), ImGuiTableColumnFlags.WidthStretch, 2.6f);
        ImGui.TableSetupColumn(Loc.T("col_status"), ImGuiTableColumnFlags.WidthStretch, 1.0f);
        ImGui.TableHeadersRow();

        foreach (var piece in o.Pieces)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextColored(Grey, Loc.Slot(piece.Slot.Name));

            ImGui.TableNextColumn();
            if (piece.OldModel)
            {
                Icon(FontAwesomeIcon.History, Yellow);
                Tooltip(Loc.T("old_model_tip"));
                ImGui.SameLine();
            }

            ImGui.TextUnformatted(piece.ItemName);
            Tooltip(Loc.T("model_tip", piece.Tag));
            if (piece.Replacement != null && !piece.IgnoreConflict && piece.Conflicts.Count > 0)
                ImGui.TextColored(Yellow, "→ " + piece.Replacement.Name);

            ImGui.TableNextColumn();
            DrawPieceState(piece);
        }

        ImGui.EndTable();
    }

    private static void DrawPieceState(OutfitPiece piece)
    {
        FontAwesomeIcon icon;
        Vector4 color;
        string label;
        string tip;

        if (piece.Conflicts.Count > 0)
        {
            var mods = string.Join(", ", piece.Conflicts.Select(c => c.ModName).Distinct());
            if (piece.IgnoreConflict)
            {
                (icon, color, label) = (FontAwesomeIcon.EyeSlash, Grey, Loc.T("state_ignored"));
                tip = Loc.T("conflict_with", mods);
            }
            else if (piece.Replacement != null)
            {
                (icon, color, label) = (FontAwesomeIcon.ArrowRight, Yellow, Loc.T("state_moved"));
                tip = Loc.T("moved_to", piece.Replacement.Name);
            }
            else
            {
                (icon, color, label) = (FontAwesomeIcon.ExclamationTriangle, Red, Loc.T("state_conflict"));
                tip = Loc.T("conflict_with", mods);
            }
        }
        else if (piece.SharedWith.Count > 0)
        {
            (icon, color, label) = (FontAwesomeIcon.Link, Blue, Loc.T("state_shared"));
            tip = Loc.T("shared_with", string.Join(", ", piece.SharedWith));
        }
        else
        {
            (icon, color, label) = (FontAwesomeIcon.Check, Green, Loc.T("state_free"));
            tip = Loc.T("tip_free");
        }

        ImGui.BeginGroup();
        Icon(icon, color);
        ImGui.SameLine();
        ImGui.TextColored(color, label);
        ImGui.EndGroup();
        Tooltip(tip);
    }

    private void DrawConflicts(Outfit o)
    {
        var conflicting = o.Pieces.Where(p => p.Conflicts.Count > 0).ToList();
        if (conflicting.Count == 0)
            return;

        Heading(Loc.T("conflicts_title"));
        Wrapped(Grey, Loc.T("picker_help"));
        ImGui.Spacing();

        foreach (var piece in conflicting)
        {
            ImGui.PushID(piece.GetHashCode());

            Icon(piece.IgnoreConflict ? FontAwesomeIcon.EyeSlash : piece.Replacement != null ? FontAwesomeIcon.ArrowRight : FontAwesomeIcon.ExclamationTriangle,
                 piece.IgnoreConflict ? Grey : piece.Replacement != null ? Yellow : Red);
            ImGui.SameLine();
            ImGui.TextUnformatted($"{Loc.Slot(piece.Slot.Name)} : {piece.ItemName}");
            var mods = string.Join(", ", piece.Conflicts.Select(c => c.ModName).Distinct());
            ImGui.TextColored(Grey, "   " + Loc.T("conflict_with", mods));

            ImGui.Indent();
            var ignore = piece.IgnoreConflict;
            if (ImGui.Checkbox(Loc.T("ignore_conflict"), ref ignore))
                piece.IgnoreConflict = ignore;

            if (!piece.IgnoreConflict)
                DrawReplacementPicker(o, piece);
            ImGui.Unindent();

            ImGui.Spacing();
            ImGui.PopID();
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

        var width = ImGui.GetContentRegionAvail().X;
        ImGui.SetNextItemWidth(width * 0.4f);
        if (ImGui.InputTextWithHint("##search", Loc.T("search_hint"), ref filter, 64))
            filters[piece] = filter;

        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
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

        ImGui.TextColored(Grey, Loc.T("free_count", list.Count));
    }

    private void DrawSources(Outfit o)
    {
        var config = plugin.Configuration;
        if (!config.AutoInstall || !config.DisableSourceMods)
            return;

        Heading(Loc.T("sources_title"));
        var sources = o.SourceMods.Where(s => !IsReplacedMod(s.Directory)).ToList();
        if (sources.Count == 0)
        {
            Wrapped(Grey, Loc.T("no_sources"));
            return;
        }

        foreach (var src in sources)
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

        ImGui.Spacing();
        Icon(FontAwesomeIcon.ExclamationCircle, Yellow);
        ImGui.SameLine();
        Wrapped(Yellow, Loc.T("freeze_short"));
    }

    /// <summary>Nom final du mod : la tenue mise à jour garde son nom exact, sinon préfixe + nom saisi.</summary>
    private string ModName => updateTarget ?? (plugin.Configuration.ModPrefix + " " + outfitName).Trim();

    /// <summary>Dossier du mod existant portant ce nom, et s'il s'agit d'une tenue DressSaver.</summary>
    private (string? Folder, bool IsOutfit) ExistingMod(string name)
    {
        if (scanner == null || !scanner.FoldersByName.TryGetValue(name, out var folder))
            return (null, false);
        return (folder, scanner.OutfitModFolders.ContainsKey(folder));
    }

    /// <summary>Ce dossier est-il le mod qui va être remplacé par la création en cours ?</summary>
    private bool IsReplacedMod(string folder)
    {
        var (existing, isOutfit) = ExistingMod(ModName);
        return isOutfit && string.Equals(existing, folder, StringComparison.OrdinalIgnoreCase);
    }

    private void DrawCreateButton(Outfit o)
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var name = ModName;
        var noName = string.IsNullOrWhiteSpace(updateTarget ?? outfitName);
        var (existing, isOutfit) = noName ? ((string?)null, false) : ExistingMod(name);
        var nameTaken = existing != null && !isOutfit;

        if (existing != null && updateTarget == null)
        {
            Icon(isOutfit ? FontAwesomeIcon.ExclamationTriangle : FontAwesomeIcon.TimesCircle, isOutfit ? Yellow : Red);
            ImGui.SameLine();
            Wrapped(isOutfit ? Yellow : Red, isOutfit ? Loc.T("name_taken_ours", Names.Display(name)) : Loc.T("name_taken_other", Names.Display(name)));
            ImGui.Spacing();
        }

        var blocked = o.HasConflicts || noName || nameTaken;
        var label = updateTarget != null ? Loc.T("create_update", Names.Display(name))
                  : existing != null && isOutfit ? Loc.T("create_replace", Names.Display(name))
                  : Loc.T("create");

        if (blocked)
            ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.Button, CreateButton);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, CreateButtonHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, CreateButtonActive);
        if (ImGui.Button(label + "##create", new Vector2(ImGui.GetContentRegionAvail().X, 36)))
            Create();
        ImGui.PopStyleColor(3);
        if (blocked)
            ImGui.EndDisabled();

        if (noName)
            ImGui.TextColored(Yellow, Loc.T("need_name"));
        else if (nameTaken)
            ImGui.TextColored(Yellow, Loc.T("need_other_name"));
        else if (o.HasConflicts)
            ImGui.TextColored(Yellow, Loc.T("need_replacement"));
        else
            ImGui.TextColored(Grey, Loc.T("ready"));
    }

    // ------------------------------------------------------------------ Onglet Mes tenues

    private void DrawWardrobeTab()
    {
        ImGui.Spacing();
        if (!DependenciesOk)
        {
            Wrapped(Grey, Loc.T("dep_blocking"));
            return;
        }

        if (wardrobe == null)
            RefreshWardrobe();

        var outfits = wardrobe!.OutfitModFolders
            .Where(kv => wardrobeFilter.Length == 0 || Names.Simplify(kv.Value).Contains(Names.Simplify(wardrobeFilter), StringComparison.Ordinal))
            .OrderBy(kv => kv.Value, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.6f);
        ImGui.InputTextWithHint("##wardrobe_search", Loc.T("wardrobe_search_hint"), ref wardrobeFilter, 64);
        ImGui.SameLine();
        if (ImGui.Button(Loc.T("wardrobe_refresh") + "##wardrobe_refresh"))
            RefreshWardrobe();
        ImGui.SameLine();
        ImGui.TextColored(Grey, Loc.T("wardrobe_count", wardrobe.OutfitModFolders.Count));

        if (!string.IsNullOrEmpty(wardrobeStatus))
            Wrapped(wardrobeStatusIsError ? Red : Green, wardrobeStatus);

        ImGui.Spacing();
        if (wardrobe.OutfitModFolders.Count == 0)
        {
            Wrapped(Grey, Loc.T("wardrobe_empty"));
            return;
        }

        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX;
        if (!ImGui.BeginTable("##wardrobe", 3, flags))
            return;

        ImGui.TableSetupColumn(Loc.T("tab_outfit"), ImGuiTableColumnFlags.WidthStretch, 3f);
        ImGui.TableSetupColumn(Loc.T("col_design"), ImGuiTableColumnFlags.WidthStretch, 2.4f);
        ImGui.TableSetupColumn("##actions", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableHeadersRow();

        foreach (var (folder, name) in outfits)
        {
            ImGui.PushID(folder);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            Icon(FontAwesomeIcon.Tshirt, Accent);
            ImGui.SameLine();
            ImGui.TextUnformatted(Names.Display(name));

            ImGui.TableNextColumn();
            DrawDesignPicker(folder);

            ImGui.TableNextColumn();
            if (ImGui.Button(Loc.T("wardrobe_wear") + "##wear"))
                Wear(folder, name);
            Tooltip(Loc.T("wardrobe_wear_tip"));
            ImGui.SameLine();
            if (ImGui.Button(Loc.T("wardrobe_update") + "##update"))
                StartUpdate(name);
            Tooltip(Loc.T("wardrobe_update_tip"));

            ImGui.PopID();
        }

        ImGui.EndTable();
    }

    /// <summary>Liste déroulante pour choisir (ou corriger) le design Glamourer d'une tenue.</summary>
    private void DrawDesignPicker(string folder)
    {
        var linked = LinkedDesign(folder);
        var preview = linked is { } id && designList.TryGetValue(id, out var dn) ? Names.Display(dn) : Loc.T("design_pick");

        ImGui.SetNextItemWidth(-1);
        if (linked == null)
            ImGui.PushStyleColor(ImGuiCol.Text, Yellow);
        var open = ImGui.BeginCombo("##design", preview);
        if (linked == null)
            ImGui.PopStyleColor();
        Tooltip(Loc.T("design_pick_tip"));
        if (!open)
            return;

        if (ImGui.IsWindowAppearing())
        {
            designFilter = string.Empty;
            ImGui.SetKeyboardFocusHere();
        }

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##design_search", Loc.T("search_hint"), ref designFilter, 64);

        if (ImGui.Selectable(Loc.T("design_unlink") + "##unlink", linked == null))
            LinkDesign(folder, null);

        var filter = Names.Simplify(designFilter);
        foreach (var (designId, designName) in designList
                     .Where(d => filter.Length == 0 || Names.Simplify(d.Value).Contains(filter, StringComparison.Ordinal))
                     .OrderBy(d => d.Value, StringComparer.CurrentCultureIgnoreCase)
                     .Take(300))
        {
            if (ImGui.Selectable(Names.Display(designName) + "##" + designId, linked == designId))
                LinkDesign(folder, designId);
        }

        ImGui.EndCombo();
    }

    /// <summary>Design associé à la tenue (s'il existe toujours dans Glamourer).</summary>
    private Guid? LinkedDesign(string folder)
        => plugin.Configuration.OutfitDesigns.TryGetValue(folder, out var id) && designList.ContainsKey(id) ? id : null;

    private void LinkDesign(string folder, Guid? id)
    {
        if (id is { } g)
            plugin.Configuration.OutfitDesigns[folder] = g;
        else
            plugin.Configuration.OutfitDesigns.Remove(folder);
        plugin.SaveConfiguration();
    }

    private void RefreshWardrobe()
    {
        try
        {
            wardrobe = ModScanner.Scan(plugin.Extractor.ModDirectory);
            designList = plugin.Extractor.Designs();

            // Tenues sans design associé : on cherche un design au nom proche (accents, tirets, nom raccourci).
            var changed = false;
            foreach (var (folder, name) in wardrobe.OutfitModFolders)
            {
                if (LinkedDesign(folder) != null)
                    continue;
                if (Names.BestDesign(name, designList) is { } id)
                {
                    plugin.Configuration.OutfitDesigns[folder] = id;
                    changed = true;
                }
            }

            if (changed)
                plugin.SaveConfiguration();
        }
        catch (Exception e)
        {
            wardrobe = new ModScanner();
            wardrobeStatus = e.Message;
            wardrobeStatusIsError = true;
        }
    }

    private void Wear(string folder, string name)
    {
        try
        {
            var enabled = plugin.Extractor.SetModEnabled(folder, true);
            var design = LinkedDesign(folder) is { } id
                ? plugin.Extractor.ApplyDesign(id)
                : Loc.T("design_none");
            wardrobeStatus = Loc.T("wardrobe_worn", Names.Display(name), enabled, design);
            wardrobeStatusIsError = LinkedDesign(folder) == null;
        }
        catch (Exception e)
        {
            wardrobeStatus = e.Message;
            wardrobeStatusIsError = true;
            Plugin.Log.Error(e, "DressSaver: wear failed");
        }
    }

    private void StartUpdate(string name)
    {
        updateTarget = name;
        selectOutfitTab = true;
        Analyze();
    }

    // ------------------------------------------------------------------ Onglet Réglages

    private void DrawSettingsTab()
    {
        var config = plugin.Configuration;

        Heading(Loc.T("section_general"));
        var currentName = Loc.Languages.First(l => l.Code == Loc.Current).Name;
        ImGui.SetNextItemWidth(220);
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

        Heading(Loc.T("section_files"));
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

        Heading(Loc.T("section_penumbra"));
        var compareAll = config.CompareAllMods;
        if (ImGui.Checkbox(Loc.T("compare_all"), ref compareAll))
        {
            config.CompareAllMods = compareAll;
            plugin.SaveConfiguration();
        }

        var ignoreSources = config.IgnoreSourceModsInConflicts;
        if (ImGui.Checkbox(Loc.T("ignore_sources"), ref ignoreSources))
        {
            config.IgnoreSourceModsInConflicts = ignoreSources;
            plugin.SaveConfiguration();
        }

        ImGui.Indent();
        Wrapped(Grey, Loc.T("ignore_sources_info"));
        ImGui.Unindent();

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

        ImGui.Indent();
        Wrapped(Grey, Loc.T("disable_info1"));
        Wrapped(Grey, Loc.T("disable_info2"));
        Wrapped(Yellow, Loc.T("freeze_warning"));
        ImGui.Unindent();

        Heading(Loc.T("section_glamourer"));
        var save = config.SaveDesignToGlamourer;
        if (ImGui.Checkbox(Loc.T("save_design"), ref save))
        {
            config.SaveDesignToGlamourer = save;
            plugin.SaveConfiguration();
        }

        var canApply = config.SaveDesignToGlamourer && config.AutoInstall;
        if (!canApply)
            ImGui.BeginDisabled();
        var apply = config.ApplyDesignAfterCreate;
        if (ImGui.Checkbox(Loc.T("apply_design"), ref apply))
        {
            config.ApplyDesignAfterCreate = apply;
            plugin.SaveConfiguration();
        }

        if (!canApply)
            ImGui.EndDisabled();

        ImGui.Indent();
        Wrapped(Grey, Loc.T("apply_design_info"));
        ImGui.Unindent();
    }

    // ------------------------------------------------------------------ Onglet Diagnostic

    private void DrawDiagnosticsTab()
    {
        ImGui.Spacing();
        foreach (var d in dependencies)
        {
            var color = d.Level switch
            {
                DependencyLevel.Ok => Green,
                DependencyLevel.Outdated => Yellow,
                _ => Red,
            };
            ImGui.TextColored(color, $"{d.Name} : {d.Detail}");
        }

        ImGui.Spacing();
        if (scanner == null || outfit == null)
        {
            Wrapped(Grey, Loc.T("hint_equip"));
            return;
        }

        var o = outfit;
        Wrapped(Grey, Loc.T("diag_folder", scanner.ScannedDirectory));
        ImGui.TextUnformatted(Loc.T("diag_mods", scanner.ModCount, scanner.Files.Count));
        ImGui.TextUnformatted(Loc.T("files_summary",
            o.InGroup(FileGroup.Tenue).Count(),
            o.InGroup(FileGroup.Accessoires).Count(),
            o.InGroup(FileGroup.Cheveux).Count(),
            o.InGroup(FileGroup.Maquillage).Count(),
            o.Meta.Count));
        ImGui.TextUnformatted(Loc.T("diag_files", o.DiagPieceFiles, o.DiagSameFiles, o.DiagDifferentFiles));

        ImGui.Spacing();
        ImGui.TextUnformatted(Loc.T("diag_outfit_mods", scanner.OutfitMods.Count));
        foreach (var name in scanner.OutfitMods.OrderBy(n => n).Take(40))
            ImGui.TextColored(Grey, $"   {name}");

        if (scanner.Errors.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(Red, Loc.T("diag_errors", scanner.Errors.Count));
            foreach (var err in scanner.Errors.Take(8))
                ImGui.TextColored(Grey, $"   {err}");
        }

        ImGui.Spacing();
        if (ImGui.Button(Loc.T("diag_copy")))
        {
            var deps = string.Join(" | ", dependencies.Select(d => $"{d.Name}: {d.Level} {d.Detail}"));
            var text = $"Dependencies: {deps}\nFolder: {scanner.ScannedDirectory}\nMods: {scanner.ModCount}\nFiles: {scanner.Files.Count}\n"
                       + $"Outfit mods: {string.Join(", ", scanner.OutfitMods)}\n"
                       + $"Pieces: {o.DiagPieceFiles} / same {o.DiagSameFiles} / different {o.DiagDifferentFiles}\n"
                       + $"Errors: {string.Join(" | ", scanner.Errors.Take(20))}\n"
                       + $"Sample paths: {string.Join(", ", o.Files.Keys.Where(k => k.EndsWith(".mdl")).Take(5))}";
            ImGui.SetClipboardText(text);
        }
    }

    // ------------------------------------------------------------------ Actions

    private void Analyze()
    {
        CheckDependencies();
        if (!DependenciesOk)
        {
            SetStatus(Loc.T("dep_blocking"), true);
            return;
        }

        try
        {
            outfit = plugin.Extractor.Analyze(includeAccessories, includeHair, includeMakeup);
            scanner = ModScanner.Scan(plugin.Extractor.ModDirectory);
            // Les sources d'abord : la recherche de conflits en a besoin pour les mettre de côté.
            plugin.Extractor.FindSources(outfit, scanner);
            plugin.Extractor.FindConflicts(outfit, scanner, plugin.Configuration.CompareAllMods, plugin.Configuration.IgnoreSourceModsInConflicts);
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
            var config = plugin.Configuration;
            var modName = ModName;
            var (existingFolder, existingIsOutfit) = ExistingMod(modName);
            if (existingFolder != null && !existingIsOutfit)
                return; // nom pris par un autre mod : le bouton est normalement désactivé

            foreach (var piece in outfit.Pieces.Where(p => p.Conflicts.Count > 0 && p.Replacement != null && !p.IgnoreConflict).ToList())
                plugin.Remapper.Apply(outfit, piece, piece.Replacement!);

            outfit.Design.RestrictToOutfit(includeHair, includeMakeup);
            var pmp = PmpWriter.Write(outfit, modName, config.Author, config.OutputFolder);

            // Design : toujours en fichier .txt et dans le presse-papier, et dans Glamourer si demandé.
            var design = outfit.Design.ToBase64();
            File.WriteAllText(Path.ChangeExtension(pmp, null) + "_Glamourer.txt", design);
            ImGui.SetClipboardText(design);

            var msg = Loc.T("status_files_saved", Path.GetFileName(pmp));

            // Remplacement d'une tenue DressSaver existante (même nom) : ancien mod et ancien design supprimés.
            // Les fichiers de la tenue sont déjà en mémoire, la suppression ne les affecte pas.
            if (existingFolder != null && existingIsOutfit && config.AutoInstall)
                msg += Loc.T("status_replaced", plugin.Extractor.RemoveMod(existingFolder));

            var designId = Guid.Empty;
            if (config.SaveDesignToGlamourer)
            {
                foreach (var old in plugin.Extractor.FindDesigns(modName))
                    plugin.Extractor.DeleteDesign(old);

                var (result, id) = plugin.Extractor.SaveDesign(design, modName);
                if (result == "Success" && id != Guid.Empty)
                {
                    designId = id;
                    msg += Loc.T("status_design_saved", Names.Display(modName));
                }
                else
                {
                    msg += Loc.T("status_design_failed", result);
                }
            }
            else
            {
                msg += Loc.T("status_clipboard");
            }

            if (config.AutoInstall)
            {
                msg += Loc.T("status_install", plugin.Extractor.Install(pmp));
                pendingModName = modName;
                pendingDisableSources = config.DisableSourceMods;
                pendingSources = config.DisableSourceMods
                    ? outfit.SourceMods.Where(m => m.Selected && !string.Equals(m.Directory, existingFolder, StringComparison.OrdinalIgnoreCase)).ToList()
                    : new List<SourceMod>();
                pendingDesignId = config.ApplyDesignAfterCreate ? designId : Guid.Empty;
                pendingLinkDesignId = designId;
                if (existingFolder != null && existingIsOutfit)
                    config.OutfitDesigns.Remove(existingFolder);
                pendingDeadline = DateTime.Now.AddSeconds(30);
                nextPoll = DateTime.Now;
                msg += Loc.T("status_activating");
            }

            SetStatus(msg, false);

            // L'analyse a été modifiée par les déplacements : on repart de zéro pour la tenue suivante.
            outfit = null;
            updateTarget = null;
            wardrobe = null;
        }
        catch (Exception e)
        {
            SetStatus(Loc.T("status_create_failed", e.Message), true);
            Plugin.Log.Error(e, "DressSaver: creation failed");
        }
    }

    /// <summary>
    /// Appelé à chaque image par le plugin, même quand la fenêtre est fermée.
    /// Penumbra installe le mod en arrière-plan : on attend qu'il apparaisse, puis on l'active,
    /// on désactive les mods d'origine cochés et on applique le design. Abandon au bout de 30 secondes.
    /// </summary>
    public void Tick()
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
                    SetStatus(Loc.T("status_not_appeared", Names.Display(pendingModName)), true);
                    pendingModName = null;
                }

                return;
            }

            // La tenue est reliée à son design pour le bouton « Porter » de Mes tenues.
            if (pendingLinkDesignId != Guid.Empty)
            {
                plugin.Configuration.OutfitDesigns[dir] = pendingLinkDesignId;
                plugin.SaveConfiguration();
            }

            var results = new List<string> { Loc.T("status_enabled", Names.Display(pendingModName), plugin.Extractor.SetModEnabled(dir, true)) };

            if (pendingDisableSources)
            {
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
            }

            if (pendingDesignId != Guid.Empty)
                results.Add(Loc.T("status_design_applied", plugin.Extractor.ApplyDesign(pendingDesignId)));

            SetStatus(string.Join(" — ", results) + ".", false);
        }
        catch (Exception e)
        {
            SetStatus(Loc.T("status_activation_failed", e.Message), true);
            Plugin.Log.Error(e, "DressSaver: activation failed");
        }

        pendingModName = null;
        pendingDesignId = Guid.Empty;
        pendingLinkDesignId = Guid.Empty;
    }

    private void SetStatus(string text, bool error)
    {
        status = text;
        statusIsError = error;
    }
}
