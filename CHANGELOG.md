# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-17

First tagged release. A headless dedicated relay server for BONELAB Fusion:
signs in to Steam, publishes a lobby that stock Fusion clients find in their
server browser, and relays their traffic — with no game, no VR headset and no
host player on the machine.

### Added

- Steam lobby publishing over Steam Datagram Relay — no port forwarding, and
  unmodified Fusion clients cannot tell it apart from a hosted game.
- Web control panel (localhost by default): live log and metrics, moderation,
  map switching for 25 vanilla levels plus modded maps by barcode, persistent
  ranks and bans, and live-editable gameplay rules.
- Permission model mirroring Fusion's (`Guest/Default/Operator/Owner`), stored
  per SteamID, applied at join, and re-checked server-side for every moderation
  command.
- Spawn guard with graduated response — purge, then kick — sized for what
  *clients* survive rather than the server, because a relay never simulates.
- World hygiene: orphan culling, inheritance-aware cleanup of abandoned props,
  and eviction of the oldest abandoned props when the world is full, so spawns
  are refused silently never.
- Automatic recovery: the systemd stack rebuilds itself after a killed server
  process or a Steam client crash, usually in under a minute.
- Metrics history (minute rows in `logs/metrics.csv`) with graphs from ten
  minutes to a month.
- Installer for Arch, Debian/Ubuntu, Fedora/RHEL and openSUSE: detects the
  distro, builds, asks before installing any package, writes systemd user units
  and helper scripts — no root required beyond optional packages.
- `fusion-ctl.sh` for day-to-day management (`start`, `stop`, `logs`, `panel`,
  `update`), and `uninstall.sh` for a clean removal.
- Test suite covering the wire format, config persistence and migration,
  permissions, the spawn guard and player ID allocation, with CI on every push
  and tagged releases that publish a linux-x64 build.
