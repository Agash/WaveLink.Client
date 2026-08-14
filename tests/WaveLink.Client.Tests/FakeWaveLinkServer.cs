using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace WaveLink.Client.Tests;

/// <summary>
/// A loopback stand-in for Wave Link: a real WebSocket server speaking the same JSON-RPC dialect.
/// The client's interesting behaviour - port discovery, the handshake it rejects on, correlating a
/// response to the request that is waiting for it, turning an error object into an exception,
/// dispatching a notification to an event - only happens against a live socket, so a mock of the
/// client's own internals would prove nothing about any of it.
/// </summary>
internal sealed class FakeWaveLinkServer : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Func<JsonElement, string?> _respond;
    private readonly TaskCompletionSource _clientConnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private WebSocket? _socket;
    private Task? _acceptLoop;
    private bool _disposed;

    private FakeWaveLinkServer(int port, Func<JsonElement, string?> respond)
    {
        Port = port;
        _respond = respond;
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    /// <summary>The loopback port the server is listening on.</summary>
    public int Port { get; }

    /// <summary>Completes once a client has upgraded to a WebSocket.</summary>
    public Task ClientConnected => _clientConnected.Task;

    /// <summary>
    /// Starts a server on a free port. The port is chosen by binding, because the client discovers
    /// Wave Link by probing and a hard-coded port would make the suite order-dependent on any
    /// machine that happens to be using it.
    /// </summary>
    /// <param name="respond">
    /// Maps an incoming request to the raw JSON to send back, or null to send nothing at all -
    /// which is how a request timeout is exercised.
    /// </param>
    public static FakeWaveLinkServer Start(Func<JsonElement, string?> respond)
    {
        // Retry across ports: HttpListener cannot bind to port 0, and another process can take the
        // port between it being reported free and this binding it.
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int port = GetFreePort();
            FakeWaveLinkServer server = new(port, respond);
            try
            {
                server._listener.Start();
            }
            catch (HttpListenerException)
            {
                continue;
            }

            server._acceptLoop = Task.Run(server.AcceptAsync);
            return server;
        }

        throw new InvalidOperationException("Could not bind a loopback port for the fake server.");
    }

    /// <summary>Sends an unsolicited message, standing in for a server-side notification.</summary>
    public async Task PushAsync(string json)
    {
        await ClientConnected.ConfigureAwait(false);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        await _socket!.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, _cts.Token).ConfigureAwait(false);
    }

    private static int GetFreePort()
    {
        using System.Net.Sockets.TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext context = await _listener.GetContextAsync().ConfigureAwait(false);
                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
                _socket = wsContext.WebSocket;
                _ = _clientConnected.TrySetResult();
                await ServeAsync(wsContext.WebSocket).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or OperationCanceledException)
        {
            // Expected on shutdown: disposing the listener is how this loop is stopped.
        }
    }

    private async Task ServeAsync(WebSocket socket)
    {
        byte[] buffer = new byte[64 * 1024];
        while (socket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await socket.ReceiveAsync(buffer, _cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
            {
                return;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                // Complete the closing handshake. The client closes with no cancellation token, so
                // it waits on this acknowledgement indefinitely and a server that just walks away
                // hangs every test at disposal rather than failing one.
                try
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (WebSocketException)
                {
                    // The client is already gone; there is nobody left to acknowledge.
                }

                return;
            }

            using JsonDocument request = JsonDocument.Parse(buffer.AsMemory(0, result.Count));
            string? reply = _respond(request.RootElement);
            if (reply is null)
            {
                continue;
            }

            await socket.SendAsync(Encoding.UTF8.GetBytes(reply), WebSocketMessageType.Text, endOfMessage: true, _cts.Token)
                .ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Some tests stop the server mid-test to free its port, then dispose it again on the way
        // out of the using block. Disposing twice is their normal path, not a mistake.
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _cts.CancelAsync().ConfigureAwait(false);
        _listener.Close();

        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or HttpListenerException or ObjectDisposedException)
            {
                // Expected: the loop is torn down by closing the listener out from under it.
            }
        }

        _socket?.Dispose();
        _cts.Dispose();
    }

    /// <summary>
    /// The reply Wave Link gives to the handshake the client performs on connect. Anything else
    /// makes ConnectAsync throw, so most tests need this to succeed before reaching their subject.
    /// </summary>
    public static string ApplicationInfoReply(int id, string appId = "EWL", int interfaceRevision = 3) =>
        $$$"""{"jsonrpc":"2.0","id":{{{id}}},"result":{"appID":"{{{appId}}}","name":"Wave Link","interfaceRevision":{{{interfaceRevision}}}}}""";
}
