using System;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using LLib.GameUI;
using Workshoppa.External;
using Workshoppa.GameData.Shops;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Workshoppa.Windows;

internal sealed class RepairKitWindow : ShopWindow
{
    private const int DarkMatterCluster6ItemId = 10386;

    private readonly IPluginLog _pluginLog;
    private readonly Configuration _configuration;

    public RepairKitWindow(WorkshopPlugin plugin, IPluginLog pluginLog,
        IGameGui gameGui, IAddonLifecycle addonLifecycle, Configuration configuration,
        ExternalPluginHandler externalPluginHandler)
        : base("Repair Kits###WorkshoppaRepairKitWindow", "Shop", plugin, pluginLog, gameGui, addonLifecycle, externalPluginHandler)
    {
        _pluginLog = pluginLog;
        _configuration = configuration;
    }

    protected override bool Enabled => _configuration.EnableRepairKitCalculator;

    protected override unsafe void UpdateShopStock(AtkUnitBase* addon)
    {
        if (GetDarkMatterClusterCount() == 0)
        {
            ItemForSale = null;
            return;
        }

        if (addon->AtkValuesCount != 625)
        {
            _pluginLog.Error($"Unexpected amount of atkvalues for Shop addon ({addon->AtkValuesCount})");
            ItemForSale = null;
            return;
        }

        var atkValues = addon->AtkValues;

        // Check if on 'Current Stock' tab?
        if (atkValues[0].UInt != 0)
        {
            ItemForSale = null;
            return;
        }

        uint itemCount = atkValues[2].UInt;
        if (itemCount == 0)
        {
            ItemForSale = null;
            return;
        }

        ItemForSale = Enumerable.Range(0, (int)itemCount)
            .Select(i => new ItemForSale
            {
                Position = i,
                ItemName = atkValues[14 + i].ReadAtkString(),
                Price = atkValues[75 + i].UInt,
                OwnedItems = atkValues[136 + i].UInt,
                ItemId = atkValues[441 + i].UInt,
            })
            .FirstOrDefault(x => x.ItemId == DarkMatterCluster6ItemId);
    }

    private int GetDarkMatterClusterCount() => GetItemCount(10335);

    protected override int GetCurrencyCount() => GetItemCount(1);

    public override void Draw()
    {
        int darkMatterClusters = GetDarkMatterClusterCount();
        if (ItemForSale == null || darkMatterClusters == 0)
        {
            IsOpen = false;
            return;
        }

        ImGui.Text("Inventory");
        ImGui.Indent();
        ImGui.Text($"Dark Matter Clusters: {darkMatterClusters:N0}");
        ImGui.Text($"Grade 6 Dark Matter: {ItemForSale.OwnedItems:N0}");
        ImGui.Unindent();

        int missingItems = Math.Max(0, darkMatterClusters * 5 - (int)ItemForSale.OwnedItems);
        ImGui.TextColored(missingItems == 0 ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed,
            $"Missing Grade 6 Dark Matter: {missingItems:N0}");

        if (PurchaseState != null)
        {
            HandleNextPurchaseStep();

            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Times, "Cancel Auto-Buy"))
                CancelAutoPurchase();
        }
        else
        {
            int toPurchase = Math.Min(GetMaxItemsToPurchase(), missingItems);
            if (toPurchase > 0)
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.DollarSign,
                        $"Auto-Buy missing Dark Matter for {ItemForSale.Price * toPurchase:N0}{SeIconChar.Gil.ToIconString()}"))
                {
                    StartAutoPurchase(toPurchase);
                    HandleNextPurchaseStep();
                }
            }
        }
    }

    protected override unsafe void FirePurchaseCallback(AtkUnitBase* addonShop, int buyNow)
    {
        var buyItem = stackalloc AtkValue[]
        {
            new() { Type = ValueType.Int, Int = 0 },
            new() { Type = ValueType.Int, Int = ItemForSale!.Position },
            new() { Type = ValueType.Int, Int = buyNow },
            new() { Type = 0, Int = 0 }
        };
        addonShop->FireCallback(4, buyItem);
    }
}
