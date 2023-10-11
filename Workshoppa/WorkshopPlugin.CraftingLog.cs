﻿using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Component.GUI;
using LLib.GameUI;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Workshoppa;

partial class WorkshopPlugin
{
    private bool InteractWithFabricationStation(GameObject fabricationStation)
    {
        InteractWithTarget(fabricationStation);
        return true;
    }

    private void TakeItemFromQueue()
    {
        if (_configuration.CurrentlyCraftedItem == null)
        {
            while (_configuration.ItemQueue.Count > 0 && _configuration.CurrentlyCraftedItem == null)
            {
                var firstItem = _configuration.ItemQueue[0];
                if (firstItem.Quantity > 0)
                {
                    _configuration.CurrentlyCraftedItem = new Configuration.CurrentItem
                    {
                        WorkshopItemId = firstItem.WorkshopItemId,
                    };

                    if (firstItem.Quantity > 1)
                        firstItem.Quantity--;
                    else
                        _configuration.ItemQueue.Remove(firstItem);
                }
                else
                    _configuration.ItemQueue.Remove(firstItem);
            }

            _pluginInterface.SavePluginConfig(_configuration);
            if (_configuration.CurrentlyCraftedItem != null)
                CurrentStage = Stage.TargetFabricationStation;
            else
                CurrentStage = Stage.RequestStop;
        }
        else
            CurrentStage = Stage.TargetFabricationStation;
    }

    private void OpenCraftingLog()
    {
        if (SelectSelectString("craftlog", 0, s => s == "View company crafting log."))
            CurrentStage = Stage.SelectCraftCategory;
    }

    private unsafe void SelectCraftCategory()
    {
        AtkUnitBase* addonCraftingLog = GetCompanyCraftingLogAddon();
        if (addonCraftingLog == null)
            return;

        var craft = GetCurrentCraft();
        _pluginLog.Information($"Selecting category {craft.Category} and type {craft.Type}");
        var selectCategory = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 2 },
            new() { Type = 0, Int = 0 },
            new() { Type = ValueType.UInt, UInt = (uint)craft.Category },
            new() { Type = ValueType.UInt, UInt = craft.Type },
            new() { Type = ValueType.UInt, Int = 0 },
            new() { Type = ValueType.UInt, Int = 0 },
            new() { Type = ValueType.UInt, Int = 0 },
            new() { Type = 0, Int = 0 }
        };
        addonCraftingLog->FireCallback(8, selectCategory);
        CurrentStage = Stage.SelectCraft;
        _continueAt = DateTime.Now.AddSeconds(0.1);
    }

    private unsafe void SelectCraft()
    {
        AtkUnitBase* addonCraftingLog = GetCompanyCraftingLogAddon();
        if (addonCraftingLog == null)
            return;

        var craft = GetCurrentCraft();
        var atkValues = addonCraftingLog->AtkValues;

        uint shownItemCount = atkValues[13].UInt;
        var visibleItems = Enumerable.Range(0, (int)shownItemCount)
            .Select(i => new
            {
                WorkshopItemId = atkValues[14 + 4 * i].UInt,
                Name = atkValues[17 + 4 * i].ReadAtkString(),
            })
            .ToList();

        if (visibleItems.All(x => x.WorkshopItemId != craft.WorkshopItemId))
        {
            _pluginLog.Error($"Could not find {craft.Name} in current list, is it unlocked?");
            CurrentStage = Stage.RequestStop;
            return;
        }

        _pluginLog.Information($"Selecting craft {craft.WorkshopItemId}");
        var selectCraft = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 1 },
            new() { Type = 0, Int = 0 },
            new() { Type = 0, Int = 0 },
            new() { Type = 0, Int = 0 },
            new() { Type = ValueType.UInt, UInt = craft.WorkshopItemId },
            new() { Type = 0, Int = 0 },
            new() { Type = 0, Int = 0 },
            new() { Type = 0, Int = 0 }
        };
        addonCraftingLog->FireCallback(8, selectCraft);
        CurrentStage = Stage.ConfirmCraft;
        _continueAt = DateTime.Now.AddSeconds(0.1);
    }

    private void ConfirmCraft()
    {
        if (SelectSelectYesno(0, s => s.StartsWith("Craft ")))
        {
            _configuration.CurrentlyCraftedItem!.StartedCrafting = true;
            _pluginInterface.SavePluginConfig(_configuration);

            CurrentStage = Stage.TargetFabricationStation;
        }
    }
}
