using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WaveLink.Client;

public sealed record SetSubscriptionResult
{
    [JsonPropertyName("focusedAppChanged")] public SubscriptionAck? FocusedAppChanged { get; init; }
    [JsonPropertyName("levelMeterChanged")] public SubscriptionAck? LevelMeterChanged { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record SubscriptionAck
{
    [JsonPropertyName("isEnabled")] public bool? IsEnabled { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("subId")] public string? SubId { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
