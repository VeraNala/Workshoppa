using System;
using System.Text.RegularExpressions;
using Dalamud.Plugin.Services;
using LLib;
using Lumina.Excel.GeneratedSheets;

namespace Workshoppa.GameData;

internal sealed class GameStrings
{
    public GameStrings(IDataManager dataManager, IPluginLog pluginLog)
    {
        PurchaseItem = dataManager.GetRegex<Addon>(3406, addon => addon.Text, pluginLog)
                        ?? throw new Exception($"Unable to resolve {nameof(PurchaseItem)}");
    }

    public Regex PurchaseItem { get; }
}
