using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WaveLink.Client.Tests;

/// <summary>
/// The library is built for Native AOT, so every type it puts on the wire has to be reachable
/// through the source-generated context rather than reflection. These tests go through the context
/// explicitly: a type missing from it still serializes fine in a normal test run and only fails once
/// trimmed, which is exactly the failure nobody notices until a published app breaks.
/// </summary>
[TestClass]
public sealed class WaveLinkJsonContextTests
{
    [TestMethod]
    public void Request_WhenSerialized_UsesProtocolPropertyNames()
    {
        JsonRpcRequest request = new() { Id = 7, Method = "getApplicationInfo" };

        string json = JsonSerializer.Serialize(request, WaveLinkJsonContext.Default.JsonRpcRequest);

        Assert.AreEqual("""{"id":7,"jsonrpc":"2.0","method":"getApplicationInfo"}""", json);
    }

    [TestMethod]
    public void Request_WhenParamsAreAbsent_OmitsTheProperty()
    {
        JsonRpcRequest request = new() { Id = 1, Method = "getInputDevices" };

        string json = JsonSerializer.Serialize(request, WaveLinkJsonContext.Default.JsonRpcRequest);

        // The context ignores nulls when writing. Wave Link rejects a params property that is
        // present but null, so this is protocol behaviour, not formatting preference.
        Assert.IsFalse(json.Contains("params", StringComparison.Ordinal), json);
    }

    [TestMethod]
    public void Response_WhenServerReturnsAnError_RoundTripsCodeAndMessage()
    {
        const string payload = """
            {"jsonrpc":"2.0","id":4,"error":{"code":-32601,"message":"Method not found"}}
            """;

        JsonRpcResponse? response = JsonSerializer.Deserialize(payload, WaveLinkJsonContext.Default.JsonRpcResponse);

        Assert.IsNotNull(response);
        Assert.AreEqual(4, response.Id);
        Assert.IsNull(response.Result);
        Assert.IsNotNull(response.Error);
        Assert.AreEqual(-32601, response.Error.Code);
        Assert.AreEqual("Method not found", response.Error.Message);
    }

    [TestMethod]
    public void Notification_WhenDeserialized_ExposesMethodAndRawParams()
    {
        const string payload = """
            {"jsonrpc":"2.0","method":"focusedAppChanged","params":{"appID":"obs64.exe"}}
            """;

        JsonRpcNotification? notification = JsonSerializer.Deserialize(payload, WaveLinkJsonContext.Default.JsonRpcNotification);

        Assert.IsNotNull(notification);
        Assert.AreEqual("focusedAppChanged", notification.Method);
        Assert.IsNotNull(notification.Params);
        Assert.AreEqual("obs64.exe", notification.Params.Value.GetProperty("appID").GetString());
    }

    [TestMethod]
    public void InputDevices_WhenDeserialized_MapsNestedInputsAndGain()
    {
        const string payload = """
            {"inputDevices":[{"id":"dev-1","name":"Wave XLR","isWaveDevice":true,
              "inputs":[{"id":"in-1","name":"Mic","isMuted":false,"gain":{"value":0.75}}]}]}
            """;

        InputDevicesResult? result = JsonSerializer.Deserialize(payload, WaveLinkJsonContext.Default.InputDevicesResult);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.InputDevices.Count);

        InputDevice device = result.InputDevices[0];
        Assert.AreEqual("dev-1", device.Id);
        Assert.AreEqual(true, device.IsWaveDevice);
        Assert.AreEqual(1, device.Inputs.Count);
        Assert.AreEqual("in-1", device.Inputs[0].Id);
    }

    [TestMethod]
    public void UnknownServerProperties_WhenDeserialized_AreKeptAsExtensionData()
    {
        // Wave Link adds properties between firmware revisions. They have to survive a round trip
        // rather than being dropped, so a caller can still read a field this version predates.
        const string payload = """
            {"appID":"wavelink","interfaceRevision":3,"someFutureField":42}
            """;

        ApplicationInfo? info = JsonSerializer.Deserialize(payload, WaveLinkJsonContext.Default.ApplicationInfo);

        Assert.IsNotNull(info);
        Assert.AreEqual("wavelink", info.AppId);
        Assert.AreEqual(3, info.InterfaceRevision);
        Assert.IsNotNull(info.ExtensionData);
        Assert.AreEqual(42, info.ExtensionData["someFutureField"].GetInt32());
    }

    [TestMethod]
    public void SetInputDeviceParams_WhenSerialized_OmitsUnchangedFields()
    {
        SetInputDeviceParams parameters = new()
        {
            Id = "dev-1",
            Inputs = [new SetInputParams { Id = "in-1", IsMuted = true }],
        };

        string json = JsonSerializer.Serialize(parameters, WaveLinkJsonContext.Default.SetInputDeviceParams);

        // Only the field being changed is sent. Sending nulls for the rest would ask the server to
        // reinterpret every other property of the input on each call.
        Assert.AreEqual("""{"id":"dev-1","inputs":[{"id":"in-1","isMuted":true}]}""", json);
    }

    [TestMethod]
    public void SubscriptionResult_WhenDeserialized_ReportsPerTopicAcknowledgement()
    {
        const string payload = """
            {"levelMeterChanged":{"isEnabled":true,"type":"input","id":"in-1"}}
            """;

        SetSubscriptionResult? result = JsonSerializer.Deserialize(payload, WaveLinkJsonContext.Default.SetSubscriptionResult);

        Assert.IsNotNull(result);
        Assert.IsNull(result.FocusedAppChanged);
        Assert.IsNotNull(result.LevelMeterChanged);
        Assert.AreEqual(true, result.LevelMeterChanged.IsEnabled);
        Assert.AreEqual("in-1", result.LevelMeterChanged.Id);
    }
}
