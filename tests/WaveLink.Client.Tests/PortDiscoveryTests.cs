using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WaveLink.Client.Tests;

/// <summary>
/// Discovery is how the client finds Wave Link without being told a port: read the port out of
/// ws-info.json, and fall back to probing a small range. Both paths are reached only through
/// <see cref="WaveLinkClient.ConnectAsync"/>, so these drive it from there.
/// </summary>
[TestClass]
public sealed class PortDiscoveryTests
{
    private static int IdOf(System.Text.Json.JsonElement request) => request.GetProperty("id").GetInt32();

    private string WriteWsInfo(string contents)
    {
        string path = Path.Combine(TestContext.TestRunResultsDirectory ?? Path.GetTempPath(), $"ws-info-{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    [TestMethod]
    public async Task ConnectAsync_WhenWsInfoNamesThePort_UsesIt()
    {
        await using FakeWaveLinkServer server = FakeWaveLinkServer.Start(
            request => FakeWaveLinkServer.ApplicationInfoReply(IdOf(request)));

        // The scan range is set past the server's port, so connecting can only succeed by way of
        // the file. Otherwise this test would pass even if the file were ignored entirely.
        await using WaveLinkClient client = new(new WaveLinkClientOptions
        {
            WsInfoFilePathOverride = WriteWsInfo($$$"""{"port":{{{server.Port}}}}"""),
            MinPort = 1,
            MaxPort = 1,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            RequestTimeout = TimeSpan.FromSeconds(10),
        });

        await client.ConnectAsync(TestContext.CancellationTokenSource.Token);

        Assert.IsNotNull(client.ApplicationInfo);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenWsInfoIsUnusable_FallsBackToScanning()
    {
        await using FakeWaveLinkServer server = FakeWaveLinkServer.Start(
            request => FakeWaveLinkServer.ApplicationInfoReply(IdOf(request)));

        await using WaveLinkClient client = new(new WaveLinkClientOptions
        {
            // Wave Link leaves this file behind after it exits, so a stale or truncated one is the
            // normal case rather than an exotic one. It must not stop discovery.
            WsInfoFilePathOverride = WriteWsInfo("{ this is not valid json"),
            MinPort = server.Port,
            MaxPort = server.Port,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            RequestTimeout = TimeSpan.FromSeconds(10),
        });

        await client.ConnectAsync(TestContext.CancellationTokenSource.Token);

        Assert.IsNotNull(client.ApplicationInfo);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenWsInfoIsMissing_FallsBackToScanning()
    {
        await using FakeWaveLinkServer server = FakeWaveLinkServer.Start(
            request => FakeWaveLinkServer.ApplicationInfoReply(IdOf(request)));

        await using WaveLinkClient client = new(new WaveLinkClientOptions
        {
            WsInfoFilePathOverride = Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.json"),
            MinPort = server.Port,
            MaxPort = server.Port,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            RequestTimeout = TimeSpan.FromSeconds(10),
        });

        await client.ConnectAsync(TestContext.CancellationTokenSource.Token);

        Assert.IsNotNull(client.ApplicationInfo);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenNothingAnswersInTheRange_ReportsTheRangeItTried()
    {
        // Bind a port and immediately release it, so the range is real but certainly empty.
        await using FakeWaveLinkServer server = FakeWaveLinkServer.Start(
            request => FakeWaveLinkServer.ApplicationInfoReply(IdOf(request)));
        int emptyPort = server.Port;
        await server.DisposeAsync();

        await using WaveLinkClient client = new(new WaveLinkClientOptions
        {
            WsInfoFilePathOverride = Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.json"),
            MinPort = emptyPort,
            MaxPort = emptyPort,
        });

        WaveLinkException ex = await Assert.ThrowsAsync<WaveLinkException>(
            () => client.ConnectAsync(TestContext.CancellationTokenSource.Token));

        Assert.IsTrue(ex.Message.Contains($"{emptyPort}-{emptyPort}", StringComparison.Ordinal), ex.Message);
    }

    [TestMethod]
    public void Options_ByDefault_ScanTheRangeWaveLinkActuallyUses()
    {
        // These defaults are the documented Wave Link 3.x range; changing them silently would make
        // discovery fail on a machine that never configured anything.
        WaveLinkClientOptions options = new();

        Assert.AreEqual(1884, options.MinPort);
        Assert.AreEqual(1893, options.MaxPort);
        Assert.AreEqual("streamdeck://", options.OriginHeader);
    }

    /// <summary>Supplied by MSTest; used for the per-test cancellation token.</summary>
    public TestContext TestContext { get; set; } = null!;
}
