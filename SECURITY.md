# Security Policy

## Reporting a vulnerability

Please do **not** open a public issue for security problems. Use GitHub's
[private vulnerability reporting](https://github.com/AndreikaKopeika/fusion-dedicated/security/advisories/new)
to reach the maintainer directly, with:

- what an attacker could do, step by step
- the affected version (commit or tag)
- any log excerpts that show the problem

You will get a response within a few days, and a fix or a mitigation plan
before anything is disclosed publicly.

## Known, documented limitations

These are **by design** and not vulnerabilities, but they are worth knowing
before running a server:

- **The web panel has no authentication.** Anyone who can reach the dashboard
  port (8778 by default) can kick, ban, change the map, wipe the world and
  restart the server. The default listens on loopback only; reach it through
  an SSH tunnel. Setting `DashboardHost` to `"+"` exposes an unauthenticated
  admin interface on every network interface — only do this on a network you
  control.
- **The relay trusts its clients the way any host does.** Permission checks
  cover moderation commands, but the server does not simulate the world, so it
  cannot validate physics or gameplay claims made by clients.
- **`server.json` holds other people's SteamIDs** (bans, ranks, the mod
  catalogue). Treat it as sensitive.

## Supported versions

Only the latest tagged release and `main` receive security fixes.
