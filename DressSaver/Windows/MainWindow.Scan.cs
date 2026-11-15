using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using DressSaver.Core;

namespace DressSaver.Windows;

/// <summary>Onglet Mes tenues : scan des conflits entre les tenues déjà créées et les autres mods.</summary>
public sealed partial class MainWindow
{
    /// <summary>Une tenue installée, rechargée pour le scan.</summary>
    private sealed class ScannedOutfit
    {
        public required string Folder { get; init; }
        public required string Name { get; init; }
        public Guid? DesignId { get; init; }
        public Outfit? Outfit { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>Une tenue réinstallée : à relier à son nouveau design (et à réactiver) quand Penumbra l'a chargée.</summary>
    private sealed record PendingRelink(string ModName, Guid DesignId, bool Enable, DateTime Deadline);

    private Task<(List<ScannedOutfit> Outfits, ModScanner Scanner)>? scanTask;
    private List<ScannedOutfit>? scanResults;
    private ModScanner? scanScanner;
    private readonly Dictionary<OutfitPiece, List<GameItem>> scanFreeItems = new();
    private string scanFilter = string.Empty;
    private string scanStatus = string.Empty;
    private bool scanStatusIsError;
    private readonly List<PendingRelink> pendingRelinks = new();
    private DateTime nextRelinkPoll;

    private void StartConflictScan()
    {
        if (scanTask != null)
            return;

        // Les appels à Penumbra et Glamourer se font ici (fil principal) ; la lecture des fichiers en arrière-plan.
        var modDirectory = plugin.Extractor.ModDirectory;
        designList = plugin.Extractor.Designs();
        var entries = new List<(string Folder, string Name, Guid? DesignId, string? Design)>();
        foreach (var (folder, name) in (wardrobe ?? ModScanner.Scan(modDirectory)).OutfitModFolders)
        {
            var id = LinkedDesign(folder);
            entries.Add((folder, name, id, id is { } g ? plugin.Extractor.DesignBase64(g) : null));
        }

        var config = plugin.Configuration;
        scanResults = null;
        scanFreeItems.Clear();
        scanStatus = string.Empty;
        scanTask = Task.Run(() =>
        {
            var scanner = ModScanner.Scan(modDirectory);
            var result = new List<ScannedOutfit>();
            foreach (var (folder, name, designId, design) in entries.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var entry = new ScannedOutfit { Folder = folder, Name = name, DesignId = string.IsNullOrEmpty(design) ? null : designId };
                try
                {
                    // Sans design, on lit quand même la tenue pour montrer ses conflits (avec un design vide).
                    var parsed = string.IsNullOrEmpty(design) ? null : GlamourerDesign.Parse(design);
                    if (parsed != null)
                    {
                        var outfit = plugin.Extractor.LoadInstalled(folder, scanner, parsed);
                        outfit.SourceMods.Add(new SourceMod { Directory = folder, Name = name });
                        plugin.Extractor.FindConflicts(outfit, scanner, config.CompareAllMods, true);
                        entry.Outfit = outfit;
                    }
                }
                catch (Exception e)
                {
                    entry.Error = e.Message;
                }

                result.Add(entry);
            }

            return (result, scanner);
        });
    }

    private void DrawConflictScan()
    {
        if (scanTask != null)
        {
            if (!scanTask.IsCompleted)
            {
                Icon(FontAwesomeIcon.Spinner, Accent);
                ImGui.SameLine();
                ImGui.TextColored(Grey, Loc.T("scan_running"));
                ImGui.Separator();
                return;
            }

            if (scanTask.IsFaulted)
            {
                scanStatus = scanTask.Exception?.GetBaseException().Message ?? "?";
                scanStatusIsError = true;
            }
            else
            {
                (scanResults, scanScanner) = scanTask.Result;
            }

            scanTask = null;
        }

        if (!string.IsNullOrEmpty(scanStatus))
            Wrapped(scanStatusIsError ? Red : Green, scanStatus);

        if (scanResults == null)
            return;

        var rows = scanResults
            .Where(o => o.Outfit != null)
            .SelectMany(o => o.Outfit!.Pieces.Where(p => p.Conflicts.Count > 0).Select(p => (Entry: o, Piece: p)))
            .ToList();
        var noDesign = scanResults.Where(o => o.Outfit == null && o.Error == null).ToList();
        var errors = scanResults.Where(o => o.Error != null).ToList();

        Heading(Loc.T("scan_title", rows.Count));

        if (rows.Count == 0)
            Wrapped(Green, Loc.T("scan_none"));
        else
            DrawScanTable(rows);

        if (noDesign.Count > 0)
        {
            ImGui.Spacing();
            Wrapped(Yellow, Loc.T("scan_no_design", string.Join(", ", noDesign.Select(o => Names.Display(o.Name)))));
        }

        foreach (var e in errors)
            Wrapped(Red, Loc.T("scan_error", Names.Display(e.Name), e.Error!));

        ImGui.Spacing();
        var chosen = rows.Count(r => r.Piece.Replacement != null);
        if (chosen == 0)
            ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.Button, CreateButton);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, CreateButtonHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, CreateButtonActive);
        if (ImGui.Button(Loc.T("scan_apply", chosen) + "##scan_apply"))
            ApplyScanReplacements();
        ImGui.PopStyleColor(3);
        if (chosen == 0)
            ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button(Loc.T("scan_close") + "##scan_close"))
        {
            scanResults = null;
            scanStatus = string.Empty;
        }

