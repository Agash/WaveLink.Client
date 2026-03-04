using System.Text.Json.Serialization;

namespace WaveLink.Client;

/// <summary>Parameters for the server "setPluginInfo" call used by the official Stream Deck plugin.</summary>
public sealed record SetPluginInfoParams
{
    /// <summary>List of connected device identifiers.</summary>
    [JsonPropertyName("connectedDevices")]
    public required List<string> ConnectedDevices { get; init; }
}
