using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using LLib;

namespace Workshoppa.External;

internal sealed class ExternalPluginHandler
{
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly IPluginLog _pluginLog;
    private readonly YesAlreadyIpc _yesAlreadyIpc;
    private readonly PandoraIpc _pandoraIpc;

    private bool? _yesAlreadyState;
    private bool? _pandoraState;

    public ExternalPluginHandler(DalamudPluginInterface pluginInterface, IFramework framework, IPluginLog pluginLog)
    {
        _pluginInterface = pluginInterface;
        _pluginLog = pluginLog;

        var dalamudReflector = new DalamudReflector(pluginInterface, framework, pluginLog);
        _yesAlreadyIpc = new YesAlreadyIpc(dalamudReflector);
        _pandoraIpc = new PandoraIpc(pluginInterface, pluginLog);
    }

    public bool Saved { get; private set; }

    public void Save()
    {
        if (Saved)
        {
            _pluginLog.Information("Not overwriting external plugin state");
            return;
        }

        _pluginLog.Information("Saving external plugin state...");
        SaveYesAlreadyState();
        SavePandoraState();
        Saved = true;
    }

    private void SaveYesAlreadyState()
    {
        _yesAlreadyState = _yesAlreadyIpc.DisableIfNecessary();
        _pluginLog.Information($"Previous yesalready state: {_yesAlreadyState}");
    }

    private void SavePandoraState()
    {
        _pandoraState = _pandoraIpc.DisableIfNecessary();
        _pluginLog.Information($"Previous pandora feature state: {_pandoraState}");
    }

    /// <summary>
    /// Unlike Pandora/YesAlready, we only disable TextAdvance during the item turn-in so that the cutscene skip
    /// still works (if enabled).
    /// </summary>
    public void SaveTextAdvance()
    {
        if (_pluginInterface.TryGetData<HashSet<string>>("TextAdvance.StopRequests", out var data) &&
            !data.Contains(nameof(Workshoppa)))
        {
            _pluginLog.Debug("Disabling textadvance");
            data.Add(nameof(Workshoppa));
        }
    }

    public void Restore()
    {
        if (Saved)
        {
            RestoreYesAlready();
            RestorePandora();
        }

        Saved = false;
        _yesAlreadyState = null;
        _pandoraState = null;
    }

    private void RestoreYesAlready()
    {
        _pluginLog.Information($"Restoring previous yesalready state: {_yesAlreadyState}");
        if (_yesAlreadyState == true)
            _yesAlreadyIpc.Enable();
    }

    private void RestorePandora()
    {
        _pluginLog.Information($"Restoring previous pandora state: {_pandoraState}");
        if (_pandoraState == true)
            _pandoraIpc.Enable();
    }

    public void RestoreTextAdvance()
    {
        if (_pluginInterface.TryGetData<HashSet<string>>("TextAdvance.StopRequests", out var data) &&
            data.Contains(nameof(Workshoppa)))
        {
            _pluginLog.Debug("Restoring textadvance");
            data.Remove(nameof(Workshoppa));
        }
    }
}
