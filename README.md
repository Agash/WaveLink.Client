# WaveLink.Client (.NET 10 / Native AOT friendly)

A C# client for the local WebSocket JSON-RPC API exposed by **Elgato Wave Link 3.x**.

- Target: **.NET 9.0 & .NET 10.0**, **C# 14**
- Serializer: **System.Text.Json Source Generation** (100% Native AOT & Trimming compatible)
- Extracted features: Supports hardware `DspEffects`, `IsGainLockOn`, and advanced `Channel` metadata.

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

// Subscribe to focused app + level meters
await client.SetSubscriptionAsync(new SetSubscriptionParams
{
    FocusedAppChanged = new SubscriptionToggle { IsEnabled = true },
    LevelMeterChanged = new LevelMeterSubscription { IsEnabled = true, Type = "all", Id = "all" }
});
```

## Consuming Events

WaveLink.Client gives you two modern ways to consume real-time events.

### Option 1: Modern `IAsyncEnumerable<T>` (Recommended)
You can use standard `await foreach` loops to process events asynchronously without dealing with traditional event handler memory leaks.

```csharp
// Runs asynchronously in the background
_ = Task.Run(async () => 
{
    await foreach (var meters in client.StreamLevelMetersAsync(cancellationToken))
    {
        Console.WriteLine($"Meters updated. Channels={meters.Channels?.Count ?? 0}");
    }
});

_ = Task.Run(async () => 
{
    await foreach (var app in client.StreamFocusedAppChangesAsync(cancellationToken))
    {
        Console.WriteLine($"Focused App Changed: {app.Name} -> {app.Channel?.Id}");
    }
});
```

### Option 2: Traditional .NET Events
```csharp
client.FocusedAppChanged += (_, e) => Console.WriteLine($"Focused: {e.Name} -> {e.Channel?.Id}");
client.LevelMeterChanged += (_, m) => Console.WriteLine($"Meters updated.");
```

## Notes
- This is a reverse-engineered client based on the Stream Deck plugin behavior.
- Unknown fields introduced by newer Wave Link updates are safely preserved inside `ExtensionData` dictionaries.
