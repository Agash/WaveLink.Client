using System.Text.Json;
using System.Text.Json.Serialization;

namespace WaveLink.Client;

// Requests
/// <summary>Parameters for the setInputDevice RPC.</summary>
public sealed record SetInputDeviceParams
{
    /// <summary>The input device ID.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>List of input changes to apply.</summary>
    [JsonPropertyName("inputs")] public required List<SetInputParams> Inputs { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Parameters describing changes to a single input.</summary>
public sealed record SetInputParams
{
    /// <summary>The input ID.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Mute state to apply, or null to leave unchanged.</summary>
    [JsonPropertyName("isMuted")] public bool? IsMuted { get; init; }

    /// <summary>Hardware gain lock state to apply, or null to leave unchanged.</summary>
    [JsonPropertyName("isGainLockOn")] public bool? IsGainLockOn { get; init; }

    /// <summary>Gain value to apply, or null to leave unchanged.</summary>
    [JsonPropertyName("gain")] public GainValue? Gain { get; init; }

    /// <summary>Mic/PC mix value to apply, or null to leave unchanged.</summary>
    [JsonPropertyName("micPcMix")] public MicPcMixValue? MicPcMix { get; init; }

    /// <summary>List of software effect state changes to apply.</summary>
    [JsonPropertyName("effects")] public List<EffectToggle>? Effects { get; init; }

    /// <summary>List of hardware DSP effect state changes to apply.</summary>
    [JsonPropertyName("dspEffects")] public List<EffectToggle>? DspEffects { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Represents a normalized gain value.</summary>
public sealed record GainValue
{
    /// <summary>The gain value normalized to the range 0..1.</summary>
    [JsonPropertyName("value")] public required double Value { get; init; }
}

/// <summary>Represents a normalized mic/pc mix value.</summary>
public sealed record MicPcMixValue
{
    /// <summary>The mic/PC mix value normalized to the range 0..1.</summary>
    [JsonPropertyName("value")] public required double Value { get; init; }
}

/// <summary>Toggle state for an effect.</summary>
public sealed record EffectToggle
{
    /// <summary>The ID of the effect to toggle.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Whether the effect should be enabled.</summary>
    [JsonPropertyName("isEnabled")] public required bool IsEnabled { get; init; }
}

/// <summary>Parameters for the setOutputDevice RPC.</summary>
public sealed record SetOutputDeviceParams
{
    /// <summary>Output device update, or null to skip updating output devices.</summary>
    [JsonPropertyName("outputDevice")] public OutputDeviceUpdate? OutputDevice { get; init; }

    /// <summary>Main output selection, or null to skip changing the main output.</summary>
    [JsonPropertyName("mainOutput")] public MainOutput? MainOutput { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Describes updates to an output device.</summary>
public sealed record OutputDeviceUpdate
{
    /// <summary>The output device ID.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>List of output changes to apply.</summary>
    [JsonPropertyName("outputs")] public required List<OutputUpdate> Outputs { get; init; }
}

/// <summary>Describes updates to a single output.</summary>
public sealed record OutputUpdate
{
    /// <summary>The output ID.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Mute state to apply, or null to leave unchanged.</summary>
    [JsonPropertyName("isMuted")] public bool? IsMuted { get; init; }

    /// <summary>Level value to apply, or null to leave unchanged.</summary>
    [JsonPropertyName("level")] public double? Level { get; init; }
}

/// <summary>Parameters for the setChannel RPC.</summary>
public sealed record SetChannelParams
{
    /// <summary>The channel ID.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>List of mix changes to apply to this channel.</summary>
    [JsonPropertyName("mixes")] public List<ChannelMixUpdate>? Mixes { get; init; }

    /// <summary>List of effect changes to apply to this channel.</summary>
    [JsonPropertyName("effects")] public List<EffectToggle>? Effects { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Describes updates to a channel mix entry.</summary>
public sealed record ChannelMixUpdate
{
    /// <summary>The mix ID.</summary>
    [JsonPropertyName("mixId")] public required string MixId { get; init; }

    /// <summary>Mute state to apply, or null to leave unchanged.</summary>
    [JsonPropertyName("isMuted")] public bool? IsMuted { get; init; }

    /// <summary>Level value to apply, or null to leave unchanged.</summary>
    [JsonPropertyName("level")] public double? Level { get; init; }
}

/// <summary>Parameters for the setMix RPC.</summary>
public sealed record SetMixParams
{
    /// <summary>The mix ID.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Mute state to apply, or null to leave unchanged.</summary>
    [JsonPropertyName("isMuted")] public bool? IsMuted { get; init; }

    /// <summary>Level value to apply, or null to leave unchanged.</summary>
    [JsonPropertyName("level")] public double? Level { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Parameters to add an app to a channel.</summary>
public sealed record AddToChannelParams
{
    /// <summary>The application ID.</summary>
    [JsonPropertyName("appId")] public required string AppId { get; init; }

    /// <summary>The channel ID.</summary>
    [JsonPropertyName("channelId")] public required string ChannelId { get; init; }
}

/// <summary>Parameters to configure server notification subscriptions.</summary>
public sealed record SetSubscriptionParams
{
    /// <summary>Subscription configuration for focused app change notifications.</summary>
    [JsonPropertyName("focusedAppChanged")] public SubscriptionToggle? FocusedAppChanged { get; init; }

    /// <summary>Subscription configuration for level meter change notifications.</summary>
    [JsonPropertyName("levelMeterChanged")] public LevelMeterSubscription? LevelMeterChanged { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Toggle to enable or disable a subscription.</summary>
public sealed record SubscriptionToggle
{
    /// <summary>Whether the subscription is enabled.</summary>
    [JsonPropertyName("isEnabled")] public required bool IsEnabled { get; init; }
}

/// <summary>Parameters for subscribing to level meter notifications.</summary>
public sealed record LevelMeterSubscription
{
    /// <summary>Whether level meter notifications should be enabled.</summary>
    [JsonPropertyName("isEnabled")]
    public required bool IsEnabled { get; init; }

    /// <summary>The type filter: "" for all, "input", "output", "channel", or "mix".</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The entity ID filter, or "all" for all entities of the given type.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The sub-ID filter, used by some servers for additional filtering.</summary>
    [JsonPropertyName("subId")]
    public string? SubId { get; init; }
}