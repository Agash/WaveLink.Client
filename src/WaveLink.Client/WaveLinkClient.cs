using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;

namespace WaveLink.Client;

/// <summary>
/// A client for the local Wave Link JSON-RPC WebSocket API.
/// Use this to connect to a running Wave Link instance and call its RPC methods or receive notifications.
/// </summary>
/// <remarks>
/// Create a new <see cref="WaveLinkClient"/> instance.
/// </remarks>
/// <param name="options">Optional client options.</param>
public sealed class WaveLinkClient(WaveLinkClientOptions? options = null) : IAsyncDisposable
{
    private readonly WaveLinkClientOptions _options = options ?? new WaveLinkClientOptions();
    private readonly ClientWebSocket _ws = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonRpcResponse>> _pending = new();

    private Task? _recvLoop;
    private int _nextId;

    // Cached state (optional but handy for consumers)
    /// <summary>Information about the connected Wave Link application or <c>null</c> if not connected.</summary>
    public ApplicationInfo? ApplicationInfo { get; private set; }

    /// <summary>Current cached list of input devices.</summary>
    public IReadOnlyList<InputDevice> InputDevices => _inputDevices;

    /// <summary>Current cached list of output devices.</summary>
    public IReadOnlyList<OutputDevice> OutputDevices => _outputDevices;

    /// <summary>Current cached selected main output, or <c>null</c>.</summary>
    public MainOutput? MainOutput { get; private set; }

    /// <summary>Current cached list of channels.</summary>
    public IReadOnlyList<Channel> Channels => _channels;

    /// <summary>Current cached list of mixes.</summary>
    public IReadOnlyList<Mix> Mixes => _mixes;

    /// <summary>Latest received level meter values, or <c>null</c>.</summary>
    public LevelMeterChangedParams? LevelMeters { get; private set; }

    /// <summary>Latest focused app information, or <c>null</c>.</summary>
    public FocusedAppChangedParams? FocusedApp { get; private set; }

    private List<InputDevice> _inputDevices = [];
    private List<OutputDevice> _outputDevices = [];
    private List<Channel> _channels = [];
    private List<Mix> _mixes = [];

    #region Standard Events

    /// <summary>Raised when the WebSocket connection is disconnected.</summary>
    public event EventHandler? Disconnected;

    /// <summary>Raised when the full input device list is updated.</summary>
    public event EventHandler<IReadOnlyList<InputDevice>>? InputDevicesChanged;

    /// <summary>Raised when a single input device is added or updated.</summary>
    public event EventHandler<InputDevice>? InputDeviceChanged;

    /// <summary>Raised when the main output or output devices list changes.</summary>
    public event EventHandler<(MainOutput mainOutput, IReadOnlyList<OutputDevice> outputDevices)>? OutputDevicesChanged;

    /// <summary>Raised when a single output device is added or updated.</summary>
    public event EventHandler<OutputDevice>? OutputDeviceChanged;

    /// <summary>Raised when the list of channels changes.</summary>
    public event EventHandler<IReadOnlyList<Channel>>? ChannelsChanged;

    /// <summary>Raised when a single channel is added or updated.</summary>
    public event EventHandler<Channel>? ChannelChanged;

    /// <summary>Raised when the list of mixes changes.</summary>
    public event EventHandler<IReadOnlyList<Mix>>? MixesChanged;

    /// <summary>Raised when a single mix is added or updated.</summary>
    public event EventHandler<Mix>? MixChanged;

    /// <summary>Raised when level meter values are received.</summary>
    public event EventHandler<LevelMeterChangedParams>? LevelMeterChanged;

    /// <summary>Raised when focused app information changes.</summary>
    public event EventHandler<FocusedAppChangedParams>? FocusedAppChanged;

    /// <summary>Raised when a create-profile request is received from the server.</summary>
    public event EventHandler<CreateProfileRequestedParams>? CreateProfileRequested;

    #endregion

