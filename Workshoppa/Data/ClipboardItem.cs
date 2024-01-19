using System.Text.Json.Serialization;

namespace Workshoppa.Data;

public class ClipboardItem
{
    [JsonPropertyName("Id")]
    public uint WorkshopItemId { get; set; }

    [JsonPropertyName("Q")]
    public int Quantity { get; set; }
}
