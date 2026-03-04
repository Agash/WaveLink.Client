using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WaveLink.Client;

/// <summary>
/// A Wave Link local WebSocket JSON-RPC client.
/// Designed to be Native AOT friendly (System.Text.Json source-gen, no reflection required).
/// </summary>
public sealed class WaveLinkClient : IAsyncDisposable
{
    private readonly WaveLinkClientOptions _options;
    private readonly ClientWebSocket _ws = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonRpcResponse>> _pending = new();
    private readonly JsonSerializerOptions _jsonOptions;

    private Task? _recvLoop;
    private int _nextId;

    // Cached state (optional but handy for consumers)
    public ApplicationInfo? ApplicationInfo { get; private set; }
    public IReadOnlyList<InputDevice> InputDevices => _inputDevices;
    public IReadOnlyList<OutputDevice> OutputDevices => _outputDevices;
    public MainOutput? MainOutput { get; private set; }
    public IReadOnlyList<Channel> Channels => _channels;
    public IReadOnlyList<Mix> Mixes => _mixes;
    public LevelMeterChangedParams? LevelMeters { get; private set; }
    public FocusedAppChangedParams? FocusedApp { get; private set; }

    private List<InputDevice> _inputDevices = new();
    private List<OutputDevice> _outputDevices = new();
    private List<Channel> _channels = new();
    private List<Mix> _mixes = new();

    // Events
    public event EventHandler? Disconnected;

    public event EventHandler<IReadOnlyList<InputDevice>>? InputDevicesChanged;
    public event EventHandler<InputDevice>? InputDeviceChanged;

    public event EventHandler<(MainOutput mainOutput, IReadOnlyList<OutputDevice> outputDevices)>? OutputDevicesChanged;
    public event EventHandler<OutputDevice>? OutputDeviceChanged;

    public event EventHandler<IReadOnlyList<Channel>>? ChannelsChanged;
    public event EventHandler<Channel>? ChannelChanged;

    public event EventHandler<IReadOnlyList<Mix>>? MixesChanged;
    public event EventHandler<Mix>? MixChanged;

    public event EventHandler<LevelMeterChangedParams>? LevelMeterChanged;
    public event EventHandler<FocusedAppChangedParams>? FocusedAppChanged;
    public event EventHandler<CreateProfileRequestedParams>? CreateProfileRequested;

    public WaveLinkClient(WaveLinkClientOptions? options = null)
    {
        _options = options ?? new WaveLinkClientOptions();

        _jsonOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = WaveLinkJsonContext.Default,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_ws.State is WebSocketState.Open or WebSocketState.Connecting)
            return;

