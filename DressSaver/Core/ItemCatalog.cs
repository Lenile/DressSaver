using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace DressSaver.Core;

public sealed record GameItem(uint Id, string Name, int Set, int Variant, uint SlotCategory);

/// <summary>Catalogue des objets du jeu équipables, par emplacement et par modèle.</summary>
public sealed class ItemCatalog
{
    private readonly Dictionary<uint, List<GameItem>> bySlotCategory = new();
    private readonly Dictionary<uint, GameItem> byId = new();

    public ItemCatalog(IDataManager data)
    {
        var sheet = data.GetExcelSheet<Item>();
        foreach (var row in sheet)
        {
            var category = row.EquipSlotCategory.RowId;
            if (category is < 3 or > 12 || category == 6)
                continue;

            var model = row.ModelMain;
            var set = (int)(model & 0xFFFF);
            var variant = (int)((model >> 16) & 0xFF);
            if (set == 0)
                continue;

            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var item = new GameItem(row.RowId, name, set, variant, category);
            byId[item.Id] = item;
            if (!bySlotCategory.TryGetValue(category, out var list))
                bySlotCategory[category] = list = new List<GameItem>();
            list.Add(item);
        }

        foreach (var list in bySlotCategory.Values)
            list.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.CurrentCultureIgnoreCase));
    }

    public GameItem? Get(uint id) => byId.GetValueOrDefault(id);

    public IReadOnlyList<GameItem> ForSlot(SlotInfo slot)
        => bySlotCategory.TryGetValue(slot.EquipSlotCategory, out var list) ? list : new List<GameItem>();

    /// <summary>Tous les objets qui utilisent un modèle donné sur un emplacement.</summary>
    public IEnumerable<GameItem> UsingSet(SlotInfo slot, int set) => ForSlot(slot).Where(i => i.Set == set);
}
