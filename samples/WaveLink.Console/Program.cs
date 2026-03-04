using WaveLink.Client;

WaveLinkClient client = new();

client.Disconnected += (_, _) => Console.WriteLine("Disconnected.");
client.FocusedAppChanged += (_, e) => Console.WriteLine($"Focused app: {e.Name} ({e.Id}) -> channel {e.Channel?.Id}");
client.LevelMeterChanged += (_, e) => Console.WriteLine($"Level meters update: channels={e.Channels?.Count ?? 0}");

await client.ConnectAsync();

ApplicationInfo info = await client.GetApplicationInfoAsync();
Console.WriteLine($"Connected to: {info.Name} ({info.AppId}) rev={info.InterfaceRevision}");

await client.GetInputDevicesAsync();
await client.GetOutputDevicesAsync();
await client.GetChannelsAsync();
await client.GetMixesAsync();

// Subscribe to focused app changes and all channel meters
await client.SetSubscriptionAsync(new SetSubscriptionParams
{
    FocusedAppChanged = new SubscriptionToggle { IsEnabled = true },
    LevelMeterChanged = new LevelMeterSubscription { IsEnabled = true, Type = "channel", Id = "all" }
});

Console.WriteLine("Press Enter to exit.");
Console.ReadLine();

await client.DisposeAsync();