        var port = _options.PortOverride ?? await DiscoverPortAsync(cancellationToken).ConfigureAwait(false);
        var uri = new Uri($"ws://127.0.0.1:{port}");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.ConnectTimeout);

        _ws.Options.SetRequestHeader("Origin", _options.OriginHeader);

        try
        {
            await _ws.ConnectAsync(uri, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new WaveLinkException($"Failed to connect to Wave Link WebSocket at {uri}.", ex);
        }

        _recvLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token));

        // Basic validation handshake
        var info = await GetApplicationInfoAsync(cancellationToken).ConfigureAwait(false);
        ApplicationInfo = info;

        // The official plugin expects appID == "EWL" and interfaceRevision >= 1.
        if (!string.Equals(info.AppId, "EWL", StringComparison.Ordinal))
            throw new WaveLinkException($"Connected server returned unexpected appID '{info.AppId}'.");

        if (info.InterfaceRevision < 1)
            throw new WaveLinkException($"Connected server returned unsupported interfaceRevision {info.InterfaceRevision}.");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _cts.Cancel();
            if (_ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Dispose", CancellationToken.None).ConfigureAwait(false); }
                catch { /* ignore */ }
            }
        }
        finally
        {
            _ws.Dispose();
            _cts.Dispose();
        }
    }

    // ---- Public API: methods used by the Stream Deck plugin ----

    public Task<ApplicationInfo> GetApplicationInfoAsync(CancellationToken ct = default)
        => CallAsync<ApplicationInfo>("getApplicationInfo", paramsObject: null, ct);

    public Task SetPluginInfoAsync(SetPluginInfoParams info, CancellationToken ct = default)
        => CallAsync("setPluginInfo", info, ct);

    public async Task<InputDevicesResult> GetInputDevicesAsync(CancellationToken ct = default)
    {
        var result = await CallAsync<InputDevicesResult>("getInputDevices", paramsObject: null, ct).ConfigureAwait(false);
        _inputDevices = result.InputDevices ?? new();
        InputDevicesChanged?.Invoke(this, _inputDevices);
        return result;
    }

    public Task SetInputDeviceAsync(SetInputDeviceParams p, CancellationToken ct = default)
        => CallAsync("setInputDevice", p, ct);

    public async Task<OutputDevicesResult> GetOutputDevicesAsync(CancellationToken ct = default)
    {
        var result = await CallAsync<OutputDevicesResult>("getOutputDevices", paramsObject: null, ct).ConfigureAwait(false);
        MainOutput = result.MainOutput;
        _outputDevices = result.OutputDevices ?? new();
        OutputDevicesChanged?.Invoke(this, (result.MainOutput, _outputDevices));
        return result;
    }

    public Task SetOutputDeviceAsync(SetOutputDeviceParams p, CancellationToken ct = default)
        => CallAsync("setOutputDevice", p, ct);

    public async Task<ChannelsResult> GetChannelsAsync(CancellationToken ct = default)
    {
        var result = await CallAsync<ChannelsResult>("getChannels", paramsObject: null, ct).ConfigureAwait(false);
        _channels = result.Channels ?? new();
        ChannelsChanged?.Invoke(this, _channels);
        return result;
    }

    public Task SetChannelAsync(SetChannelParams p, CancellationToken ct = default)
        => CallAsync("setChannel", p, ct);

    public Task AddToChannelAsync(AddToChannelParams p, CancellationToken ct = default)
        => CallAsync("addToChannel", p, ct);

    public async Task<MixesResult> GetMixesAsync(CancellationToken ct = default)
    {
        var result = await CallAsync<MixesResult>("getMixes", paramsObject: null, ct).ConfigureAwait(false);
        _mixes = result.Mixes ?? new();
        MixesChanged?.Invoke(this, _mixes);
        return result;
    }

    public Task SetMixAsync(SetMixParams p, CancellationToken ct = default)
        => CallAsync("setMix", p, ct);

    public Task<SetSubscriptionResult> SetSubscriptionAsync(SetSubscriptionParams p, CancellationToken ct = default)
        => CallAsync<SetSubscriptionResult>("setSubscription", p, ct);

    // ---- Convenience helpers ----

    public Task SetOutputLevelAsync(string outputDeviceId, string outputId, double level0to1, CancellationToken ct = default)
        => SetOutputDeviceAsync(new SetOutputDeviceParams
        {
            OutputDevice = new OutputDeviceUpdate
            {
                Id = outputDeviceId,
                Outputs = new List<OutputUpdate> { new() { Id = outputId, Level = Clamp01(level0to1) } }
            }
        }, ct);

    public Task SetOutputMuteAsync(string outputDeviceId, string outputId, bool isMuted, CancellationToken ct = default)
        => SetOutputDeviceAsync(new SetOutputDeviceParams
        {
            OutputDevice = new OutputDeviceUpdate
            {
                Id = outputDeviceId,
                Outputs = new List<OutputUpdate> { new() { Id = outputId, IsMuted = isMuted } }
            }
        }, ct);

    public Task SetMainOutputAsync(string outputDeviceId, string outputId, CancellationToken ct = default)
        => SetOutputDeviceAsync(new SetOutputDeviceParams
        {
            MainOutput = new MainOutput { OutputDeviceId = outputDeviceId, OutputId = outputId }
        }, ct);

    public Task SetInputMuteAsync(string inputDeviceId, string inputId, bool isMuted, CancellationToken ct = default)
        => SetInputDeviceAsync(new SetInputDeviceParams
        {
            Id = inputDeviceId,
            Inputs = new List<SetInputParams> { new() { Id = inputId, IsMuted = isMuted } }
        }, ct);

    public Task SetInputGainNormalizedAsync(string inputDeviceId, string inputId, double value0to1, CancellationToken ct = default)
        => SetInputDeviceAsync(new SetInputDeviceParams
        {
            Id = inputDeviceId,
            Inputs = new List<SetInputParams> { new() { Id = inputId, Gain = new GainValue { Value = Clamp01(value0to1) } } }
        }, ct);

    public Task SetMicPcMixNormalizedAsync(string inputDeviceId, string inputId, double value0to1, CancellationToken ct = default)
        => SetInputDeviceAsync(new SetInputDeviceParams
        {
            Id = inputDeviceId,
            Inputs = new List<SetInputParams> { new() { Id = inputId, MicPcMix = new MicPcMixValue { Value = Clamp01(value0to1) } } }
        }, ct);

    public static double Clamp01(double value) => value < 0 ? 0 : (value > 1 ? 1 : value);

    // ---- Internals ----

    private async Task<int> DiscoverPortAsync(CancellationToken ct)
    {
        // 1) ws-info.json
        var wsInfoPath = _options.WsInfoFilePathOverride ?? GetDefaultWsInfoFilePath();
        if (!string.IsNullOrWhiteSpace(wsInfoPath))
        {
            var port = await TryReadPortFromWsInfoAsync(wsInfoPath!, ct).ConfigureAwait(false);
            if (port is int p and > 0)
                return p;
        }

        // 2) fallback scan
        for (var p = _options.MinPort; p <= _options.MaxPort; p++)
        {
            ct.ThrowIfCancellationRequested();
            if (await CanConnectAsync(p, ct).ConfigureAwait(false))
                return p;
        }

        throw new WaveLinkException($"Unable to discover Wave Link WebSocket port (ws-info.json missing and scan {_options.MinPort}-{_options.MaxPort} failed).");
    }

    private static string? GetDefaultWsInfoFilePath()
    {
        // Mirrors the official plugin: process.env.APPDATA?.replace("Roaming","Local/Packages/Elgato.WaveLink_g54w8ztgkx496/LocalState/ws-info.json")
        // We use platform-safe logic; on Windows this typically resolves to:
        // %LOCALAPPDATA%\Packages\Elgato.WaveLink_g54w8ztgkx496\LocalState\ws-info.json
        try
        {
            var appData = Environment.GetEnvironmentVariable("APPDATA");
            if (string.IsNullOrWhiteSpace(appData))
                return null;

            // Replace "...\Roaming" with "...\Local\Packages\...\LocalState\ws-info.json"
            if (appData.EndsWith("Roaming", StringComparison.OrdinalIgnoreCase))
            {
                var baseDir = appData[..^"Roaming".Length].TrimEnd('\', '/');
                return Path.Combine(baseDir, "Local", "Packages", "Elgato.WaveLink_g54w8ztgkx496", "LocalState", "ws-info.json");
            }

            // If APPDATA doesn't end with Roaming, still attempt a best-effort path next to LocalAppData.
            var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrWhiteSpace(localAppData))
                return Path.Combine(localAppData, "Packages", "Elgato.WaveLink_g54w8ztgkx496", "LocalState", "ws-info.json");
        }
        catch { }
        return null;
    }

    private static async Task<int?> TryReadPortFromWsInfoAsync(string path, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            using var stream = File.OpenRead(path);
            var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("port", out var portEl) && portEl.ValueKind == JsonValueKind.Number && portEl.TryGetInt32(out var port))
                return port;
        }
        catch { /* ignore */ }
        return null;
    }

    private async Task<bool> CanConnectAsync(int port, CancellationToken ct)
    {
        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Origin", _options.OriginHeader);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(700));

        try
        {
            await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{port}"), timeoutCts.Token).ConfigureAwait(false);
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "probe", CancellationToken.None).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<T> CallAsync<T>(string method, object? paramsObject, CancellationToken ct)
    {
        var resultEl = await CallCoreAsync(method, paramsObject, ct).ConfigureAwait(false);

        // JsonElement -> T using source-gen context
        if (typeof(T) == typeof(JsonElement))
            return (T)(object)(resultEl ?? default);

        if (resultEl is null)
            throw new WaveLinkException($"RPC '{method}' returned null result.");

        try
        {
            var value = resultEl.Value.Deserialize<T>(_jsonOptions);
            if (value is null)
                throw new WaveLinkException($"RPC '{method}' result could not be deserialized to {typeof(T).Name}.");
            return value;
        }
        catch (Exception ex)
        {
            throw new WaveLinkException($"Failed to deserialize RPC '{method}' result to {typeof(T).Name}.", ex);
        }
    }

    private Task CallAsync(string method, object? paramsObject, CancellationToken ct)
        => CallCoreAsync(method, paramsObject, ct);

    private async Task<JsonElement?> CallCoreAsync(string method, object? paramsObject, CancellationToken ct)
    {
        if (_ws.State != WebSocketState.Open)
            throw new WaveLinkException("WebSocket is not connected.");

        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, tcs))
            throw new WaveLinkException("Failed to register pending RPC.");

        JsonElement? paramsEl = null;
        if (paramsObject is not null)
        {
            // Serialize paramsObject to JsonElement using our options (AOT friendly)
            var bytes = JsonSerializer.SerializeToUtf8Bytes(paramsObject, paramsObject.GetType(), _jsonOptions);
            using var doc = JsonDocument.Parse(bytes);
            paramsEl = doc.RootElement.Clone();
        }
        else
        {
            // keep params as null (compatible with TS lib and accepted by Wave Link)
            paramsEl = null;
        }

        var req = new JsonRpcRequest { Id = id, Method = method, Params = paramsEl };

        var payload = JsonSerializer.Serialize(req, _jsonOptions);
        await SendTextAsync(payload, ct).ConfigureAwait(false);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_options.RequestTimeout);

        try
        {
            await using var _ = timeoutCts.Token.Register(() => tcs.TrySetCanceled(timeoutCts.Token));
            var resp = await tcs.Task.ConfigureAwait(false);

            if (resp.Error is not null)
            {
                object? data = resp.Error.Data;
                throw new WaveLinkRpcException(resp.Error.Code, resp.Error.Message, data);
            }

            return resp.Result;
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private async Task SendTextAsync(string text, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken: ct).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            var sb = new StringBuilder(64 * 1024);

            while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                sb.Clear();
                WebSocketReceiveResult? result;

                do
                {
                    result = await _ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                var message = sb.ToString();
                HandleIncoming(message);
            }
        }
        catch
        {
            // Treat as disconnect
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private void HandleIncoming(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Response?
            if (root.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number && idEl.TryGetInt32(out var id))
            {
                var resp = root.Deserialize<JsonRpcResponse>(_jsonOptions);
                if (resp is not null && _pending.TryGetValue(id, out var tcs))
                    tcs.TrySetResult(resp);
                return;
            }

            // Notification?
            if (root.TryGetProperty("method", out var methodEl) && methodEl.ValueKind == JsonValueKind.String)
            {
                var method = methodEl.GetString() ?? string.Empty;
                JsonElement? p = null;
                if (root.TryGetProperty("params", out var paramsEl))
                    p = paramsEl.Clone();

                DispatchNotification(method, p);
            }
        }
        catch
        {
            // ignore malformed messages
        }
    }

    private void DispatchNotification(string method, JsonElement? p)
    {
        try
        {
            switch (method)
            {
                case "inputDevicesChanged":
                    {
                        var parsed = p?.Deserialize<InputDevicesResult>(_jsonOptions);
                        if (parsed?.InputDevices is not null)
                        {
                            _inputDevices = parsed.InputDevices;
                            InputDevicesChanged?.Invoke(this, _inputDevices);
                        }
                        break;
                    }
                case "inputDeviceChanged":
                    {
                        // Partial update; merge by device id
                        if (p is null) break;
                        if (!p.Value.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.String) break;
                        var devId = idEl.GetString()!;
                        var update = p.Value.Deserialize<InputDevice>(_jsonOptions);
                        if (update is null) break;

                        var idx = _inputDevices.FindIndex(d => d.Id == devId);
                        if (idx >= 0)
                        {
                            var existing = _inputDevices[idx];
                            // Merge inputs by input id
                            var inputs = new List<Input>(existing.Inputs);
                            foreach (var inUpd in update.Inputs)
                            {
                                var iidx = inputs.FindIndex(x => x.Id == inUpd.Id);
                                if (iidx >= 0)
                                {
                                    var old = inputs[iidx];
                                    inputs[iidx] = old with
                                    {
                                        Name = inUpd.Name ?? old.Name,
                                        IsMuted = inUpd.IsMuted ?? old.IsMuted,
                                        Gain = inUpd.Gain ?? old.Gain,
                                        MicPcMix = inUpd.MicPcMix ?? old.MicPcMix,
                                        Effects = inUpd.Effects ?? old.Effects,
                                        ExtensionData = inUpd.ExtensionData ?? old.ExtensionData
                                    };
                                }
                                else inputs.Add(inUpd);
                            }
                            var merged = existing with { Inputs = inputs };
                            _inputDevices[idx] = merged;
                            InputDeviceChanged?.Invoke(this, merged);
                        }
                        else
                        {
                            _inputDevices.Add(update);
                            InputDeviceChanged?.Invoke(this, update);
                        }
                        break;
                    }
                case "outputDevicesChanged":
                    {
                        var parsed = p?.Deserialize<OutputDevicesResult>(_jsonOptions);
                        if (parsed is not null)
                        {
                            MainOutput = parsed.MainOutput;
                            _outputDevices = parsed.OutputDevices ?? new();
                            OutputDevicesChanged?.Invoke(this, (parsed.MainOutput, _outputDevices));
                        }
                        break;
                    }
                case "outputDeviceChanged":
                    {
                        if (p is null) break;
                        var update = p.Value.Deserialize<OutputDevice>(_jsonOptions);
                        if (update is null) break;

                        var idx = _outputDevices.FindIndex(d => d.Id == update.Id);
                        if (idx >= 0)
                        {
                            var existing = _outputDevices[idx];
                            var outputs = new List<Output>(existing.Outputs);
                            foreach (var oUpd in update.Outputs)
                            {
                                var oidx = outputs.FindIndex(x => x.Id == oUpd.Id);
                                if (oidx >= 0)
                                {
                                    var old = outputs[oidx];
                                    outputs[oidx] = old with
                                    {
                                        Name = oUpd.Name ?? old.Name,
                                        IsMuted = oUpd.IsMuted ?? old.IsMuted,
                                        Level = oUpd.Level ?? old.Level,
                                        ExtensionData = oUpd.ExtensionData ?? old.ExtensionData
                                    };
                                }
                                else outputs.Add(oUpd);
                            }
                            var merged = existing with { Outputs = outputs };
                            _outputDevices[idx] = merged;
                            OutputDeviceChanged?.Invoke(this, merged);
                        }
                        else
                        {
                            _outputDevices.Add(update);
                            OutputDeviceChanged?.Invoke(this, update);
                        }
                        break;
                    }
                case "channelsChanged":
                    {
                        var parsed = p?.Deserialize<ChannelsResult>(_jsonOptions);
                        if (parsed?.Channels is not null)
                        {
                            _channels = parsed.Channels;
                            ChannelsChanged?.Invoke(this, _channels);
                        }
                        break;
                    }
                case "channelChanged":
                    {
                        if (p is null) break;
                        var update = p.Value.Deserialize<Channel>(_jsonOptions);
                        if (update is null) break;

                        var idx = _channels.FindIndex(c => c.Id == update.Id);
                        if (idx >= 0)
                        {
                            var existing = _channels[idx];
                            var merged = existing with
                            {
                                Name = update.Name ?? existing.Name,
                                Type = update.Type ?? existing.Type,
                                Apps = update.Apps ?? existing.Apps,
                                Mixes = update.Mixes ?? existing.Mixes,
                                ExtensionData = update.ExtensionData ?? existing.ExtensionData
                            };
                            _channels[idx] = merged;
                            ChannelChanged?.Invoke(this, merged);
                        }
                        else
                        {
                            _channels.Add(update);
                            ChannelChanged?.Invoke(this, update);
                        }
                        break;
                    }
                case "mixesChanged":
                    {
                        var parsed = p?.Deserialize<MixesResult>(_jsonOptions);
                        if (parsed?.Mixes is not null)
                        {
                            _mixes = parsed.Mixes;
                            MixesChanged?.Invoke(this, _mixes);
                        }
                        break;
                    }
                case "mixChanged":
                    {
                        if (p is null) break;
                        var update = p.Value.Deserialize<Mix>(_jsonOptions);
                        if (update is null) break;

                        var idx = _mixes.FindIndex(m => m.Id == update.Id);
                        if (idx >= 0)
                        {
                            var existing = _mixes[idx];
                            var merged = existing with
                            {
                                Name = update.Name ?? existing.Name,
                                IsMuted = update.IsMuted ?? existing.IsMuted,
                                Level = update.Level ?? existing.Level,
                                ExtensionData = update.ExtensionData ?? existing.ExtensionData
                            };
                            _mixes[idx] = merged;
                            MixChanged?.Invoke(this, merged);
                        }
                        else
                        {
                            _mixes.Add(update);
                            MixChanged?.Invoke(this, update);
                        }
                        break;
                    }
                case "levelMeterChanged":
                    {
                        var parsed = p?.Deserialize<LevelMeterChangedParams>(_jsonOptions);
                        if (parsed is not null)
                        {
                            LevelMeters = parsed;
                            LevelMeterChanged?.Invoke(this, parsed);
                        }
                        break;
                    }
                case "createProfileRequested":
                    {
                        var parsed = p?.Deserialize<CreateProfileRequestedParams>(_jsonOptions);
                        if (parsed is not null)
                            CreateProfileRequested?.Invoke(this, parsed);
                        break;
                    }
                case "focusedAppChanged":
                    {
                        var parsed = p?.Deserialize<FocusedAppChangedParams>(_jsonOptions);
                        if (parsed is not null)
                        {
                            FocusedApp = parsed;
                            FocusedAppChanged?.Invoke(this, parsed);
                        }
                        break;
                    }
                default:
                    // Unknown notification; ignore
                    break;
            }
        }
        catch
        {
            // ignore notification errors
        }
    }
}
