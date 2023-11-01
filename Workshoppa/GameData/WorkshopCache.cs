using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;

namespace Workshoppa.GameData;

internal sealed class WorkshopCache
{
    public WorkshopCache(IDataManager dataManager, IPluginLog pluginLog)
    {
        Task.Run(() =>
        {
            try
            {
                Dictionary<ushort, Item> itemMapping = dataManager.GetExcelSheet<CompanyCraftSupplyItem>()!
                    .Where(x => x.RowId > 0)
                    .ToDictionary(x => (ushort)x.RowId, x => x.Item.Value!);

                Crafts = dataManager.GetExcelSheet<CompanyCraftSequence>()!
                    .Where(x => x.RowId > 0)
                    .Select(x => new WorkshopCraft
                    {
                        WorkshopItemId = x.RowId,
                        ResultItem = x.ResultItem.Row,
                        Name = x.ResultItem.Value!.Name.ToString(),
                        IconId = x.ResultItem.Value!.Icon,
                        Category = (WorkshopCraftCategory)x.CompanyCraftDraftCategory.Row,
                        Type = x.CompanyCraftType.Row,
                        Phases = x.CompanyCraftPart.Where(part => part.Row != 0)
                            .SelectMany(part =>
                                part.Value!.CompanyCraftProcess
                                    .Where(y => y.Value!.UnkData0.Any(z => z.SupplyItem > 0))
                                    .Select(y => (Type: part.Value!.CompanyCraftType.Value, Process: y)))
                            .Select(y => new WorkshopCraftPhase
                            {
                                Name = y.Type!.Name.ToString(),
                                Items = y.Process.Value!.UnkData0
                                    .Where(item => item.SupplyItem > 0)
                                    .Select(item => new WorkshopCraftItem
                                    {
                                        ItemId = itemMapping[item.SupplyItem].RowId,
                                        Name = itemMapping[item.SupplyItem].Name.ToString(),
                                        IconId = itemMapping[item.SupplyItem].Icon,
                                        SetQuantity = item.SetQuantity,
                                        SetsRequired = item.SetsRequired,
                                    })
                                    .ToList()
                                    .AsReadOnly(),
                            })
                            .ToList()
                            .AsReadOnly(),
                    })
                    .ToList()
                    .AsReadOnly();
            }
            catch (Exception e)
            {
                pluginLog.Error(e, "Unable to load cached items");
            }
        });
    }

    public IReadOnlyList<WorkshopCraft> Crafts { get; private set; } = new List<WorkshopCraft>();
}
