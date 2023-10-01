using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Workshoppa.GameData;

public sealed class CraftState
{
    public required uint ResultItem { get; init; }
    public required uint StepsComplete { get; init; }
    public required uint StepsTotal { get; init; }
    public required List<CraftItem> Items { get; init; }

    public bool IsPhaseComplete() => Items.All(x => x.Finished || x.StepsComplete == x.StepsTotal);

    public bool IsCraftComplete() => StepsComplete == StepsTotal - 1 && IsPhaseComplete();
}
