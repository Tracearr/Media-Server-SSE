# Changelog

All notable changes to this project are documented here. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Security

- The events endpoint now requires an administrator or an API key on both Jellyfin and Emby.

## [0.5.0] - 2026-09-08

### Changed

- Jellyfin builds now target .NET 10 against the Jellyfin 12.0 plugin ABI. Jellyfin 12 moved the server to .NET 10 and changed several plugin interfaces, so a 10.11 build does not load on it. Catalog entries carry `targetAbi` 12.0.0.0, which hides them from 10.11 servers; 0.4.1 stays the last release for 10.11.
- Emby is unaffected. `Emby.Plugin.Sse` is still netstandard2.0 and still targets Emby 4.9+.

## [0.4.1] - 2026-08-08

### Changed

- Emby dashboard thumbnail is the 16:9 catalog tile instead of the square Tracearr logo.
- Catalog art replaced: a 1920x1080 tile shared by the Jellyfin catalog and the Emby thumb, and an Emby terminal preview showing the wire format. The old images were Tracearr app placeholders.
- Both catalog manifests describe the full event set and carry per-version release notes; new releases pull them from this changelog.

## [0.4.0] - 2026-08-07

### Added

- `server.stats` event broadcast every 6 seconds while at least one client is connected, carrying `at` (unix seconds), `hostCpuUtilization`, `processCpuUtilization`, `hostMemoryUtilization`, and `processMemoryUtilization`. Host values read `/proc` and are omitted off Linux; inside a container `/proc` reports the host, matching what Plex's statistics endpoints show. The 6-second cadence matches Plex's resolution so dashboards can chart all server types together.
- `task.started`, `task.progress`, and `task.completed` events from the server's scheduled task manager. Payloads carry `taskId`, `taskName`, and `taskCategory` on start, `progress` (0-100) during execution, and `state` (the completion status) on finish. Progress is throttled to one event per task per percent moved or per 2 seconds, whichever comes first.

## [0.3.0] - 2026-08-04

### Added

- `library.item.added` and `library.item.removed` events, fired when an item finishes being added to or is removed from a library. Payload carries `itemId`, `itemType`, and `parentId` (the immediate parent container, e.g. a season or library folder). Ids match the server's REST API: GUID form on Jellyfin, numeric internal id on Emby. Theme media and virtual/placeholder items are filtered out, same as playback events.

## [0.2.0] - 2026-07-09

### Added

- `hello` event sent as the first frame on every SSE connection, carrying the plugin version and server type. Tracearr uses it to show the installed version and nudge when a newer release exists.

### Changed

- A subscriber whose event buffer fills up is now disconnected so it can reconnect and resync. Previously overflow events were dropped silently while the connection stayed open.
- Per-subscriber event buffer raised from 100 to 512.
- Local dev builds report version 0.0.0.0 so they always read as older than any release.

### Fixed

- Released builds now advertise their real version. The assembly version derives from the release tag instead of a pinned default.
- A client that disconnects during the hello write no longer leaks its subscription (Jellyfin).

## [0.1.0] - 2026-05-07

### Added

- Initial release of Jellyfin and Emby SSE plugins.
- Self-hosted Jellyfin manifest at `manifest.json` for the plugin catalog UI.
- Manual-install zip for Emby (Emby has no user-pasteable catalog URL).
- Six event types: `playing`, `progress`, `paused`, `stopped`, `session.start`, `session.end`, plus `ping` keepalive every 30 seconds.
- Bounded per-subscriber channels (capacity 100); overflow drops silently.
