# NetBannerNG Development Guide

Audience: contributors and maintainers.

This guide is intended to explain the current architecture and help contributors set up and work in the development environment.

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
4. UI builds per-monitor banner groups through `DisplayOverlayOrchestrator` and applies policy-driven settings.
5. On disconnect/failure, service reconciles and relaunches with throttled retries/backoff.

## Settings and policy resolution

Registry paths:
- Managed policy: `HKLM\SOFTWARE\Policies\NetBannerNG`

Behavior:
- Seed missing managed values with build-defined defaults (Not Configured baseline), then read managed policy values.
- No local settings fallback path is used.
- `ClassificationSelection` selects catalog/classification values in the format `<Catalog> - <Classification>`.
- Default profile is `NOT_CONFIGURED`.
- EU/country/international profiles are primarily textual presets; color should be set by policy.

## Build prerequisites

- Windows 10 or 11
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
- On service/pipe disconnect, UI currently records a debug trace only (`NamedPipeClient.OnDisconnected`); relaunch/recovery remains service-driven.
- Relaunch logic includes retry/backoff and health counters.

Known design considerations:
- Session targeting must remain robust for console and RDP-heavy environments.
- Pipe authorization should remain least-privilege and continuously regression-tested.
- Single-endpoint pipe design is simple but constrains concurrent multi-session isolation.

## Installer/deployment implementation notes

Inno Setup currently:
- installs under `Program Files\NetBannerNG`
- registers service `NetBannerNGWatchdog`
- starts service on install
- stops/deletes service on uninstall

Development changes that affect lifecycle must be validated with installer-driven install/remove flows, not ad-hoc service creation scripts.
