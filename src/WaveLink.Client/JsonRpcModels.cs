using System.Text.Json;
using System.Text.Json.Serialization;

namespace WaveLink.Client;

/// <summary>JSON-RPC request model.</summary>
public sealed record JsonRpcRequest
{
    /// <summary>The request ID used to correlate responses with requests.</summary>
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    /// <summary>The JSON-RPC protocol version.</summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    /// <summary>The name of the method to invoke.</summary>
    [JsonPropertyName("method")]
    public required string Method { get; init; }

    /// <summary>The parameters to pass to the method, or null for no parameters.</summary>
    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }
}

/// <summary>JSON-RPC error model.</summary>
public sealed record JsonRpcError
{
    /// <summary>The error code indicating the error type.</summary>
    [JsonPropertyName("code")]
    public required int Code { get; init; }

    /// <summary>The error message describing what went wrong.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>Optional additional error data provided by the server.</summary>
    [JsonPropertyName("data")]
    public JsonElement? Data { get; init; }
}

/// <summary>JSON-RPC response model.</summary>
public sealed record JsonRpcResponse
{
    /// <summary>The JSON-RPC protocol version.</summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    /// <summary>The request ID this response corresponds to.</summary>
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    /// <summary>The result from the method call, or null if an error occurred or the method returns no value.</summary>
    [JsonPropertyName("result")]
    public JsonElement? Result { get; init; }

    /// <summary>The error object if an error occurred, or null if the call succeeded.</summary>
    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; init; }
}

/// <summary>JSON-RPC notification model.</summary>
public sealed record JsonRpcNotification
{
    /// <summary>The JSON-RPC protocol version.</summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    /// <summary>The name of the method being called.</summary>
    [JsonPropertyName("method")]
    public required string Method { get; init; }

    /// <summary>The parameters for the method call, or null for no parameters.</summary>
    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }
}