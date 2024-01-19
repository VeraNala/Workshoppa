using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Internal;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using LLib;
using Workshoppa.Data;
using Workshoppa.GameData;

namespace Workshoppa.Windows;

// FIXME The close button doesn't work near the workshop, either hide it or make it work
internal sealed class MainWindow : LImGui.LWindow
{
    private const string JsonPrefix = "workshoppa:v1:";

    private readonly WorkshopPlugin _plugin;
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly IClientState _clientState;
    private readonly Configuration _configuration;
    private readonly WorkshopCache _workshopCache;
    private readonly IconCache _iconCache;
    private readonly IChatGui _chatGui;
    private readonly IPluginLog _pluginLog;

    private string _searchString = string.Empty;
    private bool _checkInventory;

    public MainWindow(WorkshopPlugin plugin, DalamudPluginInterface pluginInterface, IClientState clientState,
        Configuration configuration, WorkshopCache workshopCache, IconCache iconCache, IChatGui chatGui,
        IPluginLog pluginLog)
        : base("Workshoppa###WorkshoppaMainWindow")
    {
        _plugin = plugin;
        _pluginInterface = pluginInterface;
        _clientState = clientState;
        _configuration = configuration;
        _workshopCache = workshopCache;
        _iconCache = iconCache;
        _chatGui = chatGui;
        _pluginLog = pluginLog;

        Position = new Vector2(100, 100);
        PositionCondition = ImGuiCond.FirstUseEver;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(350, 50),
            MaximumSize = new Vector2(500, 9999),
        };

        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse;
        AllowClickthrough = false;
    }

    public EOpenReason OpenReason { get; set; } = EOpenReason.None;
    public bool NearFabricationStation { get; set; }
    public ButtonState State { get; set; } = ButtonState.None;

    private bool IsDiscipleOfHand =>
        _clientState.LocalPlayer != null && _clientState.LocalPlayer.ClassJob.Id is >= 8 and <= 15;

    public override void Draw()
    {
        var currentItem = _configuration.CurrentlyCraftedItem;
        if (currentItem != null)
        {
            var currentCraft = _workshopCache.Crafts.Single(x => x.WorkshopItemId == currentItem.WorkshopItemId);
            ImGui.Text($"Currently Crafting:");

            IDalamudTextureWrap? icon = _iconCache.GetIcon(currentCraft.IconId);
            if (icon != null)
            {
                ImGui.Image(icon.ImGuiHandle, new Vector2(23, 23));
                ImGui.SameLine(0, 3);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 3);
            }

            ImGui.TextUnformatted($"{currentCraft.Name}");
            ImGui.Spacing();

            if (_plugin.CurrentStage == Stage.Stopped)
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Search, "Check Inventory"))
                    _checkInventory = !_checkInventory;

                ImGui.SameLine();
                ImGui.BeginDisabled(!NearFabricationStation);
                ImGui.BeginDisabled(!IsDiscipleOfHand);
                if (currentItem.StartedCrafting)
                {
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "Resume"))
                    {
                        State = ButtonState.Resume;
                        _checkInventory = false;
                    }
                }
                else
                {
                    if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "Start Crafting"))
                    {
                        State = ButtonState.Start;
                        _checkInventory = false;
                    }
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

                ShowErrorConditions();
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
                _checkInventory = !_checkInventory;

            ImGui.SameLine();
            ImGui.BeginDisabled(!NearFabricationStation || _configuration.ItemQueue.Sum(x => x.Quantity) == 0 ||
                                _plugin.CurrentStage != Stage.Stopped || !IsDiscipleOfHand);
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "Start Crafting"))
            {
                State = ButtonState.Start;
                _checkInventory = false;
            }

            ImGui.EndDisabled();

            ShowErrorConditions();
        }

        if (_checkInventory)
        {
            ImGui.Separator();
            CheckMaterial();
        }

        ImGui.Separator();
        ImGui.Text("Queue:");
        ImGui.BeginDisabled(_plugin.CurrentStage != Stage.Stopped);
        Configuration.QueuedItem? itemToRemove = null;
        for (int i = 0; i < _configuration.ItemQueue.Count; ++i)
        {
            ImGui.PushID($"ItemQueue{i}");
            var item = _configuration.ItemQueue[i];
            var craft = _workshopCache.Crafts.Single(x => x.WorkshopItemId == item.WorkshopItemId);

            IDalamudTextureWrap? icon = _iconCache.GetIcon(craft.IconId);
            if (icon != null)
            {
                ImGui.Image(icon.ImGuiHandle, new Vector2(23, 23));
                ImGui.SameLine(0, 3);
            }

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
        if (ImGui.BeginCombo("##CraftSelection", "Add Craft...", ImGuiComboFlags.HeightLarge))
        {
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputTextWithHint("", "Filter...", ref _searchString, 256);

            foreach (var craft in _workshopCache.Crafts
                         .Where(x => x.Name.ToLower().Contains(_searchString.ToLower()))
                         .OrderBy(x => x.WorkshopItemId))
            {
                IDalamudTextureWrap? icon = _iconCache.GetIcon(craft.IconId);
                if (icon != null)
                {
                    ImGui.Image(icon.ImGuiHandle, new Vector2(23, 23));
                    ImGui.SameLine();
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 3);
                }

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

        DrawImportExport();

        ImGui.EndDisabled();

        ImGui.Separator();
        ImGui.Text($"Debug (Stage): {_plugin.CurrentStage}");
    }

    private void DrawImportExport()
    {
        ImGui.BeginDisabled(_configuration.ItemQueue.Count == 0);
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Upload, "Export to Clipboard"))
        {
            var toClipboardItems = _configuration.ItemQueue.Select(x => new ClipboardItem
                {
                    WorkshopItemId = x.WorkshopItemId,
                    Quantity = x.Quantity
                })
                .ToList();

            var clipboardText =
                Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonPrefix + JsonSerializer.Serialize(toClipboardItems)));
            ImGui.SetClipboardText(clipboardText);

            _chatGui.Print("Copied queue content to clipboard.");
        }

        ImGui.EndDisabled();

        ImGui.SameLine();
        List<ClipboardItem> fromClipboardItems = new();
        try
        {
            string? clipboardText = GetClipboardText();
            if (!string.IsNullOrWhiteSpace(clipboardText))
            {
                clipboardText = Encoding.UTF8.GetString(Convert.FromBase64String(clipboardText));
                if (clipboardText.StartsWith(JsonPrefix))
                {
                    clipboardText = clipboardText.Substring(JsonPrefix.Length);
                    fromClipboardItems = JsonSerializer.Deserialize<List<ClipboardItem>>(clipboardText) ?? new();
                }
            }
        }
        catch (Exception)
        {
            //_pluginLog.Warning(e, "Unable to extract clipboard text");
        }

        ImGui.BeginDisabled(fromClipboardItems.Count == 0);
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Download, "Import from Clipboard"))
        {
            _pluginLog.Information($"Importing {fromClipboardItems.Count} items");
            foreach (var item in fromClipboardItems)
            {
                var craft = _workshopCache.Crafts.FirstOrDefault(x => x.WorkshopItemId == item.WorkshopItemId);
                if (craft != null)
                {
                    var queuedItem =
                        _configuration.ItemQueue.FirstOrDefault(x => x.WorkshopItemId == item.WorkshopItemId);
                    if (queuedItem != null)
                        queuedItem.Quantity += item.Quantity;
                    else
                    {
                        _configuration.ItemQueue.Add(new Configuration.QueuedItem
                        {
                            WorkshopItemId = item.WorkshopItemId,
                            Quantity = item.Quantity,
                        });
                    }
                }
            }

            Save();
        }

        ImGui.EndDisabled();
    }

    /// <summary>
    /// The default implementation for <see cref="ImGui.GetClipboardText"/> throws an NullReferenceException if the clipboard is empty, maybe also if it doesn't contain text.
    /// </summary>
    private unsafe string? GetClipboardText()
    {
        byte* ptr = ImGuiNative.igGetClipboardText();
        if (ptr == null)
            return null;

        int byteCount = 0;
        while (ptr[byteCount] != 0)
            ++byteCount;
        return Encoding.UTF8.GetString(ptr, byteCount);
    }

    private void Save()
    {
        _pluginInterface.SavePluginConfig(_configuration);
    }

    public void Toggle(EOpenReason reason)
    {
        if (!IsOpen)
        {
            IsOpen = true;
            OpenReason = reason;
        }
        else
            IsOpen = false;
    }

    public override void OnClose()
    {
        OpenReason = EOpenReason.None;
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
            .GroupBy(x => new { x.Name, x.ItemId, x.IconId })
            .OrderBy(x => x.Key.Name)
            .Select(x => new
            {
                x.Key.ItemId,
                x.Key.IconId,
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

            IDalamudTextureWrap? icon = _iconCache.GetIcon(item.IconId);
            if (icon != null)
            {
                ImGui.Image(icon.ImGuiHandle, new Vector2(23, 23));
                ImGui.SameLine(0, 3);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 3);
            }

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

    private void ShowErrorConditions()
    {
        if (!_plugin.WorkshopTerritories.Contains(_clientState.TerritoryType))
            ImGui.TextColored(ImGuiColors.DalamudRed, "You are not in the Company Workshop.");
        else if (!NearFabricationStation)
            ImGui.TextColored(ImGuiColors.DalamudRed, "You are not near a Farbrication Station.");

        if (!IsDiscipleOfHand)
            ImGui.TextColored(ImGuiColors.DalamudRed, "You need to be a Disciple of the Hand to start crafting.");
    }

    public enum ButtonState
    {
        None,
        Start,
        Resume,
        Pause,
        Stop,
    }

    public enum EOpenReason
    {
        None,
        Command,
        NearFabricationStation,
        PluginInstaller,
    }
}
