using System;

namespace WaveLink.Client;

public class WaveLinkException : Exception
{
    public WaveLinkException(string message) : base(message) { }
    public WaveLinkException(string message, Exception inner) : base(message, inner) { }
}

public sealed class WaveLinkRpcException : WaveLinkException
{
    public int Code { get; }
    public object? Data { get; }

    public WaveLinkRpcException(int code, string message, object? data = null) : base(message)
    {
        Code = code;
        Data = data;
    }
}
