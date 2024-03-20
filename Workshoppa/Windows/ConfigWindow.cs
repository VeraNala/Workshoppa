using System.Numerics;
using Dalamud.Plugin;
using ImGuiNET;
using LLib.ImGui;

namespace Workshoppa.Windows;

internal sealed class ConfigWindow : LWindow
{
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly Configuration _configuration;

    public ConfigWindow(DalamudPluginInterface pluginInterface, Configuration configuration)
        : base("Workshoppa - Configuration###WorkshoppaConfigWindow")

    {
        _pluginInterface = pluginInterface;
        _configuration = configuration;

        Position = new Vector2(100, 100);
        PositionCondition = ImGuiCond.FirstUseEver;
        Flags = ImGuiWindowFlags.AlwaysAutoResize;
    }

    public override void Draw()
    {
        bool enableRepairKitCalculator = _configuration.EnableRepairKitCalculator;
        if (ImGui.Checkbox("Enable Repair Kit Calculator", ref enableRepairKitCalculator))
        {
            _configuration.EnableRepairKitCalculator = enableRepairKitCalculator;
            _pluginInterface.SavePluginConfig(_configuration);
        }

        bool enableCeruleumTankCalculator = _configuration.EnableCeruleumTankCalculator;
        if (ImGui.Checkbox("Enable Ceruleum Tank Calculator", ref enableCeruleumTankCalculator))
        {
            _configuration.EnableCeruleumTankCalculator = enableCeruleumTankCalculator;
            _pluginInterface.SavePluginConfig(_configuration);
        }
    }
}
