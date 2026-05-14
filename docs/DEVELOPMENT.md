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

## Overlay redesign status

Current phase assessment (as of 2026-05-14): **Phase 6 in progress**.

Completed:
- Phase 1: Naming/contracts for orchestration are in place (`DisplayOverlayOrchestrator`, `MonitorSurfaceSet`) and explicit service interfaces exist for orchestrator/suppression flows.
- Phase 2: `MonitorSurfaceCatalog` is extracted as a first-class component in its own module.
- Phase 3: core orchestration now runs through an instance-native runtime component (`DisplayOverlayOrchestratorRuntime`) behind adapter interfaces; static usage remains only as a compatibility adapter entrypoint.

Latest hardening in this change:
- Introduced explicit monitor abstraction contracts: `IMonitorIdentity`, `IMonitorLayoutPolicy`, and `IMonitorSurfaceCatalog`.
- Wired catalog reconciliation to consume `IMonitorIdentity` instead of direct static identity utility calls.
- Wired `DisplayOverlayOrchestrator` to consume monitor identity and monitor layout through provider abstractions, reducing direct static policy coupling in orchestration paths.
- Completed orchestrator-facing terminology migration from "Borders" to "Surfaces" in instance runtime APIs (`InitiateAllSurfaces`, `CloseAllSurfaces`) and lifecycle call sites.

Latest update (2026-05-14):
- Began Phase 6 hardening with structured orchestrator diagnostics:
  - Added reconcile telemetry events for initiate/refresh paths including monitor count, groups-to-show count, and catalog count.
  - Added suppression telemetry event with total update count and suppressed-group count.
  - Added shutdown telemetry event for close-all transitions.
- Added health-policy stress coverage with deterministic repeated disable/cooldown cycles in `GroupHealthPolicyTests`.

Latest update (2026-05-14):
- Continued Phase 6 hardening with additional structured orchestrator diagnostics:
  - Added explicit refresh skip telemetry when shutdown is already in progress.
  - Added shutdown-begin telemetry capturing catalog size and initiation state.
  - Extended suppression telemetry to include app-tagged suppression count for better diagnostics fidelity.
  - Added no-op telemetry for group-show pipeline when no groups are scheduled to render.

Remaining for Definition of Done:
- Add standalone unit coverage for catalog reconciliation/snapshot semantics.
- Complete migration away from static compatibility entrypoints (`DisplayOverlayOrchestrator`, static watcher implementations) by routing runtime coordination entirely through DI-managed instances.
- Complete Phase 6 hardening pass with broader structured logging coverage and health behavior validation under real orchestrator surface-failure scenarios.


Latest update (2026-05-14):
- Completed Phase 5 layout-policy consolidation checkpoints for geometry invariants:
  - Added targeted `MonitorLayoutPolicy` unit coverage for vertical top offset and vertical-height calculations, including minimum-height guard behavior on tiny monitor bounds.
  - Removed residual catalog dependency on `DisplayOverlayOrchestrator.BuildGroupId` by routing monitor identity creation through `IMonitorIdentity` inside `MonitorSurfaceCatalog`, tightening boundary ownership.
Latest update (2026-05-14):
- Completed Phase 4 contract hardening by making fullscreen suppression updates typed and metadata-carrying end-to-end:
  - Added `FullscreenSuppressionState` DTO (`IsSuppressed`, `AppName`).
  - Updated watcher/service/orchestrator interfaces to exchange `IReadOnlyDictionary<string, FullscreenSuppressionState>`.
  - Kept orchestration declarative: runtime applies suppression states without recomputing fullscreen ownership.

Latest update (2026-05-14):
- Continued Phase 3 static-pressure reduction by formalizing watcher terminology around topology concerns:
  - Renamed lifecycle watcher abstraction from `IMonitorWatcher` to `IMonitorTopologyWatcher`.
  - Renamed default adapter from `StaticMonitorWatcher` to `StaticMonitorTopologyWatcher`.
  - Updated lifecycle tests to consume the renamed topology watcher contract.

