using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Workshoppa;

partial class WorkshopPlugin
{
    private unsafe void SelectYesNoPostSetup(AddonEvent type, AddonArgs args)
    {
        _pluginLog.Verbose("SelectYesNo post-setup");

        AddonSelectYesno* addonSelectYesNo = (AddonSelectYesno*)args.Addon;
        string text = MemoryHelper.ReadSeString(&addonSelectYesNo->PromptText->NodeText).ToString().Replace("\n", "").Replace("\r", "");
        _pluginLog.Verbose($"YesNo prompt: '{text}'");

        if (_repairKitWindow.IsOpen)
        {
            _pluginLog.Verbose($"Checking for Repair Kit YesNo ({_repairKitWindow.AutoBuyEnabled}, {_repairKitWindow.IsAwaitingYesNo})");
            if (_repairKitWindow.AutoBuyEnabled && _repairKitWindow.IsAwaitingYesNo && _gameStrings.PurchaseItem.IsMatch(text))
            {
                _pluginLog.Information($"Selecting 'yes' ({text})");
                _repairKitWindow.IsAwaitingYesNo = false;
                addonSelectYesNo->AtkUnitBase.FireCallbackInt(0);
            }
            else
            {
                _pluginLog.Verbose("Not a purchase confirmation match");
            }
        }
        else if (_mainWindow.IsOpen)
        {
            // TODO
        }
    }
}
