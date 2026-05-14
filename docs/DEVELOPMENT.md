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

Current phase assessment (as of 2026-05-14): **Phase 6 hardening in progress; redesign is near-complete but not fully complete against the target architecture.**

### Why the previous DoD was insufficient
The earlier DoD validated important progress (catalog extraction, abstraction-first lifecycle wiring, suppression DTOs, layout tests), but it did **not** fully encode Section 4 target-architecture constraints:
- `DisplayOverlayOrchestrator` still has concrete window knowledge via nested `MonitorSurfaceSet` and explicit `Banner/BottomBanner/BottomBar/LeftBar/RightBar` construction.
- The orchestrator path still depends on static compatibility entry points as first-class runtime behavior.
- Layout and surface composition responsibilities are improved but not yet fully separated into a monitor-set contract that is independent from orchestrator internals.

### Redefined DoD (aligned to Section 4 target architecture)
The redesign is complete only when **all** are true:
1. **Orchestrator purity:** runtime orchestration is instance-based and coordinates lifecycle only (init/refresh/shutdown/suppression apply), with no concrete window type construction or nested monitor-set implementation.
2. **Catalog boundary:** `MonitorSurfaceCatalog` is standalone, owns monitor-id → monitor-set mapping, and exposes reconciliation through `IMonitorSurfaceCatalog` without references to static orchestrator helpers.
3. **Per-monitor set abstraction:** monitor-scoped aggregate is represented by an explicit `IMonitorSurfaceSet` contract in its own module, including show/sync/suppression/close responsibilities and health policy hooks.
4. **Suppression declaration:** fullscreen suppression flows are produced declaratively as typed payloads and applied by orchestrator only (no suppression recomputation inside orchestrator).
5. **Layout centralization:** monitor-relative geometry decisions live in `IMonitorLayoutPolicy` implementations; surface windows execute render/dock but do not own cross-monitor geometry policy.
6. **Static isolation:** static classes are adapter/facade only and are not required by core runtime coordination paths.
7. **Terminology consistency:** public core orchestration APIs consistently use overlay/surface vocabulary (no remaining border-manager-era names in active core API surfaces).
8. **Test coverage:** dedicated tests exist for catalog reconciliation, monitor-set orchestration behavior, suppression state application, and layout policy invariants.

### Updated phase mapping
- **Phase 0–2:** Completed (baseline characterization, naming/contracts, first-class catalog extraction).
- **Phase 3:** Completed. Core runtime wiring is instance-based (`DisplayOverlayOrchestratorRuntime`), while static orchestration entry points are now limited to compatibility facades/adapters.
- **Phase 4:** Completed for typed suppression payloads and declarative apply flow.
- **Phase 5:** Substantially completed for layout-policy invariants; additional enforcement is still required to remove residual geometry policy leakage in surface creation paths.
- **Phase 6:** In progress (telemetry/hardening continues).

### Next phase execution plan (starting now)
To proceed safely from current state toward the redefined DoD:
1. Add monitor-set focused tests that validate orchestration-facing behavior (`TryShowWindow`, suppression state transitions, and close behavior) without static orchestration dependencies.
2. Continue hardening GroupHealthPolicy behavior under repetitive window failures and recovery windows.
3. Preserve and verify startup rendering performance telemetry (`First Banner shown`) across refresh/reconcile transitions.
4. Expand structured reconcile/suppression logs with test assertions where practical.

### Practical status conclusion
- We are **after Phase 5 and within Phase 6 hardening**, with remaining work centered on hardening/observability test depth rather than major architectural boundary extraction.
- Therefore, redesign completion should be tracked against the redefined DoD above rather than the earlier milestone language.
