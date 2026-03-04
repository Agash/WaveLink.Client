using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WaveLink.Client;

// Application
public sealed record ApplicationInfo
{
    [JsonPropertyName("appID")] public required string AppId { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("interfaceRevision")] public required int InterfaceRevision { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

// Input devices
public sealed record InputDevicesResult
{
    [JsonPropertyName("inputDevices")] public required List<InputDevice> InputDevices { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record InputDevice
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("isWaveDevice")] public bool? IsWaveDevice { get; init; }
    [JsonPropertyName("inputs")] public required List<Input> Inputs { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record Input
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("isMuted")] public bool? IsMuted { get; init; }
    [JsonPropertyName("gain")] public Gain? Gain { get; init; }
    [JsonPropertyName("micPcMix")] public MicPcMix? MicPcMix { get; init; }
    [JsonPropertyName("effects")] public List<Effect>? Effects { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record Gain
{
    [JsonPropertyName("value")] public required double Value { get; init; }  // 0..1
    [JsonPropertyName("max")] public double? Max { get; init; }            // dB range
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record MicPcMix
{
    [JsonPropertyName("value")] public required double Value { get; init; } // 0..1
    [JsonPropertyName("isInverted")] public bool? IsInverted { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record Effect
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("isEnabled")] public bool? IsEnabled { get; init; }
    [JsonPropertyName("isSupported")] public bool? IsSupported { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

// Output devices
public sealed record OutputDevicesResult
{
    [JsonPropertyName("mainOutput")] public required MainOutput MainOutput { get; init; }
    [JsonPropertyName("outputDevices")] public required List<OutputDevice> OutputDevices { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record MainOutput
{
    [JsonPropertyName("outputDeviceId")] public required string OutputDeviceId { get; init; }
    [JsonPropertyName("outputId")] public required string OutputId { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record OutputDevice
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("isWaveDevice")] public bool? IsWaveDevice { get; init; }
    [JsonPropertyName("outputs")] public required List<Output> Outputs { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record Output
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("isMuted")] public bool? IsMuted { get; init; }
    [JsonPropertyName("level")] public double? Level { get; init; } // 0..1

    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

// Channels & mixes
public sealed record ChannelsResult
{
    [JsonPropertyName("channels")] public required List<Channel> Channels { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record Channel
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }

    [JsonPropertyName("apps")] public List<AppRef>? Apps { get; init; }
    [JsonPropertyName("mixes")] public List<ChannelMix>? Mixes { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record AppRef
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record ChannelMix
{
    [JsonPropertyName("mixId")] public required string MixId { get; init; }
    [JsonPropertyName("isMuted")] public bool? IsMuted { get; init; }
    [JsonPropertyName("level")] public double? Level { get; init; } // 0..1
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record MixesResult
{
    [JsonPropertyName("mixes")] public required List<Mix> Mixes { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record Mix
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("isMuted")] public bool? IsMuted { get; init; }
    [JsonPropertyName("level")] public double? Level { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

// Notifications
public sealed record FocusedAppChangedParams
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("channel")] public FocusedAppChannel? Channel { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record FocusedAppChannel
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record LevelMeterChangedParams
{
    [JsonPropertyName("inputDevices")] public List<MeterEntry>? InputDevices { get; init; }
    [JsonPropertyName("outputDevices")] public List<MeterEntry>? OutputDevices { get; init; }
    [JsonPropertyName("channels")] public List<MeterEntry>? Channels { get; init; }
    [JsonPropertyName("mixes")] public List<MeterEntry>? Mixes { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record MeterEntry
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("levelLeftPercentage")] public double? LevelLeftPercentage { get; init; }
    [JsonPropertyName("levelRightPercentage")] public double? LevelRightPercentage { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

// Other notifications
public sealed record CreateProfileRequestedParams
{
    [JsonPropertyName("deviceType")] public string? DeviceType { get; init; }
    [JsonPropertyName("mixes")] public List<string>? Mixes { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
