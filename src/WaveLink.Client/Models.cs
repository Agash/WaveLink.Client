using System.Text.Json;
using System.Text.Json.Serialization;

namespace WaveLink.Client;

// Application
/// <summary>Information about the Wave Link application.</summary>
public sealed record ApplicationInfo
{
    /// <summary>The application ID.</summary>
    [JsonPropertyName("appID")] public required string AppId { get; init; }

    /// <summary>The application name.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The interface revision supported by the server.</summary>
    [JsonPropertyName("interfaceRevision")] public required int InterfaceRevision { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

// Input devices
/// <summary>Result returned by the server for the input devices list.</summary>
public sealed record InputDevicesResult
{
    /// <summary>List of input devices.</summary>
    [JsonPropertyName("inputDevices")] public required List<InputDevice> InputDevices { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Represents an input device and its inputs.</summary>
public sealed record InputDevice
{
    /// <summary>Unique identifier for this input device.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Display name of the input device.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>Indicates whether this is a Wave Link device.</summary>
    [JsonPropertyName("isWaveDevice")] public bool? IsWaveDevice { get; init; }

    /// <summary>List of inputs on this device.</summary>
    [JsonPropertyName("inputs")] public required List<Input> Inputs { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Represents a single input on an input device.</summary>
public sealed record Input
{
    /// <summary>Unique identifier for this input.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Display name of the input.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>Indicates whether the input is muted.</summary>
    [JsonPropertyName("isMuted")] public bool? IsMuted { get; init; }

    /// <summary>Indicates whether the hardware gain lock is on (if supported).</summary>
    [JsonPropertyName("isGainLockOn")] public bool? IsGainLockOn { get; init; }

    /// <summary>Gain settings for this input.</summary>
    [JsonPropertyName("gain")] public Gain? Gain { get; init; }

    /// <summary>Microphone to PC audio mix settings for this input.</summary>
    [JsonPropertyName("micPcMix")] public MicPcMix? MicPcMix { get; init; }

    /// <summary>List of software effects applied to this input.</summary>
    [JsonPropertyName("effects")] public List<Effect>? Effects { get; init; }

    /// <summary>List of hardware DSP effects applied to this input.</summary>
    [JsonPropertyName("dspEffects")] public List<Effect>? DspEffects { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Represents gain information for an input.</summary>
public sealed record Gain
{
    /// <summary>The gain value normalized to the range 0..1.</summary>
    [JsonPropertyName("value")] public required double Value { get; init; }

    /// <summary>The maximum gain value in dB.</summary>
    [JsonPropertyName("max")] public double? Max { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Represents mic/PC mix value and related metadata.</summary>
public sealed record MicPcMix
{
    /// <summary>The mic/PC mix value normalized to the range 0..1.</summary>
    [JsonPropertyName("value")] public required double Value { get; init; }

    /// <summary>Indicates whether the mix direction is inverted.</summary>
    [JsonPropertyName("isInverted")] public bool? IsInverted { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Represents an audio effect available on an input.</summary>
public sealed record Effect
{
    /// <summary>Unique identifier for this effect.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Display name of the effect.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>Indicates whether the effect is currently enabled.</summary>
    [JsonPropertyName("isEnabled")] public bool? IsEnabled { get; init; }

    /// <summary>Indicates whether the effect is supported by the device.</summary>
    [JsonPropertyName("isSupported")] public bool? IsSupported { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

// Output devices
/// <summary>Result returned by the server for the output devices list.</summary>
public sealed record OutputDevicesResult
{
    /// <summary>The current main output selection.</summary>
    [JsonPropertyName("mainOutput")] public required MainOutput MainOutput { get; init; }

    /// <summary>List of output devices.</summary>
    [JsonPropertyName("outputDevices")] public required List<OutputDevice> OutputDevices { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Identifies the selected main output.</summary>
public sealed record MainOutput
{
    /// <summary>The ID of the output device containing the main output.</summary>
    [JsonPropertyName("outputDeviceId")] public required string OutputDeviceId { get; init; }

    /// <summary>The ID of the main output on the device.</summary>
    [JsonPropertyName("outputId")] public required string OutputId { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Represents an output device and its outputs.</summary>
public sealed record OutputDevice
{
    /// <summary>Unique identifier for this output device.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Display name of the output device.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>Indicates whether this is a Wave Link device.</summary>
    [JsonPropertyName("isWaveDevice")] public bool? IsWaveDevice { get; init; }

    /// <summary>List of outputs on this device.</summary>
    [JsonPropertyName("outputs")] public required List<Output> Outputs { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Represents a single output on an output device.</summary>
public sealed record Output
{
    /// <summary>Unique identifier for this output.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Display name of the output.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>Indicates whether the output is muted.</summary>
    [JsonPropertyName("isMuted")] public bool? IsMuted { get; init; }

    /// <summary>The output level normalized to the range 0..1.</summary>
    [JsonPropertyName("level")] public double? Level { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

// Channels & mixes
/// <summary>Result returned by the server for the channels list.</summary>
public sealed record ChannelsResult
{
    /// <summary>List of channels.</summary>
    [JsonPropertyName("channels")] public required List<Channel> Channels { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Represents an image definition for a channel or mix.</summary>
public sealed record ChannelImage
{
    /// <summary>The base64 image data or path.</summary>
    [JsonPropertyName("imgData")] public string? ImgData { get; init; }

    /// <summary>Indicates whether the image acts as an app icon.</summary>
    [JsonPropertyName("isAppIcon")] public bool? IsAppIcon { get; init; }

    /// <summary>The name of the image.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }
}

/// <summary>Represents a channel which can contain apps and mixes.</summary>
public sealed record Channel
{
    /// <summary>Unique identifier for this channel.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Display name of the channel.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The type of channel.</summary>
    [JsonPropertyName("type")] public string? Type { get; init; }

    /// <summary>Indicates whether the entire channel is muted.</summary>
    [JsonPropertyName("isMuted")] public bool? IsMuted { get; init; }

    /// <summary>The overall level of the channel normalized 0..1.</summary>
    [JsonPropertyName("level")] public double? Level { get; init; }

    /// <summary>The image properties assigned to the channel.</summary>
    [JsonPropertyName("image")] public ChannelImage? Image { get; init; }

    /// <summary>List of applications assigned to this channel.</summary>
    [JsonPropertyName("apps")] public List<AppRef>? Apps { get; init; }

    /// <summary>List of mixes in this channel.</summary>
    [JsonPropertyName("mixes")] public List<ChannelMix>? Mixes { get; init; }

    /// <summary>List of software effects on this channel.</summary>
    [JsonPropertyName("effects")] public List<Effect>? Effects { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Reference to an application that can be attached to a channel.</summary>
public sealed record AppRef
{
    /// <summary>Unique identifier for the application.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Display name of the application.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Represents a mix entry within a channel.</summary>
public sealed record ChannelMix
{
    /// <summary>The ID of the mix.</summary>
    [JsonPropertyName("mixId")] public required string MixId { get; init; }

    /// <summary>Indicates whether the mix is muted.</summary>
    [JsonPropertyName("isMuted")] public bool? IsMuted { get; init; }

    /// <summary>The mix level normalized to the range 0..1.</summary>
    [JsonPropertyName("level")] public double? Level { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Result returned by the server for the mixes list.</summary>
public sealed record MixesResult
{
    /// <summary>List of mixes.</summary>
    [JsonPropertyName("mixes")] public required List<Mix> Mixes { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Represents a mix (bus) in the Wave Link system.</summary>
public sealed record Mix
{
    /// <summary>Unique identifier for this mix.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Display name of the mix.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>Indicates whether the mix is muted.</summary>
    [JsonPropertyName("isMuted")] public bool? IsMuted { get; init; }

    /// <summary>The mix level.</summary>
    [JsonPropertyName("level")] public double? Level { get; init; }

    /// <summary>The image properties assigned to the mix.</summary>
    [JsonPropertyName("image")] public ChannelImage? Image { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

// Notifications
/// <summary>Notification payload for a focused app change.</summary>
public sealed record FocusedAppChangedParams
{
    /// <summary>Unique identifier for the focused application.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Display name of the focused application.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>The channel associated with the focused application.</summary>
    [JsonPropertyName("channel")] public FocusedAppChannel? Channel { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Represents the channel associated with a focused app.</summary>
public sealed record FocusedAppChannel
{
    /// <summary>The channel ID.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Notification payload for level meter updates.</summary>
public sealed record LevelMeterChangedParams
{
    /// <summary>Level meter entries for input devices.</summary>
    [JsonPropertyName("inputDevices")] public List<MeterEntry>? InputDevices { get; init; }

    /// <summary>Level meter entries for output devices.</summary>
    [JsonPropertyName("outputDevices")] public List<MeterEntry>? OutputDevices { get; init; }

    /// <summary>Level meter entries for channels.</summary>
    [JsonPropertyName("channels")] public List<MeterEntry>? Channels { get; init; }

    /// <summary>Level meter entries for mixes.</summary>
    [JsonPropertyName("mixes")] public List<MeterEntry>? Mixes { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>Entry describing left/right level percentages for an entity.</summary>
public sealed record MeterEntry
{
    /// <summary>The unique identifier of the metered entity.</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }

    /// <summary>Sub-identifier for specific mix associations.</summary>
    [JsonPropertyName("subId")] public string? SubId { get; init; }

    /// <summary>The left channel level as a percentage (0-100).</summary>
    [JsonPropertyName("levelLeftPercentage")] public double? LevelLeftPercentage { get; init; }

    /// <summary>The right channel level as a percentage (0-100).</summary>
    [JsonPropertyName("levelRightPercentage")] public double? LevelRightPercentage { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

// Other notifications
/// <summary>Notification payload for when the server requests creation of a profile.</summary>
public sealed record CreateProfileRequestedParams
{
    /// <summary>The type of device for which to create a profile.</summary>
    [JsonPropertyName("deviceType")] public string? DeviceType { get; init; }

    /// <summary>List of mix IDs to include in the profile.</summary>
    [JsonPropertyName("mixes")] public List<string>? Mixes { get; init; }

    /// <summary>Additional properties returned by the server.</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}