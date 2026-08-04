# Tracearr SSE

Jellyfin and Emby plugins that expose a Server-Sent Events endpoint for real-time playback and session notifications. Built for [Tracearr](https://github.com/connorgallopo/Tracearr). Works with anything that consumes SSE.

## Why

Neither Jellyfin nor Emby has a built-in way to subscribe to playback events over a persistent HTTP connection. The webhook plugins push to external URLs, which isn't what you want when your client wants to hold a stream open. This plugin gives you a standard SSE endpoint that fires events when playback starts, stops, pauses, progresses, when sessions connect or disconnect, or when items are added to or removed from a library.

## Install

### Jellyfin

1. Open Jellyfin → **Dashboard → Plugins → Repositories**.
2. Add a repository:
   - **Name:** `Tracearr`
   - **URL:** `https://raw.githubusercontent.com/Tracearr/Media-Server-SSE/main/manifest.json`
3. Open the **Catalog**, find **Tracearr SSE**, install.
4. Restart Jellyfin.

Manual install (if you don't want to add the repository): download `Tracearr.Sse.Jellyfin_<version>.zip` from [Releases](https://github.com/Tracearr/Media-Server-SSE/releases), extract `Jellyfin.Plugin.Sse.dll` and `MediaServer.Sse.Core.dll` into your Jellyfin data directory at `plugins/Tracearr SSE/`. Restart.

### Emby

Emby has no equivalent of Jellyfin's user-pasteable plugin repository URL — install is manual.

1. Download `Tracearr.Sse.Emby_<version>.zip` from [Releases](https://github.com/Tracearr/Media-Server-SSE/releases).
2. Extract `Emby.Plugin.Sse.dll` into Emby's `programdata/plugins/` directory.
3. Restart Emby.

Updates: repeat the same steps with the new release zip.

## Usage

Connect to the SSE endpoint with any client that can set custom headers.

Jellyfin:
```bash
curl -N -H 'Authorization: MediaBrowser Token="YOUR_API_KEY"' \
  http://your-jellyfin:8096/api/sse/events
```

Emby:
```bash
curl -N -H 'X-Emby-Token: YOUR_API_KEY' \
  http://your-emby:8096/emby/sse/events
```

(The exact Emby path is verified during release; see CHANGELOG for any path corrections.)

## Events

| Event | Fields | When |
|---|---|---|
| `playing` | sessionId, itemId, userId, state, positionTicks | Playback started |
| `progress` | sessionId, itemId, userId, state, positionTicks | Playback position update |
| `paused` | sessionId, itemId, userId, state, positionTicks | Playback paused |
| `stopped` | sessionId, itemId, userId, state, positionTicks, playedToCompletion | Playback stopped |
| `session.start` | sessionId, userId | Device session connected |
| `session.end` | sessionId, userId | Device session disconnected |
| `library.item.added` | itemId, itemType, parentId | Item finished being added to a library |
| `library.item.removed` | itemId, itemType, parentId | Item removed from a library |
| `ping` | (empty) | Keepalive every 30 seconds |

### Wire format

```
event: playing
data: {"sessionId":"abc123","itemId":"def456","userId":"user1","state":"playing","positionTicks":0}

event: stopped
data: {"sessionId":"abc123","itemId":"def456","userId":"user1","state":"stopped","positionTicks":50000000,"playedToCompletion":true}

event: library.item.added
data: {"itemId":"def456","itemType":"Movie","parentId":"lib789"}

event: ping
data: {}
```

`sessionId` is the device session ID (matches what Jellyfin/Emby return from `/Sessions`), not the per-playback `PlaySessionId`. Events broadcast to all connected clients — there's no per-connection filtering. Null fields are omitted.

### Behavior notes

- Bounded channel per subscriber (capacity 100). If a client falls behind, events drop silently. Reconnect and poll `/Sessions` to catch up.
- Progress events pass through at whatever rate the media server reports them (typically every 5–10 seconds). No server-side throttling.
- Theme music and local trailer playback events are filtered out.
- Library events fire once per changed item, no batching. Theme media and virtual/placeholder items (e.g. missing episodes) are filtered out, same as playback events. `parentId` is the item's immediate parent (a season for an episode, a library folder for a top-level series or movie), not necessarily the library root.
- Emby also raises events for container folders (a new movie's directory arrives as `itemType: "Folder"` alongside the movie), and deleting a directory fires a removed event for the folder only, not each child. Jellyfin reports each media item individually in both directions. Don't assume added/removed pairs match one-to-one on Emby; treat removals as a cue to re-query.

## Verifying releases

Each release has SHA-256 checksums and a build attestation.

```bash
# Plain checksum verification
sha256sum -c SHA256SUMS

# Build attestation (proves the zip came from this repo's CI)
gh attestation verify Tracearr.Sse.Jellyfin_0.1.0.zip --owner Tracearr
```

## Build from source

Requires [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```bash
git clone https://github.com/Tracearr/Media-Server-SSE.git
cd Media-Server-SSE

# Jellyfin
dotnet publish Jellyfin.Plugin.Sse --configuration Release --output bin/jellyfin

# Emby
dotnet publish Emby.Plugin.Sse --configuration Release --output bin/emby
```

Run tests:
```bash
dotnet test
```

## Architecture

Three projects, two distribution shapes:

- `MediaServer.Sse.Core` — platform-agnostic event model and broadcaster. Uses `System.Threading.Channels` for fan-out.
- `Jellyfin.Plugin.Sse` — five `IEventConsumer<T>` implementations for playback/session events, a hosted service that subscribes directly to `ILibraryManager`'s `ItemAdded`/`ItemRemoved` events, and an ASP.NET Core controller for the SSE endpoint. Ships as `Jellyfin.Plugin.Sse.dll` + `MediaServer.Sse.Core.dll`.
- `Emby.Plugin.Sse` — single `IServerEntryPoint` that subscribes to `ISessionManager` and `ILibraryManager` events + an `IService` + `IAsyncStreamWriter` endpoint. Ships as a single `Emby.Plugin.Sse.dll`; Core sources are inlined at compile time because Emby's plugin loader only resolves a single DLL per plugin.

## License

[GPL-3.0-or-later](LICENSE)

## TODO

- Replace placeholder icons in `assets/` with SSE-specific art (currently uses Tracearr web app icon as placeholder).
