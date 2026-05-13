# NetBannerNG Development Guide

Audience: developers and maintainers.

## Solution structure

- `src/NetBannerNG` — WPF desktop UI (`net481`) that renders banner/borders.
- `src/NetBannerNG.Service` — Windows Service (`net481`) that supervises UI lifecycle.
- `src/NetBannerNG.Common` — shared named-pipe contracts, appbar helpers, interop, and security/process helpers.
- `src/NetBannerNG.Tests` — unit/integration-focused tests for lifecycle, pipe security, settings, and monitor behavior.
- `installer/setup.iss` — installer and service lifecycle orchestration.
- `GPO/NetBannerNG.admx` + `GPO/en-*/NetBannerNG.adml` — policy templates.

## Runtime architecture

1. Service runs in Session 0 as watchdog.
2. Service identifies target interactive session and launches `NetBannerNG.exe` in user context.
3. Service/UI coordinate over named pipes (`NetBannerNG.Common.NamedPipes`).
4. UI builds per-monitor banner groups through `BorderManager` and applies policy-driven settings.
5. On disconnect/failure, service reconciles and relaunches with throttled retries/backoff.

## Settings and policy resolution

Registry paths:
- Managed policy: `HKLM\SOFTWARE\Policies\NetBannerNG`
- Local fallback: `HKLM\SOFTWARE\NetBannerNG`
- Legacy migration compatibility: `HKLM\SOFTWARE\Policies\Microsoft\NetBanner`

Behavior:
- Load local defaults, then overlay managed policy values when present.
- `ClassificationProfile` selects catalogs from `ClassificationCatalogs`.
- `NATO` and `US` have built-in color mappings.
- EU/country/international profiles are primarily textual presets; color should be set by policy.

## Build prerequisites

- Windows
- Visual Studio 2022 (recommended)
- .NET Framework 4.8.1 targeting pack

## Build and test

```bash
cd src
dotnet restore NetBannerNG.sln
dotnet build NetBannerNG.sln -c Release
dotnet test NetBannerNG.sln -c Release
```

## Service debug mode

`NetBannerNG.Service` supports `--debug` in Debug builds for interactive troubleshooting.

Example:

```bash
cd src/NetBannerNG.Service/bin/Debug/net481
NetBannerNG.Service.exe --debug
```

Release builds are service-context only.

## Lifecycle and IPC notes

Current accepted behavior:
- Service watchdog + user-session UI model is the production pattern.
- UI shuts down when service/pipe disconnects so service can re-provision.
- Relaunch logic includes retry/backoff and health counters.

Known design considerations:
- Session targeting must remain robust for console and RDP-heavy environments.
- Pipe authorization should remain least-privilege and continuously regression-tested.
- Single-endpoint pipe design is simple but constrains concurrent multi-session isolation.

## Engineering roadmap (high-value follow-ons)

1. Session selection hardening for multi-session/RDP-first estates (`WTSEnumerateSessions` policy-based selection).
2. Per-session pipe endpoint model (`netbannerng-pipe-s<SessionId>`) with SID-scoped ACLs.
3. Structured lifecycle state-machine logging (`NoUser`, `Launching`, `Running`, `Backoff`).
4. Expanded tests for session transitions and pipe identity/authorization rules.

## Installer/deployment implementation notes

Inno Setup currently:
- installs under `Program Files\NetBannerNG`
- registers service `NetBannerNGWatchdog`
- starts service on install
- stops/deletes service on uninstall

Development changes that affect lifecycle must be validated with installer-driven install/remove flows, not ad-hoc service creation scripts.
