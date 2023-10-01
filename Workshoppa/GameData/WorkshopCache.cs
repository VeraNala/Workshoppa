using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Data;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;

namespace Workshoppa.GameData;

internal sealed class WorkshopCache
{
    public WorkshopCache(DataManager dataManager)
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
                PluginLog.Error(e, "Unable to load cached items");
            }
        });
    }

    /*
    /waitaddon "CompanyCraftRecipeNoteBook" <maxwait.30>
    /pcall CompanyCraftRecipeNoteBook false 2 0 1u 16u 548u 1505u 715u 0
    /wait 0.3
    /pcall CompanyCraftRecipeNoteBook false 1 0 0 0 548u 0 0 0
     */

    public IReadOnlyList<WorkshopCraft> Crafts { get; private set; } = new List<WorkshopCraft>();
}
