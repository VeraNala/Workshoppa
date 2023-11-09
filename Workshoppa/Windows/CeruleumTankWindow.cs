using System;
using System.Linq;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using LLib;
using LLib.GameUI;
using Workshoppa.External;
using Workshoppa.GameData.Shops;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Workshoppa.Windows;

internal sealed class CeruleumTankWindow : ShopWindow
{
    private const int CeruleumTankItemId = 10155;

    private readonly WorkshopPlugin _plugin;
    private readonly IPluginLog _pluginLog;
    private readonly Configuration _configuration;

    private int _companyCredits;
    private int _buyStackCount;
    private bool _buyPartialStacks = true;

    public CeruleumTankWindow(WorkshopPlugin plugin, IPluginLog pluginLog,
        IGameGui gameGui, IAddonLifecycle addonLifecycle, Configuration configuration,
        ExternalPluginHandler externalPluginHandler)
        : base("Ceruleum Tanks###WorkshoppaCeruleumTankWindow", "FreeCompanyCreditShop", plugin, pluginLog, gameGui, addonLifecycle, externalPluginHandler)
    {
        _plugin = plugin;
        _pluginLog = pluginLog;
        _configuration = configuration;
    }

    protected override bool Enabled => _configuration.EnableCeruleumTankCalculator;

    protected override unsafe void UpdateShopStock(AtkUnitBase* addon)
    {
        if (addon->AtkValuesCount != 170)
        {
            _pluginLog.Error($"Unexpected amount of atkvalues for FreeCompanyCreditShop addon ({addon->AtkValuesCount})");
            _companyCredits = 0;
            ItemForSale = null;
            return;
        }

        var atkValues = addon->AtkValues;
        _companyCredits = (int)atkValues[3].UInt;

        uint itemCount = atkValues[9].UInt;
        if (itemCount == 0)
        {
            ItemForSale = null;
            return;
        }
        ItemForSale = Enumerable.Range(0, (int)itemCount)
            .Select(i => new ItemForSale
            {
                Position = i,
                ItemName = atkValues[10 + i].ReadAtkString(),
                Price = atkValues[130 + i].UInt,
                OwnedItems = atkValues[90 + i].UInt,
                ItemId = atkValues[30 + i].UInt,
            })
            .FirstOrDefault(x => x.ItemId == CeruleumTankItemId);
    }

    protected override int GetCurrencyCount() => _companyCredits;

    public override void Draw()
    {
        if (ItemForSale == null)
        {
            IsOpen = false;
            return;
        }

        int ceruleumTanks = GetItemCount(CeruleumTankItemId);
        int freeInventorySlots = _plugin.GetFreeInventorySlots();

        ImGui.Text("Inventory");
        ImGui.Indent();
        ImGui.Text($"Ceruleum Tanks: {FormatStackCount(ceruleumTanks)}");
        ImGui.Text($"Free Slots: {freeInventorySlots}");
        ImGui.Unindent();

        ImGui.Separator();

        if (PurchaseState == null)
        {
            ImGui.SetNextItemWidth(100);
            ImGui.InputInt("Stacks to Buy", ref _buyStackCount);
            _buyStackCount = Math.Min(freeInventorySlots, Math.Max(0, _buyStackCount));

            if (ceruleumTanks % 999 > 0)
                ImGui.Checkbox($"Fill Partial Stacks (+{999 - ceruleumTanks % 999})", ref _buyPartialStacks);
        }

        int missingItems = _buyStackCount * 999;
        if (_buyPartialStacks && ceruleumTanks % 999 > 0)
            missingItems += (999 - ceruleumTanks % 999);

        if (PurchaseState != null)
        {
            HandleNextPurchaseStep();

            ImGui.Text($"Buying {FormatStackCount(PurchaseState.ItemsLeftToBuy)}...");
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Times, "Cancel Auto-Buy"))
                CancelAutoPurchase();
        }
        else
        {
            int toPurchase = Math.Min(GetMaxItemsToPurchase(), missingItems);
            if (toPurchase > 0)
            {
                ImGui.Spacing();
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.DollarSign,
                        $"Auto-Buy {FormatStackCount(toPurchase)} for {ItemForSale.Price * toPurchase:N0} CC"))
                {
                    StartAutoPurchase(toPurchase);
                    HandleNextPurchaseStep();
                }
            }
        }
    }

    private string FormatStackCount(int ceruleumTanks)
    {
        int fullStacks = ceruleumTanks / 999;
        int partials = ceruleumTanks % 999;
        string stacks = fullStacks == 1 ? "stack" : "stacks";
        if (partials > 0)
            return $"{fullStacks:N0} {stacks} + {partials}";
        return $"{fullStacks:N0} {stacks}";
    }

    protected override unsafe void FirePurchaseCallback(AtkUnitBase* addonShop, int buyNow)
    {
        var buyItem = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 0 },
            new() { Type = ValueType.UInt, UInt = (uint)ItemForSale!.Position },
            new() { Type = ValueType.UInt, UInt = (uint)buyNow },
        };
        addonShop->FireCallback(3, buyItem);
    }
}
