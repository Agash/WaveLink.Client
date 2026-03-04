using System;

namespace WaveLink.Client;

public sealed record WaveLinkClientOptions
{
    /// <summary>Override port discovery by forcing a port.</summary>
    public int? PortOverride { get; init; }

    /// <summary>Override the ws-info.json absolute file path.</summary>
    public string? WsInfoFilePathOverride { get; init; }

    /// <summary>Fallback scan range when ws-info.json is not available.</summary>
    public int MinPort { get; init; } = 1884;

    /// <summary>Fallback scan range when ws-info.json is not available.</summary>
    public int MaxPort { get; init; } = 1893;

    /// <summary>WebSocket connection timeout.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>Default per-RPC call timeout.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Origin header used by the official Stream Deck plugin.</summary>
    public string OriginHeader { get; init; } = "streamdeck://";
}
