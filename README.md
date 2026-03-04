# WaveLink.Client (.NET 10 / Native AOT friendly)

A C# client for the local WebSocket JSON-RPC API exposed by **Elgato Wave Link 3.x**.

- Target: **.NET 10**, **C# 14**
- Serializer: **System.Text.Json source generation** (AOT compatible)
- Implements the same RPC surface used by the official Stream Deck **Wave Link 3** plugin:
  - `getApplicationInfo`, `setPluginInfo`
  - `getInputDevices`, `setInputDevice`
  - `getOutputDevices`, `setOutputDevice`
  - `getChannels`, `setChannel`
  - `addToChannel`
  - `getMixes`, `setMix`
  - `setSubscription`
- Dispatches notifications and maintains a lightweight state cache.

See **PROTOCOL.md** for the wire format.

## Quick start

```csharp
using WaveLink.Client;

var client = new WaveLinkClient();
await client.ConnectAsync();

var app = await client.GetApplicationInfoAsync();
Console.WriteLine($"{app.Name} ({app.AppId}) rev {app.InterfaceRevision}");

var inputs = await client.GetInputDevicesAsync();
var outputs = await client.GetOutputDevicesAsync();
var channels = await client.GetChannelsAsync();
var mixes = await client.GetMixesAsync();

// Subscribe to focused app + level meters
await client.SetSubscriptionAsync(new SetSubscriptionParams
{
    FocusedAppChanged = new SubscriptionToggle { IsEnabled = true },
    LevelMeterChanged = new LevelMeterSubscription { IsEnabled = true, Type = "all", Id = "all" }
});
```

## Events

```csharp
client.FocusedAppChanged += (_, e) => Console.WriteLine($"Focused: {e.Name} -> {e.Channel?.Id}");
client.LevelMeterChanged += (_, m) => Console.WriteLine($"Meters updated. Channels={m.Channels?.Count ?? 0}");
```

## Notes

- This is a reverse-engineered client based on the Stream Deck plugin behavior.
- Wave Link may add fields over time; models use `JsonExtensionData` so unknown fields are preserved and ignored by default.
