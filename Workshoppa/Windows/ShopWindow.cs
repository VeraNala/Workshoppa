using System;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using LLib;
using LLib.GameUI;
using Workshoppa.External;
using Workshoppa.GameData.Shops;

namespace Workshoppa.Windows;

internal abstract class ShopWindow : LImGui.LWindow, IDisposable
{
    private readonly string _addonName;
    private readonly WorkshopPlugin _plugin;
    private readonly IPluginLog _pluginLog;
    private readonly IGameGui _gameGui;
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly ExternalPluginHandler _externalPluginHandler;

    protected ItemForSale? ItemForSale;
    protected PurchaseState? PurchaseState;

    protected ShopWindow(string name, string addonName, WorkshopPlugin plugin, IPluginLog pluginLog,
        IGameGui gameGui, IAddonLifecycle addonLifecycle, ExternalPluginHandler externalPluginHandler)
        : base(name)
    {
        _addonName = addonName;
        _plugin = plugin;
        _pluginLog = pluginLog;
        _gameGui = gameGui;
        _addonLifecycle = addonLifecycle;
        _externalPluginHandler = externalPluginHandler;

        Position = new Vector2(100, 100);
        PositionCondition = ImGuiCond.Always;
        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoCollapse;

        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, _addonName, ShopPostSetup);
        _addonLifecycle.RegisterListener(AddonEvent.PreFinalize, _addonName, ShopPreFinalize);
        _addonLifecycle.RegisterListener(AddonEvent.PostUpdate, _addonName, ShopPostUpdate);
    }

    public bool AutoBuyEnabled => PurchaseState != null;

    protected abstract bool Enabled { get; }

    public bool IsAwaitingYesNo
    {
        get => PurchaseState?.IsAwaitingYesNo ?? false;
        set => PurchaseState!.IsAwaitingYesNo = value;
    }

    private unsafe void ShopPostSetup(AddonEvent type, AddonArgs args)
    {
        if (!Enabled)
        {
            ItemForSale = null;
            IsOpen = false;
            return;
        }

        UpdateShopStock((AtkUnitBase*)args.Addon);
        PostUpdateShopStock();
        if (ItemForSale != null)
            IsOpen = true;
    }

    private void ShopPreFinalize(AddonEvent type, AddonArgs args)
    {
        PurchaseState = null;
        _externalPluginHandler.Restore();

        IsOpen = false;
    }

    private unsafe void ShopPostUpdate(AddonEvent type, AddonArgs args)
    {
        if (!Enabled)
        {
            ItemForSale = null;
            IsOpen = false;
            return;
        }

        UpdateShopStock((AtkUnitBase*)args.Addon);
        PostUpdateShopStock();
        if (ItemForSale != null)
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

    protected abstract unsafe void UpdateShopStock(AtkUnitBase* addon);

    private void PostUpdateShopStock()
    {
        if (ItemForSale != null && PurchaseState != null)
        {
            int ownedItems = (int)ItemForSale.OwnedItems;
            if (PurchaseState.OwnedItems != ownedItems)
            {
                PurchaseState.OwnedItems = ownedItems;
                PurchaseState.NextStep = DateTime.Now.AddSeconds(0.25);
            }
        }
    }

    protected unsafe int GetItemCount(uint itemId)
    {
        InventoryManager* inventoryManager = InventoryManager.Instance();
        return inventoryManager->GetInventoryItemCount(itemId, checkEquipped: false, checkArmory: false);
    }

    protected abstract int GetCurrencyCount();

    protected int GetMaxItemsToPurchase()
    {
        if (ItemForSale == null)
            return 0;

        int currency = GetCurrencyCount();
        return (int)(currency / ItemForSale!.Price);
    }

    protected void CancelAutoPurchase()
    {
        PurchaseState = null;
        _externalPluginHandler.Restore();
    }

    protected void StartAutoPurchase(int toPurchase)
    {
        PurchaseState = new((int)ItemForSale!.OwnedItems + toPurchase, (int)ItemForSale.OwnedItems);
        _externalPluginHandler.Save();
    }

    protected unsafe void HandleNextPurchaseStep()
    {
        if (ItemForSale == null || PurchaseState == null)
            return;

        int maxStackSize = _plugin.DetermineMaxStackSize(ItemForSale.ItemId);
        if (maxStackSize == 0 && !_plugin.HasFreeInventorySlot())
        {
            _pluginLog.Warning($"No free inventory slots, can't buy more {ItemForSale.ItemName}");
            PurchaseState = null;
            _externalPluginHandler.Restore();
        }
        else if (!PurchaseState.IsComplete)
        {
            if (PurchaseState.NextStep <= DateTime.Now &&
                _gameGui.TryGetAddonByName(_addonName, out AtkUnitBase* addonShop))
            {
                int buyNow = Math.Min(PurchaseState.ItemsLeftToBuy, maxStackSize);
                _pluginLog.Information($"Buying {buyNow}x {ItemForSale.ItemName}");

                FirePurchaseCallback(addonShop, buyNow);

                PurchaseState.NextStep = DateTime.MaxValue;
                PurchaseState.IsAwaitingYesNo = true;
            }
        }
        else
        {
            _pluginLog.Information(
                $"Stopping item purchase (desired = {PurchaseState.DesiredItems}, owned = {PurchaseState.OwnedItems})");
            PurchaseState = null;
            _externalPluginHandler.Restore();
        }
    }

    protected abstract unsafe void FirePurchaseCallback(AtkUnitBase* addonShop, int buyNow);

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, _addonName, ShopPostSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, _addonName, ShopPreFinalize);
        _addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, _addonName, ShopPostUpdate);
    }
}
