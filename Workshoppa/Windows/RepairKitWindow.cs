using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using LLib;
using LLib.GameUI;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Workshoppa.Windows;

internal sealed class RepairKitWindow : Window, IDisposable
{
    private const int DarkMatterCluster6ItemId = 10386;

    private readonly WorkshopPlugin _plugin;
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly IPluginLog _pluginLog;
    private readonly IGameGui _gameGui;
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly Configuration _configuration;

    private ItemForSale? _itemForSale;
    private PurchaseState? _purchaseState;

    public RepairKitWindow(WorkshopPlugin plugin, DalamudPluginInterface pluginInterface, IPluginLog pluginLog, IGameGui gameGui, IAddonLifecycle addonLifecycle, Configuration configuration)
        : base("Repair Kits###WorkshoppaRepairKitWindow")
    {
        _plugin = plugin;
        _pluginInterface = pluginInterface;
        _pluginLog = pluginLog;
        _gameGui = gameGui;
        _addonLifecycle = addonLifecycle;
        _configuration = configuration;

        Position = new Vector2(100, 100);
        PositionCondition = ImGuiCond.Always;
        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoCollapse;

        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "Shop", ShopPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PreFinalize, "Shop", ShopPreFinalize);
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "Shop", ShopPostUpdate);
    }

    public bool AutoBuyEnabled => _purchaseState != null;

    public bool IsAwaitingYesNo
    {
        get => _purchaseState?.IsAwaitingYesNo ?? false;
        set => _purchaseState!.IsAwaitingYesNo = value;
    }

    private unsafe void ShopPostSetup(AddonEvent type, AddonArgs args)
    {
        if (!_configuration.EnableRepairKitCalculator)
        {
            _itemForSale = null;
            IsOpen = false;
            return;
        }

        UpdateShopStock((AtkUnitBase*)args.Addon);
        if (_itemForSale != null)
            IsOpen = true;
    }

    private void ShopPreFinalize(AddonEvent type, AddonArgs args)
    {
        _purchaseState = null;
        _plugin.RestoreYesAlready();

        IsOpen = false;
    }

    private unsafe void ShopPostUpdate(AddonEvent type, AddonArgs args)
    {
        if (!_configuration.EnableRepairKitCalculator)
        {
            _itemForSale = null;
            IsOpen = false;
            return;
        }

        UpdateShopStock((AtkUnitBase*)args.Addon);
        if (_itemForSale != null)
        {
            AtkUnitBase* addon = (AtkUnitBase*)args.Addon;
            short x = 0, y = 0;
            addon->GetPosition(&x, &y);

            short width = 0, height = 0;
            addon->GetSize(&width, &height, true);
            x += width;

            if ((short)Position!.Value.X != x || (short)Position!.Value.Y != y)
                Position = new Vector2(x, y);

            IsOpen = true;
        }
        else
            IsOpen = false;
    }

    private unsafe void UpdateShopStock(AtkUnitBase* addon)
    {
        if (GetDarkMatterClusterCount() == 0)
        {
            _itemForSale = null;
            return;
        }

        if (addon->AtkValuesCount != 625)
        {
            _pluginLog.Error($"Unexpected amount of atkvalues for Shop addon ({addon->AtkValuesCount})");
            _itemForSale = null;
            return;
        }

        var atkValues = addon->AtkValues;

        // Check if on 'Current Stock' tab?
        if (atkValues[0].UInt != 0)
        {
            _itemForSale = null;
            return;
        }

        uint itemCount = atkValues[2].UInt;
        if (itemCount == 0)
        {
            _itemForSale = null;
            return;
        }

        _itemForSale = Enumerable.Range(0, (int)itemCount)
            .Select(i => new ItemForSale
            {
                Position = i,
                ItemName = atkValues[14 + i].ReadAtkString(),
                Price = atkValues[75 + i].UInt,
                OwnedItems = atkValues[136 + i].UInt,
                ItemId = atkValues[441 + i].UInt,
            })
            .FirstOrDefault(x => x.ItemId == DarkMatterCluster6ItemId);
        if (_itemForSale != null && _purchaseState != null)
        {
            int ownedItems = (int)_itemForSale.OwnedItems;
            if (_purchaseState.OwnedItems != ownedItems)
            {
                _purchaseState.OwnedItems = ownedItems;
                _purchaseState.NextStep = DateTime.Now.AddSeconds(0.25);
            }
        }
    }

    private int GetDarkMatterClusterCount() => GetItemCount(10335);

    private int GetGil() => GetItemCount(1);

    private unsafe int GetItemCount(uint itemId)
    {
        InventoryManager* inventoryManager = InventoryManager.Instance();
        return inventoryManager->GetInventoryItemCount(itemId, checkEquipped: false, checkArmory: false);
    }

    private int GetMaxItemsToPurchase()
    {
        if (_itemForSale == null)
            return 0;

        int gil = GetGil();
        return (int)(gil / _itemForSale!.Price);
    }

    public override void Draw()
    {
        int darkMatterClusters = GetDarkMatterClusterCount();
        if (_itemForSale == null || darkMatterClusters == 0)
        {
            IsOpen = false;
            return;
        }

        LImGui.AddPatreonIcon(_pluginInterface);

        ImGui.Text("Inventory");
        ImGui.Indent();
        ImGui.Text($"Dark Matter Clusters: {darkMatterClusters:N0}");
        ImGui.Text($"Grade 6 Dark Matter: {_itemForSale.OwnedItems:N0}");
        ImGui.Unindent();

        int missingItems = Math.Max(0, darkMatterClusters * 5 - (int)_itemForSale.OwnedItems);
        ImGui.TextColored(missingItems == 0 ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed, $"Missing Grade 6 Dark Matter: {missingItems:N0}");

        if (_purchaseState != null)
        {
            HandleNextPurchaseStep();

            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Times, "Cancel Auto-Buy"))
            {
                _purchaseState = null;
                _plugin.RestoreYesAlready();
            }
        }
        else
        {
            int toPurchase = Math.Min(GetMaxItemsToPurchase(), missingItems);
            if (toPurchase > 0)
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.DollarSign, $"Auto-Buy missing Dark Matter for {_itemForSale.Price * toPurchase:N0}{SeIconChar.Gil.ToIconString()}"))
                {
                    _purchaseState = new((int)_itemForSale.OwnedItems + toPurchase, (int)_itemForSale.OwnedItems);
                    _plugin.SaveYesAlready();

                    HandleNextPurchaseStep();
                }
            }
        }
    }

    private unsafe void HandleNextPurchaseStep()
    {
        if (_itemForSale == null || _purchaseState == null)
            return;

        if (!_plugin.HasFreeInventorySlot())
        {
            _pluginLog.Warning($"No free inventory slots, can't buy more {_itemForSale.ItemName}");
            _purchaseState = null;
            _plugin.RestoreYesAlready();
        }
        else if (!_purchaseState.IsComplete)
        {
            if (_purchaseState.NextStep <= DateTime.Now && _gameGui.TryGetAddonByName("Shop", out AtkUnitBase* addonShop))
            {
                int buyNow = Math.Min(_purchaseState.ItemsLeftToBuy, 99);
                _pluginLog.Information($"Buying {buyNow}x {_itemForSale.ItemName}");

                var buyItem = stackalloc AtkValue[]
                {
                    new() { Type = ValueType.Int, Int = 0 },
                    new() { Type = ValueType.Int, Int = _itemForSale.Position },
                    new() { Type = ValueType.Int, Int = buyNow },
                    new() { Type = 0, Int = 0 }
                };
                addonShop->FireCallback(4, buyItem);

                _purchaseState.NextStep = DateTime.MaxValue;
                _purchaseState.IsAwaitingYesNo = true;
            }
        }
        else
        {
            _pluginLog.Information($"Stopping item purchase (desired = {_purchaseState.DesiredItems}, owned = {_purchaseState.OwnedItems})");
            _purchaseState = null;
            _plugin.RestoreYesAlready();
        }
    }

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Shop", ShopPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, "Shop", ShopPreFinalize);
        _addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "PostUpdate", ShopPostUpdate);
    }

    private sealed class ItemForSale
    {
        public required int Position { get; init; }
        public required uint ItemId { get; init; }
        public required string? ItemName { get; init; }
        public required uint Price { get; init; }
        public required uint OwnedItems { get; init; }
    }

    private sealed class PurchaseState
    {
        public PurchaseState(int desiredItems, int ownedItems)
        {
            DesiredItems = desiredItems;
            OwnedItems = ownedItems;
        }

        public int DesiredItems { get; }
        public int OwnedItems { get; set; }
        public int ItemsLeftToBuy => Math.Max(0, DesiredItems - OwnedItems);
        public bool IsComplete => ItemsLeftToBuy == 0;
        public bool IsAwaitingYesNo { get; set; }
        public DateTime NextStep { get; set; } = DateTime.MinValue;
    }
}
