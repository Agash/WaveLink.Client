namespace WaveLink.Client;

/// <summary>Base exception type for WaveLink client errors.</summary>
public class WaveLinkException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="WaveLinkException"/> class with a message.</summary>
    /// <param name="message">The error message.</param>
    public WaveLinkException(string message) : base(message) { }

    /// <summary>Initializes a new instance of the <see cref="WaveLinkException"/> class with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="inner">The inner exception.</param>
    public WaveLinkException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Exception representing a JSON-RPC error returned by the server.</summary>
/// <remarks>Initializes a new instance of the <see cref="WaveLinkRpcException"/> class.</remarks>
/// <param name="code">The JSON-RPC error code.</param>
/// <param name="message">The error message.</param>
/// <param name="data">Optional error data payload from the server.</param>
public sealed class WaveLinkRpcException(int code, string message, object? data = null) : WaveLinkException(message)
{
    /// <summary>Gets the JSON-RPC error code.</summary>
    public int Code { get; } = code;

    /// <summary>Gets the JSON-RPC error data payload, or null if not present.</summary>
    public new object? Data { get; } = data;
}
