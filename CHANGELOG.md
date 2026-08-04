# Changelog

All notable changes to this project are documented here. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