    /// <summary>Connects to the local Wave Link WebSocket and performs an initial handshake.</summary>
    /// <param name="cancellationToken">Cancellation token to cancel the connect operation.</param>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_ws.State is WebSocketState.Open or WebSocketState.Connecting)
        {
            return;
        }

        int port = _options.PortOverride ?? await DiscoverPortAsync(cancellationToken).ConfigureAwait(false);
        Uri uri = new($"ws://127.0.0.1:{port}");

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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

        _recvLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token), CancellationToken.None);

        // Basic validation handshake
        ApplicationInfo info = await GetApplicationInfoAsync(cancellationToken).ConfigureAwait(false);
        ApplicationInfo = info;

        if (!string.Equals(info.AppId, "EWL", StringComparison.Ordinal))
        {
            throw new WaveLinkException($"Connected server returned unexpected appID '{info.AppId}'.");
        }

        if (info.InterfaceRevision < 1)
        {
            throw new WaveLinkException($"Connected server returned unsupported interfaceRevision {info.InterfaceRevision}.");
        }
    }

    /// <summary>Dispose the client and close the underlying WebSocket connection.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await _cts.CancelAsync().ConfigureAwait(false);
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

    #region Public RPC API (AOT Compatible)

    /// <summary>Get application information from the connected Wave Link instance.</summary>
    public Task<ApplicationInfo> GetApplicationInfoAsync(CancellationToken ct = default)
    {
        return CallAsync("getApplicationInfo", WaveLinkJsonContext.Default.ApplicationInfo, ct);
    }

    /// <summary>Send plugin information to the server.</summary>
    public Task SetPluginInfoAsync(SetPluginInfoParams info, CancellationToken ct = default)
    {
        return CallVoidAsync("setPluginInfo", info, WaveLinkJsonContext.Default.SetPluginInfoParams, ct);
    }

    /// <summary>Retrieve the list of input devices and update the cached state.</summary>
    public async Task<InputDevicesResult> GetInputDevicesAsync(CancellationToken ct = default)
    {
        InputDevicesResult result = await CallAsync("getInputDevices", WaveLinkJsonContext.Default.InputDevicesResult, ct).ConfigureAwait(false);
        _inputDevices = result.InputDevices ?? [];
        InputDevicesChanged?.Invoke(this, _inputDevices);
        return result;
    }

    /// <summary>Update an input device configuration.</summary>
    public Task SetInputDeviceAsync(SetInputDeviceParams p, CancellationToken ct = default)
    {
        return CallVoidAsync("setInputDevice", p, WaveLinkJsonContext.Default.SetInputDeviceParams, ct);
    }

    /// <summary>Retrieve the list of output devices and update the cached state.</summary>
    public async Task<OutputDevicesResult> GetOutputDevicesAsync(CancellationToken ct = default)
    {
        OutputDevicesResult result = await CallAsync("getOutputDevices", WaveLinkJsonContext.Default.OutputDevicesResult, ct).ConfigureAwait(false);
        MainOutput = result.MainOutput;
        _outputDevices = result.OutputDevices ?? [];
        OutputDevicesChanged?.Invoke(this, (result.MainOutput, _outputDevices));
        return result;
    }

    /// <summary>Update an output device configuration.</summary>
    public Task SetOutputDeviceAsync(SetOutputDeviceParams p, CancellationToken ct = default)
    {
        return CallVoidAsync("setOutputDevice", p, WaveLinkJsonContext.Default.SetOutputDeviceParams, ct);
    }

    /// <summary>Retrieve the list of channels and update the cached state.</summary>
    public async Task<ChannelsResult> GetChannelsAsync(CancellationToken ct = default)
    {
        ChannelsResult result = await CallAsync("getChannels", WaveLinkJsonContext.Default.ChannelsResult, ct).ConfigureAwait(false);
        _channels = result.Channels ?? [];
        ChannelsChanged?.Invoke(this, _channels);
        return result;
    }

    /// <summary>Update a channel configuration.</summary>
    public Task SetChannelAsync(SetChannelParams p, CancellationToken ct = default)
    {
        return CallVoidAsync("setChannel", p, WaveLinkJsonContext.Default.SetChannelParams, ct);
    }

    /// <summary>Add an app to a channel.</summary>
    public Task AddToChannelAsync(AddToChannelParams p, CancellationToken ct = default)
    {
        return CallVoidAsync("addToChannel", p, WaveLinkJsonContext.Default.AddToChannelParams, ct);
    }

    /// <summary>Retrieve the list of mixes and update the cached state.</summary>
    public async Task<MixesResult> GetMixesAsync(CancellationToken ct = default)
    {
        MixesResult result = await CallAsync("getMixes", WaveLinkJsonContext.Default.MixesResult, ct).ConfigureAwait(false);
        _mixes = result.Mixes ?? [];
        MixesChanged?.Invoke(this, _mixes);
        return result;
    }

    /// <summary>Update a mix configuration.</summary>
    public Task SetMixAsync(SetMixParams p, CancellationToken ct = default)
    {
        return CallVoidAsync("setMix", p, WaveLinkJsonContext.Default.SetMixParams, ct);
    }

    /// <summary>Subscribe or unsubscribe to server notifications.</summary>
    public Task<SetSubscriptionResult> SetSubscriptionAsync(SetSubscriptionParams p, CancellationToken ct = default)
    {
        return CallAsync("setSubscription", p, WaveLinkJsonContext.Default.SetSubscriptionParams, WaveLinkJsonContext.Default.SetSubscriptionResult, ct);
    }

    #endregion

    #region Streams (IAsyncEnumerable)

    /// <summary>Streams real-time updates for input devices.</summary>
    public async IAsyncEnumerable<InputDevice> StreamInputDeviceChangesAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        Channel<InputDevice> channel = System.Threading.Channels.Channel.CreateUnbounded<InputDevice>();
        void handler(object? _, InputDevice e)
        {
            _ = channel.Writer.TryWrite(e);
        }

        InputDeviceChanged += handler;
        try
        {
            await foreach (InputDevice? item in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return item;
            }
        }
        finally
        {
            InputDeviceChanged -= handler;
        }
    }

    /// <summary>Streams real-time updates for level meters.</summary>
    public async IAsyncEnumerable<LevelMeterChangedParams> StreamLevelMetersAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        Channel<LevelMeterChangedParams> channel = System.Threading.Channels.Channel.CreateUnbounded<LevelMeterChangedParams>();
        void handler(object? _, LevelMeterChangedParams e)
        {
            _ = channel.Writer.TryWrite(e);
        }

        LevelMeterChanged += handler;
        try
        {
            await foreach (LevelMeterChangedParams? item in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return item;
            }
        }
        finally
        {
            LevelMeterChanged -= handler;
        }
    }

    /// <summary>Streams real-time updates for focused app changes.</summary>
    public async IAsyncEnumerable<FocusedAppChangedParams> StreamFocusedAppChangesAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        Channel<FocusedAppChangedParams> channel = System.Threading.Channels.Channel.CreateUnbounded<FocusedAppChangedParams>();
        void handler(object? _, FocusedAppChangedParams e)
        {
            _ = channel.Writer.TryWrite(e);
        }

        FocusedAppChanged += handler;
        try
        {
            await foreach (FocusedAppChangedParams? item in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return item;
            }
        }
        finally
        {
            FocusedAppChanged -= handler;
        }
    }

    #endregion

    #region Convenience Helpers

    /// <summary>Convenience: set the level for a specific output on a device.</summary>
    public Task SetOutputLevelAsync(string outputDeviceId, string outputId, double level0to1, CancellationToken ct = default)
    {
        return SetOutputDeviceAsync(new SetOutputDeviceParams
        {
            OutputDevice = new OutputDeviceUpdate
            {
                Id = outputDeviceId,
                Outputs = [new() { Id = outputId, Level = Clamp01(level0to1) }]
            }
        }, ct);
    }

    /// <summary>Convenience: set mute state for a specific output on a device.</summary>
    public Task SetOutputMuteAsync(string outputDeviceId, string outputId, bool isMuted, CancellationToken ct = default)
    {
        return SetOutputDeviceAsync(new SetOutputDeviceParams
        {
            OutputDevice = new OutputDeviceUpdate
            {
                Id = outputDeviceId,
                Outputs = [new() { Id = outputId, IsMuted = isMuted }]
            }
        }, ct);
    }

    /// <summary>Convenience: set the main output.</summary>
    public Task SetMainOutputAsync(string outputDeviceId, string outputId, CancellationToken ct = default)
    {
        return SetOutputDeviceAsync(new SetOutputDeviceParams
        {
            MainOutput = new MainOutput { OutputDeviceId = outputDeviceId, OutputId = outputId }
        }, ct);
    }

    /// <summary>Convenience: set mute state for a specific input on a device.</summary>
    public Task SetInputMuteAsync(string inputDeviceId, string inputId, bool isMuted, CancellationToken ct = default)
    {
        return SetInputDeviceAsync(new SetInputDeviceParams
        {
            Id = inputDeviceId,
            Inputs = [new() { Id = inputId, IsMuted = isMuted }]
        }, ct);
    }

    /// <summary>Convenience: set normalized gain for a specific input.</summary>
    public Task SetInputGainNormalizedAsync(string inputDeviceId, string inputId, double value0to1, CancellationToken ct = default)
    {
        return SetInputDeviceAsync(new SetInputDeviceParams
        {
            Id = inputDeviceId,
            Inputs = [new() { Id = inputId, Gain = new GainValue { Value = Clamp01(value0to1) } }]
        }, ct);
    }

    /// <summary>Convenience: set normalized mic/PC mix for a specific input.</summary>
    public Task SetMicPcMixNormalizedAsync(string inputDeviceId, string inputId, double value0to1, CancellationToken ct = default)
    {
        return SetInputDeviceAsync(new SetInputDeviceParams
        {
            Id = inputDeviceId,
            Inputs = [new() { Id = inputId, MicPcMix = new MicPcMixValue { Value = Clamp01(value0to1) } }]
        }, ct);
    }

    /// <summary>Clamp a value to the range [0,1].</summary>
    /// <param name="value">Value to clamp.</param>
    /// <returns>Clamped value between 0 and 1 inclusive.</returns>
    public static double Clamp01(double value)
    {
        return value < 0 ? 0 : (value > 1 ? 1 : value);
    }

    #endregion

    #region Internals (AOT Safe)

    private async Task<int> DiscoverPortAsync(CancellationToken ct)
    {
        string? wsInfoPath = _options.WsInfoFilePathOverride ?? GetDefaultWsInfoFilePath();
        if (!string.IsNullOrWhiteSpace(wsInfoPath))
        {
            int? port = await TryReadPortFromWsInfoAsync(wsInfoPath, ct).ConfigureAwait(false);
            if (port is int p and > 0)
            {
                return p;
            }
        }

        for (int p = _options.MinPort; p <= _options.MaxPort; p++)
        {
            ct.ThrowIfCancellationRequested();
            if (await CanConnectAsync(p, ct).ConfigureAwait(false))
            {
                return p;
            }
        }

        throw new WaveLinkException($"Unable to discover Wave Link WebSocket port (ws-info.json missing and scan {_options.MinPort}-{_options.MaxPort} failed).");
    }

    private static string? GetDefaultWsInfoFilePath()
    {
        try
        {
            string? appData = Environment.GetEnvironmentVariable("APPDATA");
            if (string.IsNullOrWhiteSpace(appData))
            {
                return null;
            }

            if (appData.EndsWith("Roaming", StringComparison.OrdinalIgnoreCase))
            {
                string baseDir = appData[..^"Roaming".Length].TrimEnd('\\', '/');
                return Path.Combine(baseDir, "Local", "Packages", "Elgato.WaveLink_g54w8ztgkx496", "LocalState", "ws-info.json");
            }

            string? localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                return Path.Combine(localAppData, "Packages", "Elgato.WaveLink_g54w8ztgkx496", "LocalState", "ws-info.json");
            }
        }
        catch { /* ignored */ }
        return null;
    }

    private static async Task<int?> TryReadPortFromWsInfoAsync(string path, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using FileStream stream = File.OpenRead(path);
            using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("port", out JsonElement portEl) && portEl.ValueKind == JsonValueKind.Number && portEl.TryGetInt32(out int port))
            {
                return port;
            }
        }
        catch { /* ignored */ }
        return null;
    }

    private async Task<bool> CanConnectAsync(int port, CancellationToken ct)
    {
        using ClientWebSocket ws = new();
        ws.Options.SetRequestHeader("Origin", _options.OriginHeader);

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
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

    // AOT-safe typed calls
    private async Task<TResponse> CallAsync<TResponse>(string method, JsonTypeInfo<TResponse> responseTypeInfo, CancellationToken ct)
    {
        JsonElement? resultEl = await CallCoreAsync<object>(method, null, null, ct).ConfigureAwait(false);
        return resultEl is null
            ? throw new WaveLinkException($"RPC '{method}' returned null result.")
            : resultEl.Value.Deserialize(responseTypeInfo) ?? throw new WaveLinkException($"RPC '{method}' result could not be deserialized.");
    }

    private async Task<TResponse> CallAsync<TParams, TResponse>(string method, TParams p, JsonTypeInfo<TParams> paramsTypeInfo, JsonTypeInfo<TResponse> responseTypeInfo, CancellationToken ct)
    {
        JsonElement? resultEl = await CallCoreAsync(method, p, paramsTypeInfo, ct).ConfigureAwait(false);
        return resultEl is null
            ? throw new WaveLinkException($"RPC '{method}' returned null result.")
            : resultEl.Value.Deserialize(responseTypeInfo) ?? throw new WaveLinkException($"RPC '{method}' result could not be deserialized.");
    }

    private Task CallVoidAsync<TParams>(string method, TParams p, JsonTypeInfo<TParams> paramsTypeInfo, CancellationToken ct)
    {
        return CallCoreAsync(method, p, paramsTypeInfo, ct);
    }

    private async Task<JsonElement?> CallCoreAsync<TParams>(string method, TParams? paramsObject, JsonTypeInfo<TParams>? paramsTypeInfo, CancellationToken ct)
    {
        if (_ws.State != WebSocketState.Open)
        {
            throw new WaveLinkException("WebSocket is not connected.");
        }

        int id = Interlocked.Increment(ref _nextId);
        TaskCompletionSource<JsonRpcResponse> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, tcs))
        {
            throw new WaveLinkException("Failed to register pending RPC.");
        }

        JsonElement? paramsEl = null;
        if (paramsObject is not null && paramsTypeInfo is not null)
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(paramsObject, paramsTypeInfo);
            using JsonDocument doc = JsonDocument.Parse(bytes);
            paramsEl = doc.RootElement.Clone();
        }

        JsonRpcRequest req = new() { Id = id, Method = method, Params = paramsEl };
        string payload = JsonSerializer.Serialize(req, WaveLinkJsonContext.Default.JsonRpcRequest);

        await SendTextAsync(payload, ct).ConfigureAwait(false);

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_options.RequestTimeout);

        try
        {
            await using CancellationTokenRegistration _ = timeoutCts.Token.Register(() => tcs.TrySetCanceled(timeoutCts.Token));
            JsonRpcResponse resp = await tcs.Task.ConfigureAwait(false);

            if (resp.Error is not null)
            {
                object? data = resp.Error.Data;
                throw new WaveLinkRpcException(resp.Error.Code, resp.Error.Message, data);
            }

            return resp.Result;
        }
        finally
        {
            _ = _pending.TryRemove(id, out _);
        }
    }

    private async Task SendTextAsync(string text, CancellationToken ct)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken: ct).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            StringBuilder sb = new(64 * 1024);

            while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                _ = sb.Clear();
                WebSocketReceiveResult? result;

                do
                {
                    result = await _ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    _ = sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                string message = sb.ToString();
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
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("id", out JsonElement idEl) && idEl.ValueKind == JsonValueKind.Number && idEl.TryGetInt32(out int id))
            {
                JsonRpcResponse? resp = root.Deserialize(WaveLinkJsonContext.Default.JsonRpcResponse);
                if (resp is not null && _pending.TryGetValue(id, out TaskCompletionSource<JsonRpcResponse>? tcs))
                {
                    _ = tcs.TrySetResult(resp);
                }

                return;
            }

            if (root.TryGetProperty("method", out JsonElement methodEl) && methodEl.ValueKind == JsonValueKind.String)
            {
                string method = methodEl.GetString() ?? string.Empty;
                JsonElement? p = root.TryGetProperty("params", out JsonElement paramsEl) ? paramsEl.Clone() : null;
                DispatchNotification(method, p);
            }
        }
        catch
        {
            // Ignore malformed messages
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
                        InputDevicesResult? parsed = p?.Deserialize(WaveLinkJsonContext.Default.InputDevicesResult);
                        if (parsed?.InputDevices is not null)
                        {
                            _inputDevices = parsed.InputDevices;
                            InputDevicesChanged?.Invoke(this, _inputDevices);
                        }
                        break;
                    }
                case "inputDeviceChanged":
                    {
                        if (p is null || !p.Value.TryGetProperty("id", out JsonElement idEl) || idEl.ValueKind != JsonValueKind.String)
                        {
                            break;
                        }

                        string devId = idEl.GetString()!;
                        InputDevice? update = p.Value.Deserialize(WaveLinkJsonContext.Default.InputDevice);
                        if (update is null)
                        {
                            break;
                        }

                        int idx = _inputDevices.FindIndex(d => d.Id == devId);
                        if (idx >= 0)
                        {
                            InputDevice existing = _inputDevices[idx];
                            List<Input> inputs = [.. existing.Inputs];
                            foreach (Input inUpd in update.Inputs)
                            {
                                int iidx = inputs.FindIndex(x => x.Id == inUpd.Id);
                                if (iidx >= 0)
                                {
                                    Input old = inputs[iidx];
                                    inputs[iidx] = old with
                                    {
                                        Name = inUpd.Name ?? old.Name,
                                        IsMuted = inUpd.IsMuted ?? old.IsMuted,
                                        IsGainLockOn = inUpd.IsGainLockOn ?? old.IsGainLockOn,
                                        Gain = inUpd.Gain ?? old.Gain,
                                        MicPcMix = inUpd.MicPcMix ?? old.MicPcMix,
                                        Effects = inUpd.Effects ?? old.Effects,
                                        DspEffects = inUpd.DspEffects ?? old.DspEffects,
                                        ExtensionData = inUpd.ExtensionData ?? old.ExtensionData
                                    };
                                }
                                else
                                {
                                    inputs.Add(inUpd);
                                }
                            }
                            InputDevice merged = existing with { Inputs = inputs };
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
                        OutputDevicesResult? parsed = p?.Deserialize(WaveLinkJsonContext.Default.OutputDevicesResult);
                        if (parsed is not null)
                        {
                            MainOutput = parsed.MainOutput;
                            _outputDevices = parsed.OutputDevices ?? [];
                            OutputDevicesChanged?.Invoke(this, (parsed.MainOutput, _outputDevices));
                        }
                        break;
                    }
                case "outputDeviceChanged":
                    {
                        if (p is null)
                        {
                            break;
                        }

                        OutputDevice? update = p.Value.Deserialize(WaveLinkJsonContext.Default.OutputDevice);
                        if (update is null)
                        {
                            break;
                        }

                        int idx = _outputDevices.FindIndex(d => d.Id == update.Id);
                        if (idx >= 0)
                        {
                            OutputDevice existing = _outputDevices[idx];
                            List<Output> outputs = [.. existing.Outputs];
                            foreach (Output oUpd in update.Outputs)
                            {
                                int oidx = outputs.FindIndex(x => x.Id == oUpd.Id);
                                if (oidx >= 0)
                                {
                                    Output old = outputs[oidx];
                                    outputs[oidx] = old with
                                    {
                                        Name = oUpd.Name ?? old.Name,
                                        IsMuted = oUpd.IsMuted ?? old.IsMuted,
                                        Level = oUpd.Level ?? old.Level,
                                        ExtensionData = oUpd.ExtensionData ?? old.ExtensionData
                                    };
                                }
                                else
                                {
                                    outputs.Add(oUpd);
                                }
                            }
                            OutputDevice merged = existing with { Outputs = outputs };
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
                        ChannelsResult? parsed = p?.Deserialize(WaveLinkJsonContext.Default.ChannelsResult);
                        if (parsed?.Channels is not null)
                        {
                            _channels = parsed.Channels;
                            ChannelsChanged?.Invoke(this, _channels);
                        }
                        break;
                    }
                case "channelChanged":
                    {
                        if (p is null)
                        {
                            break;
                        }

                        Channel? update = p.Value.Deserialize(WaveLinkJsonContext.Default.Channel);
                        if (update is null)
                        {
                            break;
                        }

                        int idx = _channels.FindIndex(c => c.Id == update.Id);
                        if (idx >= 0)
                        {
                            Channel existing = _channels[idx];
                            Channel merged = existing with
                            {
                                Name = update.Name ?? existing.Name,
                                Type = update.Type ?? existing.Type,
                                IsMuted = update.IsMuted ?? existing.IsMuted,
                                Level = update.Level ?? existing.Level,
                                Image = update.Image ?? existing.Image,
                                Apps = update.Apps ?? existing.Apps,
                                Mixes = update.Mixes ?? existing.Mixes,
                                Effects = update.Effects ?? existing.Effects,
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
                        MixesResult? parsed = p?.Deserialize(WaveLinkJsonContext.Default.MixesResult);
                        if (parsed?.Mixes is not null)
                        {
                            _mixes = parsed.Mixes;
                            MixesChanged?.Invoke(this, _mixes);
                        }
                        break;
                    }
                case "mixChanged":
                    {
                        if (p is null)
                        {
                            break;
                        }

                        Mix? update = p.Value.Deserialize(WaveLinkJsonContext.Default.Mix);
                        if (update is null)
                        {
                            break;
                        }

                        int idx = _mixes.FindIndex(m => m.Id == update.Id);
                        if (idx >= 0)
                        {
                            Mix existing = _mixes[idx];
                            Mix merged = existing with
                            {
                                Name = update.Name ?? existing.Name,
                                IsMuted = update.IsMuted ?? existing.IsMuted,
                                Level = update.Level ?? existing.Level,
                                Image = update.Image ?? existing.Image,
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
                        LevelMeterChangedParams? parsed = p?.Deserialize(WaveLinkJsonContext.Default.LevelMeterChangedParams);
                        if (parsed is not null)
                        {
                            LevelMeters = parsed;
                            LevelMeterChanged?.Invoke(this, parsed);
                        }
                        break;
                    }
                case "createProfileRequested":
                    {
                        CreateProfileRequestedParams? parsed = p?.Deserialize(WaveLinkJsonContext.Default.CreateProfileRequestedParams);
                        if (parsed is not null)
                        {
                            CreateProfileRequested?.Invoke(this, parsed);
                        }

                        break;
                    }
                case "focusedAppChanged":
                    {
                        FocusedAppChangedParams? parsed = p?.Deserialize(WaveLinkJsonContext.Default.FocusedAppChangedParams);
                        if (parsed is not null)
                        {
                            FocusedApp = parsed;
                            FocusedAppChanged?.Invoke(this, parsed);
                        }
                        break;
                    }
            }
        }
        catch
        {
            // ignore notification errors safely
        }
    }
    #endregion
}