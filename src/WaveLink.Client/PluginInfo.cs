using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WaveLink.Client;

/// <summary>
/// Parameters for setPluginInfo as used by the official Stream Deck plugin.
/// </summary>
public sealed record SetPluginInfoParams
{
    [JsonPropertyName("connectedDevices")]
    public required List<string> ConnectedDevices { get; init; }
}
