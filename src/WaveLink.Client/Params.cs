using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WaveLink.Client;

// Requests
public sealed record SetInputDeviceParams
{
    [JsonPropertyName("id")] public required string Id { get; init; } // inputDeviceId
    [JsonPropertyName("inputs")] public required List<SetInputParams> Inputs { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record SetInputParams
{
    [JsonPropertyName("id")] public required string Id { get; init; } // inputId
    [JsonPropertyName("isMuted")] public bool? IsMuted { get; init; }
    [JsonPropertyName("gain")] public GainValue? Gain { get; init; }
    [JsonPropertyName("micPcMix")] public MicPcMixValue? MicPcMix { get; init; }
    [JsonPropertyName("effects")] public List<EffectToggle>? Effects { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record GainValue
{
    [JsonPropertyName("value")] public required double Value { get; init; } // 0..1
}

public sealed record MicPcMixValue
{
    [JsonPropertyName("value")] public required double Value { get; init; } // 0..1
}

public sealed record EffectToggle
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("isEnabled")] public required bool IsEnabled { get; init; }
}

public sealed record SetOutputDeviceParams
{
    [JsonPropertyName("outputDevice")] public OutputDeviceUpdate? OutputDevice { get; init; }
    [JsonPropertyName("mainOutput")] public MainOutput? MainOutput { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record OutputDeviceUpdate
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("outputs")] public required List<OutputUpdate> Outputs { get; init; }
}

public sealed record OutputUpdate
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("isMuted")] public bool? IsMuted { get; init; }
    [JsonPropertyName("level")] public double? Level { get; init; } // 0..1
}

public sealed record SetChannelParams
{
    [JsonPropertyName("id")] public required string Id { get; init; } // channelId
    [JsonPropertyName("mixes")] public List<ChannelMixUpdate>? Mixes { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record ChannelMixUpdate
{
    [JsonPropertyName("mixId")] public required string MixId { get; init; }
    [JsonPropertyName("isMuted")] public bool? IsMuted { get; init; }
    [JsonPropertyName("level")] public double? Level { get; init; } // 0..1
}

public sealed record SetMixParams
{
    [JsonPropertyName("id")] public required string Id { get; init; } // mixId
    [JsonPropertyName("isMuted")] public bool? IsMuted { get; init; }
    [JsonPropertyName("level")] public double? Level { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record AddToChannelParams
{
    [JsonPropertyName("appId")] public required string AppId { get; init; }
    [JsonPropertyName("channelId")] public required string ChannelId { get; init; }
}

public sealed record SetSubscriptionParams
{
    [JsonPropertyName("focusedAppChanged")] public SubscriptionToggle? FocusedAppChanged { get; init; }
    [JsonPropertyName("levelMeterChanged")] public LevelMeterSubscription? LevelMeterChanged { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record SubscriptionToggle
{
    [JsonPropertyName("isEnabled")] public required bool IsEnabled { get; init; }
}

public sealed record LevelMeterSubscription
{
    [JsonPropertyName("isEnabled")] public required bool IsEnabled { get; init; }

    /// <summary>"" | "input" | "output" | "channel" | "mix"</summary>
    [JsonPropertyName("type")] public string? Type { get; init; }

    /// <summary>Specific id or "all".</summary>
    [JsonPropertyName("id")] public string? Id { get; init; }

    /// <summary>Some servers return/use subId.</summary>
    [JsonPropertyName("subId")] public string? SubId { get; init; }
}
