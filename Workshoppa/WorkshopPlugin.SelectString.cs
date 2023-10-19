using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Workshoppa;

partial class WorkshopPlugin
{
    private unsafe void SelectStringPostSetup(AddonEvent type, AddonArgs args)
    {
        _pluginLog.Verbose("SelectString post-setup");

        string desiredText;
        Action followUp;
        if (CurrentStage == Stage.OpenCraftingLog)
        {
            desiredText = _gameStrings.ViewCraftingLog;
            followUp = OpenCraftingLogFollowUp;
        }
        else
            return;

        _pluginLog.Verbose($"Looking for '{desiredText}' in prompt");
        AddonSelectString* addonSelectString = (AddonSelectString*)args.Addon;
        int entries = addonSelectString->PopupMenu.PopupMenu.EntryCount;

        for (int i = 0; i < entries; ++i)
        {
            var textPointer = addonSelectString->PopupMenu.PopupMenu.EntryNames[i];
            if (textPointer == null)
                continue;

            var text = MemoryHelper.ReadSeStringNullTerminated((nint)textPointer).ToString();
            _pluginLog.Verbose($"  Choice {i} → {text}");
            if (text == desiredText)
            {
                _pluginLog.Information($"Selecting choice {i} ({text})");
                addonSelectString->AtkUnitBase.FireCallbackInt(i);

                followUp();
                return;
            }
        }

        _pluginLog.Verbose($"Text '{desiredText}' was not found in prompt.");
    }

    private void OpenCraftingLogFollowUp()
    {
        CurrentStage = Stage.SelectCraftCategory;
    }

    private void ConfirmCollectProductFollowUp()
    {
        _configuration.CurrentlyCraftedItem = null;
        _pluginInterface.SavePluginConfig(_configuration);

        CurrentStage = Stage.TakeItemFromQueue;
        _continueAt = DateTime.Now.AddSeconds(0.5);
    }
}
