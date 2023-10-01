using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using Workshoppa.GameData;

namespace Workshoppa.Windows;

internal sealed class MainWindow : Window
{
    private readonly WorkshopPlugin _plugin;
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly Configuration _configuration;
    private readonly WorkshopCache _workshopCache;

    private string _searchString = string.Empty;

    public MainWindow(WorkshopPlugin plugin, DalamudPluginInterface pluginInterface, Configuration configuration, WorkshopCache workshopCache)
        : base("Workshoppa###WorkshoppaMainWindow")
    {
        _plugin = plugin;
        _pluginInterface = pluginInterface;
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

    public bool NearFabricationStation { get; set; } = false;
    public ButtonState State { get; set; } = ButtonState.None;

    public override void Draw()
    {
        var currentItem = _configuration.CurrentlyCraftedItem;
        if (currentItem != null)
        {
            var currentCraft = _workshopCache.Crafts.Single(x => x.WorkshopItemId == currentItem.WorkshopItemId);
            ImGui.Text($"Currently Crafting: {currentCraft.Name}");

            ImGui.BeginDisabled(!NearFabricationStation);
            if (_plugin.CurrentStage == Stage.Stopped)
            {
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
            }
            else
            {
                ImGui.BeginDisabled(_plugin.CurrentStage == Stage.RequestStop);
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Pause, "Pause"))
                    State = ButtonState.Pause;

                ImGui.EndDisabled();
            }
            ImGui.EndDisabled();
        }
        else
        {
            ImGui.Text("Currently Crafting: ---");

            ImGui.BeginDisabled(!NearFabricationStation || _configuration.ItemQueue.Sum(x => x.Quantity) == 0 || _plugin.CurrentStage != Stage.Stopped);
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "Start Crafting"))
                State = ButtonState.Start;
            ImGui.EndDisabled();
        }

        ImGui.Separator();
        ImGui.Text("Queue:");
        //ImGui.BeginDisabled();
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

            ImGui.PopID();
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
        //ImGui.EndDisabled();

        ImGui.Separator();
        ImGui.Text($"Stage: {_plugin.CurrentStage}");
    }

    private void Save()
    {
        _pluginInterface.SavePluginConfig(_configuration);
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
