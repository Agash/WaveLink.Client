using System.Text.Json;
using System.Text.Json.Serialization;

namespace WaveLink.Client;

/// <summary>
/// Provides a source generation context for System.Text.Json serialization and deserialization of WaveLink API types.
/// </summary>
/// <remarks>
/// This context enables efficient, strongly-typed JSON serialization for a set of WaveLink-related
/// models, using source generation to improve performance and reduce runtime reflection. The context is configured to
/// use metadata generation mode, disables indented output, and ignores properties with null values when writing JSON.
/// Use this context with System.Text.Json APIs to serialize or deserialize supported types in WaveLink
/// communications.
/// </remarks>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonRpcRequest))]
[JsonSerializable(typeof(JsonRpcResponse))]
[JsonSerializable(typeof(JsonRpcNotification))]
[JsonSerializable(typeof(JsonRpcError))]
[JsonSerializable(typeof(ApplicationInfo))]
[JsonSerializable(typeof(InputDevicesResult))]
[JsonSerializable(typeof(InputDevice))]
[JsonSerializable(typeof(Input))]
[JsonSerializable(typeof(Gain))]
[JsonSerializable(typeof(MicPcMix))]
[JsonSerializable(typeof(Effect))]
[JsonSerializable(typeof(OutputDevicesResult))]
[JsonSerializable(typeof(MainOutput))]
[JsonSerializable(typeof(OutputDevice))]
[JsonSerializable(typeof(Output))]
[JsonSerializable(typeof(ChannelsResult))]
[JsonSerializable(typeof(Channel))]
[JsonSerializable(typeof(ChannelImage))]
[JsonSerializable(typeof(AppRef))]
[JsonSerializable(typeof(ChannelMix))]
[JsonSerializable(typeof(MixesResult))]
[JsonSerializable(typeof(Mix))]
[JsonSerializable(typeof(SetInputDeviceParams))]
[JsonSerializable(typeof(SetInputParams))]
[JsonSerializable(typeof(GainValue))]
[JsonSerializable(typeof(MicPcMixValue))]
[JsonSerializable(typeof(EffectToggle))]
[JsonSerializable(typeof(SetOutputDeviceParams))]
[JsonSerializable(typeof(OutputDeviceUpdate))]
[JsonSerializable(typeof(OutputUpdate))]
[JsonSerializable(typeof(SetChannelParams))]
[JsonSerializable(typeof(ChannelMixUpdate))]
[JsonSerializable(typeof(SetMixParams))]
[JsonSerializable(typeof(AddToChannelParams))]
[JsonSerializable(typeof(SetSubscriptionParams))]
[JsonSerializable(typeof(SubscriptionToggle))]
[JsonSerializable(typeof(LevelMeterSubscription))]
[JsonSerializable(typeof(SetPluginInfoParams))]
[JsonSerializable(typeof(FocusedAppChangedParams))]
[JsonSerializable(typeof(LevelMeterChangedParams))]
[JsonSerializable(typeof(MeterEntry))]
[JsonSerializable(typeof(SetSubscriptionResult))]
[JsonSerializable(typeof(SubscriptionAck))]
[JsonSerializable(typeof(CreateProfileRequestedParams))]
public partial class WaveLinkJsonContext : JsonSerializerContext;