        Wrapped(Grey, Loc.T("scan_info"));
        ImGui.Separator();
    }

    private void DrawScanTable(List<(ScannedOutfit Entry, OutfitPiece Piece)> rows)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX;
        if (!ImGui.BeginTable("##scan", 4, flags))
            return;

        ImGui.TableSetupColumn(Loc.T("col_outfit"), ImGuiTableColumnFlags.WidthStretch, 2.2f);
        ImGui.TableSetupColumn(Loc.T("col_slot"), ImGuiTableColumnFlags.WidthStretch, 1.1f);
        ImGui.TableSetupColumn(Loc.T("col_item"), ImGuiTableColumnFlags.WidthStretch, 2.2f);
        ImGui.TableSetupColumn(Loc.T("col_replacement"), ImGuiTableColumnFlags.WidthStretch, 2.5f);
        ImGui.TableHeadersRow();

        foreach (var (entry, piece) in rows)
        {
            ImGui.PushID(entry.Folder + "|" + piece.Tag + "|" + piece.Slot.Name);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(Names.Display(entry.Name));

            ImGui.TableNextColumn();
            ImGui.TextColored(Grey, Loc.Slot(piece.Slot.Name));

            ImGui.TableNextColumn();
            Icon(piece.Replacement != null ? FontAwesomeIcon.ArrowRight : FontAwesomeIcon.ExclamationTriangle, piece.Replacement != null ? Yellow : Red);
            ImGui.SameLine();
            ImGui.TextUnformatted(piece.ItemName);
            Tooltip(Loc.T("conflict_with", string.Join(", ", piece.Conflicts.Select(c => Names.Display(c.ModName)).Distinct())));

            ImGui.TableNextColumn();
            DrawScanReplacementPicker(entry, piece);

            ImGui.PopID();
        }

        ImGui.EndTable();
    }

    private void DrawScanReplacementPicker(ScannedOutfit entry, OutfitPiece piece)
    {
        var preview = piece.Replacement != null
            ? $"{piece.Replacement.Name} ({Slots.Tag(piece.Slot.Kind, piece.Replacement.Set)})"
            : Loc.T("scan_keep");

        ImGui.SetNextItemWidth(-1);
        if (!ImGui.BeginCombo("##replacement", preview))
            return;

        if (ImGui.IsWindowAppearing())
        {
            scanFilter = string.Empty;
            ImGui.SetKeyboardFocusHere();
        }

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##scan_search", Loc.T("search_hint"), ref scanFilter, 64);

        if (!scanFreeItems.TryGetValue(piece, out var list))
        {
            list = plugin.Extractor.FreeItems(entry.Outfit!, piece.Slot, scanScanner!);
            scanFreeItems[piece] = list;
        }

        // Un objet déjà choisi pour une autre pièce n'est plus libre.
        var taken = scanResults!
            .Where(o => o.Outfit != null)
            .SelectMany(o => o.Outfit!.Pieces)
            .Where(p => p != piece && p.Replacement != null && p.Slot.Kind == piece.Slot.Kind)
            .Select(p => p.Replacement!.Set)
            .ToHashSet();

        ImGui.TextColored(Grey, Loc.T("free_count", list.Count));

        if (ImGui.Selectable(Loc.T("scan_keep") + "##keep", piece.Replacement == null))
            piece.Replacement = null;

        var filter = Names.Simplify(scanFilter);
        foreach (var item in list
                     .Where(i => !taken.Contains(i.Set))
                     .Where(i => filter.Length == 0 || Names.Simplify(i.Name).Contains(filter, StringComparison.Ordinal))
                     .Take(300))
        {
            var label = $"{item.Name}  —  {Slots.Tag(piece.Slot.Kind, item.Set)} v{item.Variant}##{item.Id}";
            if (ImGui.Selectable(label, piece.Replacement?.Id == item.Id))
                piece.Replacement = item;
        }

        ImGui.EndCombo();
    }

    /// <summary>
    /// Pour chaque tenue modifiée : déplace les pièces choisies, réécrit le mod, met à jour le design Glamourer,
    /// puis remplace l'ancien mod DressSaver dans Penumbra. Seuls des mods DressSaver sont supprimés.
    /// </summary>
    private void ApplyScanReplacements()
    {
        if (scanResults == null)
            return;

        var config = plugin.Configuration;
        var done = 0;
        var messages = new List<string>();

        foreach (var entry in scanResults.Where(o => o.Outfit != null && o.DesignId != null))
        {
            var outfit = entry.Outfit!;
            var moves = outfit.Pieces.Where(p => p.Conflicts.Count > 0 && p.Replacement != null).ToList();
            if (moves.Count == 0)
                continue;

            try
            {
                // Description et auteur d'origine (crédits compris) conservés.
                var (author, description) = ReadModInfo(entry.Folder);

                foreach (var piece in moves)
                    plugin.Remapper.Apply(outfit, piece, piece.Replacement!);

                var pmp = PmpWriter.Write(outfit, entry.Name, author, config.OutputFolder, description);

                // Design : le nouveau est créé avant de supprimer l'ancien, au même endroit dans Glamourer.
                var oldId = entry.DesignId!.Value;
                string designPath;
                try
                {
                    designPath = plugin.Extractor.DesignPath(oldId);
                }
                catch (Exception)
                {
                    designPath = designList.GetValueOrDefault(oldId, entry.Name); // Glamourer trop ancien pour donner le dossier
                }

                var newBase64 = outfit.Design.ToBase64();
                File.WriteAllText(Path.ChangeExtension(pmp, null) + "_Glamourer.txt", newBase64);
                var (result, newId) = plugin.Extractor.SaveDesign(newBase64, designPath);
                if (result == "Success" && newId != Guid.Empty)
                    plugin.Extractor.DeleteDesign(oldId);
                else
                    messages.Add(Loc.T("status_design_failed", result).Trim());

                var wasEnabled = plugin.Extractor.IsModEnabled(entry.Folder);
                plugin.Extractor.RemoveMod(entry.Folder);
                plugin.Extractor.Install(pmp);
                config.OutfitDesigns.Remove(entry.Folder);
                pendingRelinks.Add(new PendingRelink(entry.Name, newId, wasEnabled, DateTime.Now.AddSeconds(30)));
                done++;
            }
            catch (Exception e)
            {
                messages.Add(Loc.T("scan_failed", Names.Display(entry.Name), e.Message));
                Plugin.Log.Error(e, $"DressSaver: conflict fix failed for {entry.Name}");
            }
        }

        plugin.SaveConfiguration();
        scanStatus = string.Join(" ", new[] { Loc.T("scan_applied", done) }.Concat(messages));
        scanStatusIsError = messages.Count > 0;
        scanResults = null;
        scanFreeItems.Clear();
        wardrobe = null;
        nextRelinkPoll = DateTime.Now.AddMilliseconds(500);
    }

    private (string Author, string? Description) ReadModInfo(string folder)
    {
        var author = plugin.Configuration.Author;
        string? description = null;
        try
        {
            var meta = Path.Combine(plugin.Extractor.ModDirectory, folder, "meta.json");
            if (File.Exists(meta) && JsonNode.Parse(File.ReadAllText(meta)) is JsonObject obj)
            {
                author = obj["Author"]?.ToString() ?? author;
                description = obj["Description"]?.ToString();
            }
        }
        catch (Exception e)
        {
            Plugin.Log.Warning(e, $"DressSaver: could not read meta.json of {folder}");
        }

        return (author, description);
    }

    /// <summary>Tenues réinstallées : on les relie à leur nouveau design et on réactive celles qui l'étaient.</summary>
    private void TickRelinks()
    {
        if (pendingRelinks.Count == 0 || DateTime.Now < nextRelinkPoll)
            return;
        nextRelinkPoll = DateTime.Now.AddMilliseconds(500);

        var changed = false;
        foreach (var r in pendingRelinks.ToList())
        {
            try
            {
                var dir = plugin.Extractor.FindModDirectory(r.ModName);
                if (dir == null)
                {
                    if (DateTime.Now > r.Deadline)
                        pendingRelinks.Remove(r);
                    continue;
                }

                if (r.DesignId != Guid.Empty)
                {
                    plugin.Configuration.OutfitDesigns[dir] = r.DesignId;
                    changed = true;
                }

                if (r.Enable)
                {
                    plugin.Extractor.SetModEnabled(dir, true);
                    if (r.DesignId != Guid.Empty)
                        plugin.Extractor.ApplyDesign(r.DesignId);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e, $"DressSaver: relink failed for {r.ModName}");
            }

            pendingRelinks.Remove(r);
        }

        if (changed)
            plugin.SaveConfiguration();
        if (pendingRelinks.Count == 0)
            wardrobe = null; // la liste sera relue avec les tenues réinstallées
    }
}
