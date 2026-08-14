using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WaveLink.Client.Tests;

/// <summary>
/// Exercises the client against <see cref="FakeWaveLinkServer"/> over a real loopback WebSocket.
/// </summary>
[TestClass]
public sealed class WaveLinkClientTests
{
    private static WaveLinkClientOptions OptionsFor(FakeWaveLinkServer server) => new()
    {
        PortOverride = server.Port,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        RequestTimeout = TimeSpan.FromSeconds(10),
    };

    private static string MethodOf(JsonElement request) =>
        request.TryGetProperty("method", out JsonElement method) ? method.GetString() ?? string.Empty : string.Empty;

    private static int IdOf(JsonElement request) => request.GetProperty("id").GetInt32();

    [TestMethod]
    public async Task ConnectAsync_WhenServerCompletesHandshake_ExposesApplicationInfo()
    {
        await using FakeWaveLinkServer server = FakeWaveLinkServer.Start(
            request => FakeWaveLinkServer.ApplicationInfoReply(IdOf(request)));
        await using WaveLinkClient client = new(OptionsFor(server));

        await client.ConnectAsync(TestContext.CancellationTokenSource.Token);

        Assert.IsNotNull(client.ApplicationInfo);
        Assert.AreEqual("EWL", client.ApplicationInfo.AppId);
        Assert.AreEqual(3, client.ApplicationInfo.InterfaceRevision);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenServerIsNotWaveLink_Fails()
    {
        // Something else on the port answering JSON-RPC must not be mistaken for Wave Link: the
        // client scans a port range, so it will meet unrelated servers.
        await using FakeWaveLinkServer server = FakeWaveLinkServer.Start(
            request => FakeWaveLinkServer.ApplicationInfoReply(IdOf(request), appId: "SOMETHING-ELSE"));
        await using WaveLinkClient client = new(OptionsFor(server));

        WaveLinkException ex = await Assert.ThrowsAsync<WaveLinkException>(
            () => client.ConnectAsync(TestContext.CancellationTokenSource.Token));

        Assert.IsTrue(ex.Message.Contains("SOMETHING-ELSE", StringComparison.Ordinal), ex.Message);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenInterfaceRevisionIsUnsupported_Fails()
    {
        await using FakeWaveLinkServer server = FakeWaveLinkServer.Start(
            request => FakeWaveLinkServer.ApplicationInfoReply(IdOf(request), interfaceRevision: 0));
        await using WaveLinkClient client = new(OptionsFor(server));

        WaveLinkException ex = await Assert.ThrowsAsync<WaveLinkException>(
            () => client.ConnectAsync(TestContext.CancellationTokenSource.Token));

        Assert.IsTrue(ex.Message.Contains("interfaceRevision", StringComparison.Ordinal), ex.Message);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenNothingIsListening_Fails()
    {
        await using FakeWaveLinkServer server = FakeWaveLinkServer.Start(
            request => FakeWaveLinkServer.ApplicationInfoReply(IdOf(request)));
        int deadPort = server.Port;
        await server.DisposeAsync();

        await using WaveLinkClient client = new(new WaveLinkClientOptions
        {
            PortOverride = deadPort,
            ConnectTimeout = TimeSpan.FromSeconds(2),
        });

        _ = await Assert.ThrowsAsync<WaveLinkException>(
            () => client.ConnectAsync(TestContext.CancellationTokenSource.Token));
    }

    [TestMethod]
    public async Task CallAsync_WhenServerRepliesOutOfOrder_MatchesEachResultToItsRequest()
    {
        // The client multiplexes over one socket and correlates by id. Answering the second request
        // first is the case a naive "next reply wins" implementation gets wrong.
        List<(int Id, string Method)> seen = [];
        TaskCompletionSource<string> deferred = new(TaskCreationOptions.RunContinuationsAsynchronously);

        await using FakeWaveLinkServer server = FakeWaveLinkServer.Start(request =>
        {
            int id = IdOf(request);
            string method = MethodOf(request);
            lock (seen)
            {
                seen.Add((id, method));
            }

            return method switch
            {
                "getApplicationInfo" => FakeWaveLinkServer.ApplicationInfoReply(id),
                // Hold the first list back until the second has been answered.
                "getInputDevices" => HandleInputDevices(id),
                _ => null,
            };
        });

        string HandleInputDevices(int id)
        {
            lock (seen)
            {
                int callNumber = seen.Count(entry => entry.Method == "getInputDevices");
                if (callNumber == 1)
                {
                    deferred.SetResult($$$"""
                        {"jsonrpc":"2.0","id":{{{id}}},"result":{"inputDevices":[{"id":"first","inputs":[]}]}}
                        """);
                    return string.Empty;
                }
            }

            return $$$"""{"jsonrpc":"2.0","id":{{{id}}},"result":{"inputDevices":[{"id":"second","inputs":[]}]}}""";
        }

        await using WaveLinkClient client = new(OptionsFor(server));
        await client.ConnectAsync(TestContext.CancellationTokenSource.Token);

        Task<InputDevicesResult> first = client.GetInputDevicesAsync(TestContext.CancellationTokenSource.Token);
        InputDevicesResult second = await client.GetInputDevicesAsync(TestContext.CancellationTokenSource.Token);

        Assert.AreEqual("second", second.InputDevices[0].Id);

        await server.PushAsync(await deferred.Task);
        InputDevicesResult firstResult = await first;
        Assert.AreEqual("first", firstResult.InputDevices[0].Id);
    }

    [TestMethod]
    public async Task CallAsync_WhenServerReturnsAnError_ThrowsWithCodeAndMessage()
    {
        await using FakeWaveLinkServer server = FakeWaveLinkServer.Start(request =>
        {
            int id = IdOf(request);
            return MethodOf(request) == "getApplicationInfo"
                ? FakeWaveLinkServer.ApplicationInfoReply(id)
                : $$$"""{"jsonrpc":"2.0","id":{{{id}}},"error":{"code":-32602,"message":"Invalid params"}}""";
        });

        await using WaveLinkClient client = new(OptionsFor(server));
        await client.ConnectAsync(TestContext.CancellationTokenSource.Token);

        WaveLinkRpcException ex = await Assert.ThrowsAsync<WaveLinkRpcException>(
            () => client.GetInputDevicesAsync(TestContext.CancellationTokenSource.Token));

        Assert.AreEqual(-32602, ex.Code);
        Assert.AreEqual("Invalid params", ex.Message);
    }

    [TestMethod]
    public async Task CallAsync_WhenServerNeverReplies_GivesUpAfterTheRequestTimeout()
    {
        await using FakeWaveLinkServer server = FakeWaveLinkServer.Start(request =>
            MethodOf(request) == "getApplicationInfo" ? FakeWaveLinkServer.ApplicationInfoReply(IdOf(request)) : null);

        await using WaveLinkClient client = new(new WaveLinkClientOptions
        {
            PortOverride = server.Port,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            RequestTimeout = TimeSpan.FromMilliseconds(250),
        });
        await client.ConnectAsync(TestContext.CancellationTokenSource.Token);

        // A silent server must not hang the caller forever; the per-call timeout is the only thing
        // standing between that and a deadlocked application.
        _ = await Assert.ThrowsAsync<TaskCanceledException>(
            () => client.GetInputDevicesAsync(TestContext.CancellationTokenSource.Token));
    }

    [TestMethod]
    public async Task CallAsync_BeforeConnect_Fails()
    {
        await using WaveLinkClient client = new(new WaveLinkClientOptions { PortOverride = 1 });

        WaveLinkException ex = await Assert.ThrowsAsync<WaveLinkException>(
            () => client.GetInputDevicesAsync(TestContext.CancellationTokenSource.Token));

        Assert.IsTrue(ex.Message.Contains("not connected", StringComparison.OrdinalIgnoreCase), ex.Message);
    }

    [TestMethod]
    public async Task Notification_WhenServerPushesLevelMeters_RaisesTheEvent()
    {
        await using FakeWaveLinkServer server = FakeWaveLinkServer.Start(
            request => FakeWaveLinkServer.ApplicationInfoReply(IdOf(request)));
        await using WaveLinkClient client = new(OptionsFor(server));

        TaskCompletionSource<LevelMeterChangedParams> raised = new(TaskCreationOptions.RunContinuationsAsynchronously);
        client.LevelMeterChanged += (_, e) => raised.TrySetResult(e);

        await client.ConnectAsync(TestContext.CancellationTokenSource.Token);
        await server.PushAsync("""
            {"jsonrpc":"2.0","method":"levelMeterChanged","params":{"inputDevices":[
              {"id":"in-1","levelLeftPercentage":42.5,"levelRightPercentage":43.5}]}}
            """);

        LevelMeterChangedParams received = await raised.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(received.InputDevices);
        Assert.AreEqual("in-1", received.InputDevices[0].Id);
        Assert.AreEqual(42.5, received.InputDevices[0].LevelLeftPercentage);
    }

    [TestMethod]
    public async Task Notification_WhenServerPushesInputDevices_UpdatesTheCachedState()
    {
        await using FakeWaveLinkServer server = FakeWaveLinkServer.Start(
            request => FakeWaveLinkServer.ApplicationInfoReply(IdOf(request)));
        await using WaveLinkClient client = new(OptionsFor(server));

        TaskCompletionSource<IReadOnlyList<InputDevice>> raised = new(TaskCreationOptions.RunContinuationsAsynchronously);
        client.InputDevicesChanged += (_, e) => raised.TrySetResult(e);

        await client.ConnectAsync(TestContext.CancellationTokenSource.Token);
        await server.PushAsync("""
            {"jsonrpc":"2.0","method":"inputDevicesChanged","params":{"inputDevices":[{"id":"dev-9","inputs":[]}]}}
            """);

        IReadOnlyList<InputDevice> devices = await raised.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationTokenSource.Token);
        Assert.AreEqual(1, devices.Count);
        Assert.AreEqual("dev-9", devices[0].Id);
    }

    [TestMethod]
    public async Task Notification_WhenMessageIsMalformed_DoesNotTearDownTheConnection()
    {
        await using FakeWaveLinkServer server = FakeWaveLinkServer.Start(
            request => FakeWaveLinkServer.ApplicationInfoReply(IdOf(request)));
        await using WaveLinkClient client = new(OptionsFor(server));

        await client.ConnectAsync(TestContext.CancellationTokenSource.Token);
        await server.PushAsync("this is not json");

        TaskCompletionSource<LevelMeterChangedParams> raised = new(TaskCreationOptions.RunContinuationsAsynchronously);
        client.LevelMeterChanged += (_, e) => raised.TrySetResult(e);

        // The receive loop has to survive garbage on the wire, or one bad frame silently ends every
        // subsequent notification for the life of the process.
        await server.PushAsync("""
            {"jsonrpc":"2.0","method":"levelMeterChanged","params":{"inputDevices":[{"id":"still-alive"}]}}
            """);

        LevelMeterChangedParams received = await raised.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(received.InputDevices);
        Assert.AreEqual("still-alive", received.InputDevices[0].Id);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenCalledTwice_IsANoOp()
    {
        await using FakeWaveLinkServer server = FakeWaveLinkServer.Start(
            request => FakeWaveLinkServer.ApplicationInfoReply(IdOf(request)));
        await using WaveLinkClient client = new(OptionsFor(server));

        await client.ConnectAsync(TestContext.CancellationTokenSource.Token);
        await client.ConnectAsync(TestContext.CancellationTokenSource.Token);

        Assert.IsNotNull(client.ApplicationInfo);
    }

    /// <summary>Supplied by MSTest; used for the per-test cancellation token.</summary>
    public TestContext TestContext { get; set; } = null!;
}
