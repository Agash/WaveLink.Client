# Wave Link Local WebSocket Protocol (Wave Link 3.x)

This document describes the **local WebSocket JSON-RPC protocol** used by **Elgato Wave Link** and exercised by the **official Stream Deck “Wave Link 3” plugin** (newer than the Beta 4 TypeScript library you provided).

> Notes  
> - Transport is **local-only** (`127.0.0.1`).  
> - Payloads are **JSON-RPC 2.0**.  
> - This protocol is not an official public spec from Elgato; it is based on observable behavior and the plugin’s request/notification shapes.

---

## 1. Transport

### 1.1 WebSocket URL
`ws://127.0.0.1:<port>`

### 1.2 Port discovery
Wave Link exposes a **single port** written to a JSON file.

The official Stream Deck plugin attempts, in order:

1. Read `ws-info.json` and use the `port` value.
2. If the file cannot be read / parsed, scan the fallback port range **1884–1893**.

#### Windows ws-info.json path (from official plugin)
The plugin derives the file path from `%APPDATA%` by replacing the `Roaming` segment:

`%APPDATA%` → `...\AppData\Roaming`  
becomes  
`...\AppData\Local\Packages\Elgato.WaveLink_g54w8ztgkx496\LocalState\ws-info.json`

So the resulting Windows path is typically:

`%LOCALAPPDATA%\Packages\Elgato.WaveLink_g54w8ztgkx496\LocalState\ws-info.json`

#### ws-info.json format
```json
{ "port": 1884 }
```

### 1.3 Handshake headers
The official plugin sets:

- `Origin: streamdeck://`

Your client should set this header for maximum compatibility.

---

## 2. Message Format: JSON-RPC 2.0

All frames are UTF-8 JSON.

### 2.1 Request (client → server)
```json
{
  "id": 1,
  "jsonrpc": "2.0",
  "method": "getApplicationInfo",
  "params": null
}
```

Notes:
- `id` is an integer chosen by the client.
- `jsonrpc` must be `"2.0"`.
- `params` is commonly `null` for no parameters (the TS lib uses `null`; the official plugin may omit params for some calls).

### 2.2 Response (server → client)
Success:
```json
{ "jsonrpc": "2.0", "id": 1, "result": { } }
```

