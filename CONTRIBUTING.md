# Contributing

Thanks for helping make Fusion Dedicated better. This document covers what you
need to build, test and send a useful change.

## What the project needs most

- **Testing on other distributions.** Development happens on Arch. If install.sh
  needed a tweak for your distro, that PR is gold.
- **Mod-info brokering.** Implemented but never observed working end to end
  (see "What is tested" in the README). Reproduction logs would unlock it.
- **Dashboard polish.** The web panel ([Web/index.html](FusionDedicated/Web/index.html))
  is a single self-contained page with no build step — easy to improve.

## Building

Any machine with the .NET 9 SDK — Linux is not required for development:

```bash
dotnet build FusionDedicated.sln -c Release
dotnet test FusionDedicated.sln -c Release
```

The test suite covers the wire format, config persistence, permissions, the
spawn guard and ID allocation — all pure logic, no Steam or Linux needed. If
your change touches `Protocol/` or any of those classes, add or extend a test.

Running the actual server requires a Linux host with a signed-in Steam client
that owns SteamVR; see the README's install section for the full setup.

## Ground rules for changes

- **The wire format is sacred.** `Protocol/` mirrors Fusion's own
  serialization; a "tidier" encoding that diverges from LabFusion's behaviour
  breaks every client. When in doubt, read
  [LabFusion's source](https://github.com/Lakatrazz/BONELAB-Fusion).
- **The config file is load-bearing.** People hand-edit `server.json` and carry
  it across versions. Keep old files loading; migrate rather than rename.
- **No new dependencies without discussion** — the server currently needs only
  the .NET runtime and `libsteam_api.so`, and that is a feature.
- **Security-relevant behaviour changes** (permissions, bans, the panel) should
  say so explicitly in the PR description.

## Reporting a bug

Open a GitHub issue — the template asks for the right things, but the short
version is:

- the relevant section of `logs/server-YYYY-MM-DD.log`
- your Fusion version and the server's `VersionMajor`/`VersionMinor`
- your distribution, if it is an install problem
- whether players disconnected with `Closing Connection` (normal exit) or
  `Timeout; remote problem` (client stopped responding) — the distinction
  matters a great deal when diagnosing

## Pull requests

- Keep the diff focused; separate refactors from behaviour changes.
- `dotnet test` must pass — CI runs it on every PR.
- Match the existing code style (the repo has an `.editorconfig`); the
  codebase favours explaining *why* in comments and keeping *what* obvious.
- Update the README if you change anything operators can observe.
