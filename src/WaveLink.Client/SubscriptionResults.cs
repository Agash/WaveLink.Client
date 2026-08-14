using System.Text.Json;
using System.Text.Json.Serialization;

namespace WaveLink.Client;

/// <summary>Result returned by the server for a subscription request.</summary>
public sealed record SetSubscriptionResult
{
    /// <summary>Acknowledgment for the focused app changed subscription.</summary>
    [JsonPropertyName("focusedAppChanged")] public SubscriptionAck? FocusedAppChanged { get; set; }

    /// <summary>Acknowledgment for the level meter changed subscription.</summary>
    [JsonPropertyName("levelMeterChanged")] public SubscriptionAck? LevelMeterChanged { get; set; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>Acknowledgment for a subscription configuration.</summary>
public sealed record SubscriptionAck
{
    /// <summary>Whether the subscription is enabled.</summary>
    [JsonPropertyName("isEnabled")] public bool? IsEnabled { get; set; }

    /// <summary>Type of the subscription target.</summary>
    [JsonPropertyName("type")] public string? Type { get; set; }

    /// <summary>ID of the subscription target.</summary>
    [JsonPropertyName("id")] public string? Id { get; set; }

    /// <summary>Sub-ID of the subscription target if applicable.</summary>
    [JsonPropertyName("subId")] public string? SubId { get; set; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
