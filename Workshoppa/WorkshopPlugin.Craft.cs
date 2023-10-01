using System;
using System.Linq;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Workshoppa.GameData;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Workshoppa;

partial class WorkshopPlugin
{
    private uint? _contributingItemId;

    private void SelectCraftBranch()
    {
        if (SelectSelectString("contrib", 0, s => s.StartsWith("Contribute materials.")))
        {
            CurrentStage = Stage.ContributeMaterials;
            _continueAt = DateTime.Now.AddSeconds(1);
        }
        else if (SelectSelectString("advance", 0, s => s.StartsWith("Advance to the next phase of production.")))
        {
            PluginLog.Information("Phase is complete");
            CurrentStage = Stage.TargetFabricationStation;
            _continueAt = DateTime.Now.AddSeconds(3);
        }
        else if (SelectSelectString("complete", 0, s => s.StartsWith("Complete the construction of")))
        {
            PluginLog.Information("Item is almost complete, confirming last cutscene");
            CurrentStage = Stage.TargetFabricationStation;
            _continueAt = DateTime.Now.AddSeconds(3);
        }
        else if (SelectSelectString("collect", 0, s => s == "Collect finished product."))
        {
            PluginLog.Information("Item is complete");
            CurrentStage = Stage.ConfirmCollectProduct;
            _continueAt = DateTime.Now.AddSeconds(0.25);
        }
    }

    private unsafe void ContributeMaterials()
    {
        AtkUnitBase* addonMaterialDelivery = GetMaterialDeliveryAddon();
        if (addonMaterialDelivery == null)
            return;

        CraftState? craftState = ReadCraftState(addonMaterialDelivery);
        if (craftState == null || craftState.ResultItem == 0)
        {
            PluginLog.Warning("Could not parse craft state");
            _continueAt = DateTime.Now.AddSeconds(1);
            return;
        }

        for (int i = 0; i < craftState.Items.Count; ++i)
        {
            var item = craftState.Items[i];
            if (item.Finished)
                continue;

            if (!HasItemInSingleSlot(item.ItemId, item.ItemCountPerStep))
            {
                PluginLog.Error($"Can't contribute item {item.ItemId} to craft, couldn't find {item.ItemCountPerStep}x in a single inventory slot");
                CurrentStage = Stage.RequestStop;
                break;
            }

            PluginLog.Information($"Contributing {item.ItemCountPerStep}x {item.ItemName}");
            _contributingItemId = item.ItemId;
            var contributeMaterial = stackalloc AtkValue[]
            {
                new() { Type = ValueType.Int, Int = 0 },
                new() { Type = ValueType.UInt, Int = i },
                new() { Type = ValueType.UInt, UInt = item.ItemCountPerStep },
                new() { Type = 0, Int = 0 }
            };
            addonMaterialDelivery->FireCallback(4, contributeMaterial);
            CurrentStage = Stage.ConfirmMaterialDelivery;
            _continueAt = DateTime.Now.AddSeconds(0.5);
            break;
        }
    }

    private unsafe void ConfirmMaterialDelivery()
    {
        AtkUnitBase* addonMaterialDelivery = GetMaterialDeliveryAddon();
        if (addonMaterialDelivery == null)
            return;

        CraftState? craftState = ReadCraftState(addonMaterialDelivery);
        if (craftState == null || craftState.ResultItem == 0)
        {
            PluginLog.Warning("Could not parse craft state");
            _continueAt = DateTime.Now.AddSeconds(1);
            return;
        }

        if (SelectSelectYesno(0, s => s.StartsWith("Contribute") && s.EndsWith("to the company project?")))
        {
            var item = craftState.Items.Single(x => x.ItemId == _contributingItemId);
            item.StepsComplete++;
            if (craftState.IsPhaseComplete())
            {
                CurrentStage = Stage.TargetFabricationStation;
                _continueAt = DateTime.Now.AddSeconds(0.5);
            }
            else
            {
                CurrentStage = Stage.ContributeMaterials;
                _continueAt = DateTime.Now.AddSeconds(1);
            }
        }
        else if (DateTime.Now > _continueAt.AddSeconds(20))
        {
            PluginLog.Warning("No confirmation dialog, falling back to previous stage");
            CurrentStage = Stage.ContributeMaterials;
        }
    }

    private void ConfirmCollectProduct()
    {
        if (SelectSelectYesno(0, s => s.StartsWith("Retrieve")))
        {
            _configuration.CurrentlyCraftedItem = null;
            _pluginInterface.SavePluginConfig(_configuration);

            CurrentStage = Stage.TakeItemFromQueue;
            _continueAt = DateTime.Now.AddSeconds(0.5);
        }
    }
}
