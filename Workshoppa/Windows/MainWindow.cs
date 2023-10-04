using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Workshoppa.GameData;

namespace Workshoppa.Windows;

internal sealed class MainWindow : Window
{
    private readonly WorkshopPlugin _plugin;
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly IClientState _clientState;
    private readonly Configuration _configuration;
    private readonly WorkshopCache _workshopCache;

    private string _searchString = string.Empty;

    public MainWindow(WorkshopPlugin plugin, DalamudPluginInterface pluginInterface, IClientState clientState, Configuration configuration, WorkshopCache workshopCache)
        : base("Workshoppa###WorkshoppaMainWindow")
    {
        _plugin = plugin;
        _pluginInterface = pluginInterface;
        _clientState = clientState;
        _configuration = configuration;
        _workshopCache = workshopCache;

        Position = new Vector2(100, 100);
        PositionCondition = ImGuiCond.FirstUseEver;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(350, 50),
            MaximumSize = new Vector2(500, 500),
        };

        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse;
    }

    public bool NearFabricationStation { get; set; }
    public ButtonState State { get; set; } = ButtonState.None;

    public bool IsDiscipleOfHand =>
        _clientState.LocalPlayer != null && _clientState.LocalPlayer.ClassJob.Id is >= 8 and <= 15;

    public override void Draw()
    {
        var currentItem = _configuration.CurrentlyCraftedItem;
        if (currentItem != null)
        {
            var currentCraft = _workshopCache.Crafts.Single(x => x.WorkshopItemId == currentItem.WorkshopItemId);
            ImGui.Text($"Currently Crafting: {currentCraft.Name}");

            if (_plugin.CurrentStage == Stage.Stopped)
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Search, "Check Inventory"))
                    ImGui.OpenPopup(nameof(CheckMaterial));

                ImGui.SameLine();
                ImGui.BeginDisabled(!NearFabricationStation);
                ImGui.BeginDisabled(!IsDiscipleOfHand);
                if (currentItem.StartedCrafting)
                {
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "Resume"))
                        State = ButtonState.Resume;
                }
                else
                {
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "Start Crafting"))
                        State = ButtonState.Start;
                }
                ImGui.EndDisabled();

                ImGui.SameLine();
                ImGui.BeginDisabled(!ImGui.GetIO().KeyCtrl);
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Times, "Cancel"))
                {
                    State = ButtonState.Pause;
                    _configuration.CurrentlyCraftedItem = null;

                    Save();
                }
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && !ImGui.GetIO().KeyCtrl)
                    ImGui.SetTooltip(
                        $"Hold CTRL to remove this as craft. You have to manually use the fabrication station to cancel or finish this craft before you can continue using the queue.");
                ImGui.EndDisabled();

                if (!IsDiscipleOfHand)
                    ImGui.TextColored(ImGuiColors.DalamudRed, "You need to be a Disciple of the Hand to start crafting.");
            }
            else
            {
                ImGui.BeginDisabled(_plugin.CurrentStage == Stage.RequestStop);
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Pause, "Pause"))
                    State = ButtonState.Pause;

                ImGui.EndDisabled();
            }
        }
        else
        {
            ImGui.Text("Currently Crafting: ---");

            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Search, "Check Inventory"))
                ImGui.OpenPopup(nameof(CheckMaterial));

            ImGui.SameLine();
            ImGui.BeginDisabled(!NearFabricationStation || _configuration.ItemQueue.Sum(x => x.Quantity) == 0 || _plugin.CurrentStage != Stage.Stopped || !IsDiscipleOfHand);
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "Start Crafting"))
                State = ButtonState.Start;
            ImGui.EndDisabled();

            if (!IsDiscipleOfHand)
                ImGui.TextColored(ImGuiColors.DalamudRed, "You need to be a Disciple of the Hand to start crafting.");
        }

        if (ImGui.BeginPopup(nameof(CheckMaterial)))
        {
            CheckMaterial();
            ImGui.EndPopup();
        }

        ImGui.Separator();
        ImGui.Text("Queue:");
        ImGui.BeginDisabled(_plugin.CurrentStage != Stage.Stopped);
        Configuration.QueuedItem? itemToRemove = null;
        for (int i = 0; i < _configuration.ItemQueue.Count; ++ i)
        {
            ImGui.PushID($"ItemQueue{i}");
            var item = _configuration.ItemQueue[i];
            var craft = _workshopCache.Crafts.Single(x => x.WorkshopItemId == item.WorkshopItemId);

            ImGui.SetNextItemWidth(100);
            int quantity = item.Quantity;
            if (ImGui.InputInt(craft.Name, ref quantity))
            {
                item.Quantity = Math.Max(0, quantity);
                Save();
            }

            ImGui.OpenPopupOnItemClick($"###Context{i}");
            if (ImGui.BeginPopupContextItem($"###Context{i}"))
            {
                if (ImGui.MenuItem($"Remove {craft.Name}"))
                    itemToRemove = item;

                ImGui.EndPopup();
            }

            ImGui.PopID();
        }

        if (itemToRemove != null)
        {
            _configuration.ItemQueue.Remove(itemToRemove);
            Save();
        }

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.BeginCombo("##CraftSelection", "Add Craft..."))
        {
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputTextWithHint("", "Filter...", ref _searchString, 256);

            foreach (var craft in _workshopCache.Crafts
                         .Where(x => x.Name.ToLower().Contains(_searchString.ToLower()))
                         .OrderBy(x => x.WorkshopItemId))
            {
                if (ImGui.Selectable($"{craft.Name}##SelectCraft{craft.WorkshopItemId}"))
                {
                    _configuration.ItemQueue.Add(new Configuration.QueuedItem
                    {
                        WorkshopItemId = craft.WorkshopItemId,
                        Quantity = 1,
                    });
                    Save();
                }
            }

            ImGui.EndCombo();
        }
        ImGui.EndDisabled();

        ImGui.Separator();
        ImGui.Text($"Debug (Stage): {_plugin.CurrentStage}");
    }

    private void Save()
    {
        _pluginInterface.SavePluginConfig(_configuration);
    }

    private unsafe void CheckMaterial()
    {
        ImGui.Text("Items needed for all crafts in queue:");

        List<uint> workshopItemIds = _configuration.ItemQueue
            .SelectMany(x => Enumerable.Range(0, x.Quantity).Select(_ => x.WorkshopItemId))
            .ToList();
        Dictionary<uint, int> completedForCurrentCraft = new();
        var currentItem = _configuration.CurrentlyCraftedItem;
        if (currentItem != null)
        {
            workshopItemIds.Add(currentItem.WorkshopItemId);

            var craft = _workshopCache.Crafts.Single(x =>
                x.WorkshopItemId == currentItem.WorkshopItemId);
            for (int i = 0; i < currentItem.PhasesComplete; ++i)
            {
                foreach (var item in craft.Phases[i].Items)
                    AddMaterial(completedForCurrentCraft, item.ItemId, item.TotalQuantity);
            }

            if (currentItem.PhasesComplete < craft.Phases.Count)
            {
                foreach (var item in currentItem.ContributedItemsInCurrentPhase)
                    AddMaterial(completedForCurrentCraft, item.ItemId, (int)item.QuantityComplete);
            }
        }

        var items = workshopItemIds.Select(x => _workshopCache.Crafts.Single(y => y.WorkshopItemId == x))
            .SelectMany(x => x.Phases)
            .SelectMany(x => x.Items)
            .GroupBy(x => new { x.Name, x.ItemId })
            .OrderBy(x => x.Key.Name)
            .Select(x => new
            {
                x.Key.ItemId,
                x.Key.Name,
                TotalQuantity = completedForCurrentCraft.TryGetValue(x.Key.ItemId, out var completed)
                    ? x.Sum(y => y.TotalQuantity) - completed
                    : x.Sum(y => y.TotalQuantity),
            });

        ImGui.Indent(20);
        InventoryManager* inventoryManager = InventoryManager.Instance();
        foreach (var item in items)
        {
            int inInventory = inventoryManager->GetInventoryItemCount(item.ItemId, true, false, false) +
                              inventoryManager->GetInventoryItemCount(item.ItemId, false, false, false);
            ImGui.TextColored(inInventory >= item.TotalQuantity ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed,
                $"{item.Name} ({inInventory} / {item.TotalQuantity})");
        }

        ImGui.Unindent(20);
    }

    private void AddMaterial(Dictionary<uint, int> completedForCurrentCraft, uint itemId, int quantity)
    {
        if (completedForCurrentCraft.TryGetValue(itemId, out var existingQuantity))
            completedForCurrentCraft[itemId] = quantity + existingQuantity;
        else
            completedForCurrentCraft[itemId] = quantity;
    }

    public enum ButtonState
    {
        None,
        Start,
        Resume,
        Pause,
        Stop,
    }
}
