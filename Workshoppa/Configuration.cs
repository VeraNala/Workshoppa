using System.Collections.Generic;
using Dalamud.Configuration;

namespace Workshoppa;

internal sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public CurrentItem? CurrentlyCraftedItem = null;
    public List<QueuedItem> ItemQueue = new();

    internal sealed class QueuedItem
    {
        public uint WorkshopItemId { get; set; }
        public int Quantity { get; set; }
    }

    internal sealed class CurrentItem
    {
        public uint WorkshopItemId { get; set; }
        public bool StartedCrafting { get; set; }
        public bool FinishedCrafting { get; set; }
    }
}
