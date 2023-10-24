using System;
using System.Text.RegularExpressions;
using Dalamud.Plugin.Services;
using LLib;
using Lumina.Excel;
using Lumina.Excel.CustomSheets;
using Lumina.Excel.GeneratedSheets;

namespace Workshoppa.GameData;

internal sealed class GameStrings
{
    public GameStrings(IDataManager dataManager, IPluginLog pluginLog)
    {
        PurchaseItemForGil = dataManager.GetRegex<Addon>(3406, addon => addon.Text, pluginLog)
                             ?? throw new Exception($"Unable to resolve {nameof(PurchaseItemForGil)}");
        PurchaseItemForCompanyCredits = dataManager.GetRegex<Addon>(3473, addon => addon.Text, pluginLog)
                                        ?? throw new Exception($"Unable to resolve {nameof(PurchaseItemForCompanyCredits)}");
        ViewCraftingLog =
            dataManager.GetString<WorkshopDialogue>("TEXT_CMNDEFCOMPANYMANUFACTORY_00150_MENU_CC_NOTE",
                pluginLog) ?? throw new Exception($"Unable to resolve {nameof(ViewCraftingLog)}");
        TurnInHighQualityItem = dataManager.GetString<Addon>(102434, addon => addon.Text, pluginLog)
                                ?? throw new Exception($"Unable to resolve {nameof(TurnInHighQualityItem)}");
        ContributeItems = dataManager.GetRegex<Addon>(6652, addon => addon.Text, pluginLog)
                          ?? throw new Exception($"Unable to resolve {nameof(ContributeItems)}");
        RetrieveFinishedItem =
            dataManager.GetRegex<WorkshopDialogue>("TEXT_CMNDEFCOMPANYMANUFACTORY_00150_FINISH_CONF", pluginLog)
            ?? throw new Exception($"Unable to resolve {nameof(RetrieveFinishedItem)}");
    }

    public Regex PurchaseItemForGil { get; }
    public Regex PurchaseItemForCompanyCredits { get; }
    public string ViewCraftingLog { get; }
    public string TurnInHighQualityItem { get; }
    public Regex ContributeItems { get; }
    public Regex RetrieveFinishedItem { get; }

    [Sheet("custom/001/CmnDefCompanyManufactory_00150")]
    private class WorkshopDialogue : QuestDialogueText
    {
    }
}
