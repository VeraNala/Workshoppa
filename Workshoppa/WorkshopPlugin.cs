using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Workshoppa.External;
using Workshoppa.GameData;
using Workshoppa.Windows;

namespace Workshoppa;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public sealed partial class WorkshopPlugin : IDalamudPlugin
{
    private const int FabricationStationId = 0x1E98F4;
    private readonly IReadOnlyList<ushort> _workshopTerritories = new ushort[] { 423, 424, 425, 653, 984 }.AsReadOnly();
    private readonly WindowSystem _windowSystem = new WindowSystem(nameof(WorkshopPlugin));

    private readonly DalamudPluginInterface _pluginInterface;
    private readonly IGameGui _gameGui;
    private readonly IFramework _framework;
    private readonly ICondition _condition;
    private readonly IClientState _clientState;
    private readonly IObjectTable _objectTable;
    private readonly ICommandManager _commandManager;
    private readonly IPluginLog _pluginLog;

    private readonly Configuration _configuration;
    private readonly YesAlreadyIpc _yesAlreadyIpc;
    private readonly WorkshopCache _workshopCache;
    private readonly MainWindow _mainWindow;

    private Stage _currentStageInternal = Stage.Stopped;
    private DateTime _continueAt = DateTime.MinValue;
    private (bool Saved, bool? PreviousState) _yesAlreadyState = (false, null);

    public WorkshopPlugin(DalamudPluginInterface pluginInterface, IGameGui gameGui, IFramework framework,
        ICondition condition, IClientState clientState, IObjectTable objectTable, IDataManager dataManager,
        ICommandManager commandManager, IPluginLog pluginLog)
    {
        _pluginInterface = pluginInterface;
        _gameGui = gameGui;
        _framework = framework;
        _condition = condition;
        _clientState = clientState;
        _objectTable = objectTable;
        _commandManager = commandManager;
        _pluginLog = pluginLog;

        var dalamudReflector = new DalamudReflector(_pluginInterface, _framework, _pluginLog);
        _yesAlreadyIpc = new YesAlreadyIpc(dalamudReflector);
        _configuration = (Configuration?)_pluginInterface.GetPluginConfig() ?? new Configuration();
        _workshopCache = new WorkshopCache(dataManager, _pluginLog);

        _mainWindow = new(this, _pluginInterface, _clientState, _configuration, _workshopCache);
        _windowSystem.AddWindow(_mainWindow);

        _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _pluginInterface.UiBuilder.OpenMainUi += _mainWindow.Toggle;
        _framework.Update += FrameworkUpdate;
        _commandManager.AddHandler("/ws", new CommandInfo(ProcessCommand)
        {
            HelpMessage = "Open UI"
        });
    }

    internal Stage CurrentStage
    {
        get => _currentStageInternal;
        private set
        {
            if (_currentStageInternal != value)
            {
                _pluginLog.Information($"Changing stage from {_currentStageInternal} to {value}");
                _currentStageInternal = value;
            }
        }
    }

    private void FrameworkUpdate(IFramework framework)
    {
        if (!_clientState.IsLoggedIn ||
            !_workshopTerritories.Contains(_clientState.TerritoryType) ||
            _condition[ConditionFlag.BoundByDuty] ||
            GetDistanceToEventObject(FabricationStationId, out var fabricationStation) >= 5f)
        {
            _mainWindow.NearFabricationStation = false;
        }
        else if (DateTime.Now >= _continueAt)
        {
            _mainWindow.NearFabricationStation = true;

            if (_mainWindow.State is MainWindow.ButtonState.Pause or MainWindow.ButtonState.Stop)
            {
                _mainWindow.State = MainWindow.ButtonState.None;
                if (CurrentStage != Stage.Stopped)
                {
                    RestoreYesAlready();
                    CurrentStage = Stage.Stopped;
                }

                return;
            }
            else if (_mainWindow.State is MainWindow.ButtonState.Start or MainWindow.ButtonState.Resume && CurrentStage == Stage.Stopped)
            {
                _mainWindow.State = MainWindow.ButtonState.None;
                CurrentStage = Stage.TakeItemFromQueue;
            }

            if (CurrentStage != Stage.Stopped && CurrentStage != Stage.RequestStop && !_yesAlreadyState.Saved)
                SaveYesAlready();

            switch (CurrentStage)
            {
                case Stage.TakeItemFromQueue:
                    TakeItemFromQueue();
                    break;

                case Stage.TargetFabricationStation:
                    if (InteractWithFabricationStation(fabricationStation!))
                    {
                        if (_configuration.CurrentlyCraftedItem is { StartedCrafting: true })
                            CurrentStage = Stage.SelectCraftBranch;
                        else
                            CurrentStage = Stage.OpenCraftingLog;
                    }

                    break;

                case Stage.OpenCraftingLog:
                    OpenCraftingLog();
                    break;

                case Stage.SelectCraftCategory:
                    SelectCraftCategory();
                    break;

                case Stage.SelectCraft:
                    SelectCraft();
                    break;

                case Stage.ConfirmCraft:
                    ConfirmCraft();
                    break;

                case Stage.RequestStop:
                    RestoreYesAlready();
                    CurrentStage = Stage.Stopped;
                    break;

                case Stage.SelectCraftBranch:
                    SelectCraftBranch();
                    break;

                case Stage.ContributeMaterials:
                    ContributeMaterials();
                    break;

                case Stage.ConfirmMaterialDelivery:
                    ConfirmMaterialDelivery();
                    break;

                case Stage.ConfirmCollectProduct:
                    ConfirmCollectProduct();
                    break;

                case Stage.Stopped:
                    break;

                default:
                    _pluginLog.Warning($"Unknown stage {CurrentStage}");
                    break;
            }
        }
    }

    private WorkshopCraft GetCurrentCraft()
    {
        return _workshopCache.Crafts.Single(x => x.WorkshopItemId == _configuration.CurrentlyCraftedItem!.WorkshopItemId);
    }

    private void ProcessCommand(string command, string arguments) => _mainWindow.Toggle();

    public void Dispose()
    {
        _commandManager.RemoveHandler("/ws");
        _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        _pluginInterface.UiBuilder.OpenMainUi -= _mainWindow.Toggle;
        _framework.Update -= FrameworkUpdate;

        RestoreYesAlready();
    }

    private void SaveYesAlready()
    {
        if (_yesAlreadyState.Saved)
        {
            _pluginLog.Information("Not overwriting yesalready state");
            return;
        }

        _yesAlreadyState = (true, _yesAlreadyIpc.DisableIfNecessary());
        _pluginLog.Information($"Previous yesalready state: {_yesAlreadyState.PreviousState}");
    }

    private void RestoreYesAlready()
    {
        if (_yesAlreadyState.Saved)
        {
            _pluginLog.Information($"Restoring previous yesalready state: {_yesAlreadyState.PreviousState}");
            if (_yesAlreadyState.PreviousState == true)
                _yesAlreadyIpc.Enable();
        }

        _yesAlreadyState = (false, null);
    }
}