Error:
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": { "code": -32601, "message": "Method not found" }
}
```

### 2.3 Notification (server → client)
```json
{
  "jsonrpc": "2.0",
  "method": "inputDevicesChanged",
  "params": { "inputDevices": [ /* ... */ ] }
}
```

Notifications have **no `id`**.

---

## 3. RPC Methods

Method names used by the official Stream Deck plugin:

| Method | Purpose |
|---|---|
| `getApplicationInfo` | Identify server + protocol revision |
| `setPluginInfo` | Inform Wave Link which Stream Deck device/plugin is connected (optional for 3rd-party clients) |
| `getInputDevices` | List input devices and their inputs |
| `setInputDevice` | Update input(s) on a device (mute/gain/mic-pc-mix/effects) |
| `getOutputDevices` | List output devices and main output selection |
| `setOutputDevice` | Update output device (mute/level) OR set main output |
| `getChannels` | List channels (including apps and per-mix data) |
| `setChannel` | Update channel properties (mute/level per mix and more) |
| `addToChannel` | Route a focused app into a channel |
| `getMixes` | List mixes |
| `setMix` | Update mix properties |
| `setSubscription` | Enable/disable notifications (e.g., focused app, level meters) |

---

## 4. Data Shapes

This section lists **minimum fields required** by the official plugin and the TS Beta 4 types, plus known optional fields.

### 4.1 ApplicationInfo (`getApplicationInfo`)
```json
{
  "appID": "EWL",
  "name": "Elgato Wave Link",
  "interfaceRevision": 1
}
```

- The official plugin checks `appID === "EWL"` and `interfaceRevision >= 1`.

### 4.2 Input devices (`getInputDevices`)
Result:
```json
{ "inputDevices": [ /* InputDevice[] */ ] }
```

InputDevice (common fields):
- `id: string`
- `name: string`
- `isWaveDevice: boolean` (present in TS types; observed by plugin)
- `inputs: Input[]`

Input (common fields):
- `id: string`
- `name: string`
- `isMuted?: boolean`
- `gain?: { "value": number, "max"?: number }`  
  - `value` is **normalized 0..1**
  - `max` is used by the plugin to map normalized value to dB (actual dB = value × max)
- `micPcMix?: { "value": number, "isInverted"?: boolean }` (normalized 0..1)
- `effects?: Effect[]` (from TS; official plugin uses effects in some actions)

Effect (TS Beta 4):
- `id: string`
- `name: string`
- `isEnabled: boolean`
- `isSupported: boolean`

### 4.3 Output devices (`getOutputDevices`)
Result:
```json
{
  "mainOutput": { "outputDeviceId": "...", "outputId": "..." },
  "outputDevices": [ /* OutputDevice[] */ ]
}
```

OutputDevice:
- `id: string`
- `name: string`
- `outputs: Output[]`

Output:
- `id: string`
- `name: string`
- `isMuted?: boolean`
- `level: number` (normalized 0..1)

### 4.4 Channels (`getChannels`)
Result:
```json
{ "channels": [ /* Channel[] */ ] }
```

Channel (common fields):
- `id: string`
- `name: string`
- `type: string` (e.g., `"Software"`)
- `apps: App[]`
- `mixes: ChannelMix[]`

App:
- `id: string`
- `name: string`

ChannelMix (TS Beta 4):
- `mixId: string`
- `isMuted: boolean`
- `level: number` (0..1)

### 4.5 Mixes (`getMixes`)
Result:
```json
{ "mixes": [ /* Mix[] */ ] }
```

Mix (TS Beta 4):
- `id: string`
- `name: string`
- `isMuted: boolean`
- `level: number`

---

## 5. Setter Params (Requests)

### 5.1 `setInputDevice`
```json
{
  "id": "<inputDeviceId>",
  "inputs": [
    {
      "id": "<inputId>",
      "isMuted": true,
      "gain": { "value": 0.42 },
      "micPcMix": { "value": 0.75 },
      "effects": [ { "id": "eq", "isEnabled": true } ]
    }
  ]
}
```

### 5.2 `setOutputDevice`
Two shapes are used:

**(A) Update outputs on a device**
```json
{
  "outputDevice": {
    "id": "<outputDeviceId>",
    "outputs": [
      { "id": "<outputId>", "isMuted": false, "level": 0.5 }
    ]
  }
}
```

**(B) Set main output**
```json
{
  "mainOutput": {
    "outputDeviceId": "<outputDeviceId>",
    "outputId": "<outputId>"
  }
}
```

### 5.3 `setChannel`
The protocol supports partial updates. A common shape (from TS Beta 4) is:
```json
{
  "id": "<channelId>",
  "mixes": [
    { "mixId": "<mixId>", "isMuted": false, "level": 0.9 }
  ]
}
```

### 5.4 `setMix`
Partial update example:
```json
{ "id": "<mixId>", "isMuted": false, "level": 0.85 }
```

### 5.5 `addToChannel`
```json
{ "appId": "<appId>", "channelId": "<channelId>" }
```

### 5.6 `setSubscription`
Enable/disable notifications:
```json
{
  "focusedAppChanged": { "isEnabled": true },
  "levelMeterChanged": { "isEnabled": true, "type": "channel", "id": "all" }
}
```

`levelMeterChanged` subscriptions are **filterable** by:
- `type`: `"input" | "output" | "channel" | "mix" | ""`
- `id`: specific id or `"all"`
- Some servers also return a `subId` to distinguish multiple subscriptions.

---

## 6. Notifications

Notifications observed in the official plugin:

| method | params |
|---|---|
| `createProfileRequested` | `{ deviceType: string, mixes: string[] }` |
| `inputDevicesChanged` | `{ inputDevices: InputDevice[] }` |
| `inputDeviceChanged` | `{ id: string, inputs: Input[] }` (partial updates) |
| `outputDevicesChanged` | `{ mainOutput: MainOutput, outputDevices: OutputDevice[] }` |
| `outputDeviceChanged` | `{ id: string, outputs: Output[] }` (partial updates) |
| `channelsChanged` | `{ channels: Channel[] }` |
| `channelChanged` | `{ id: string, ... }` (partial updates) |
| `mixesChanged` | `{ mixes: Mix[] }` |
| `mixChanged` | `{ id: string, ... }` |
| `levelMeterChanged` | `{ inputDevices: Meter[], outputDevices: Meter[], channels: Meter[], mixes: Meter[] }` |
| `focusedAppChanged` | `{ id: string, name: string, channel: { id: string } }` |

Meter entry:
- `id: string`
- `levelLeftPercentage: number`
- `levelRightPercentage: number`

---

## 7. Compatibility Notes: Official Plugin vs TS Beta 4

- **Source of truth:** This repo prefers behavior verified from the **official Stream Deck plugin**.
- The **TS Beta 4 library** is a helpful baseline for type shapes (effects, channel mixes), but may be missing:
  - `setPluginInfo` (present in official plugin)
  - newer/extra notification variants and fields
- Wave Link server may include additional fields not covered here; clients should ignore unknown fields.
