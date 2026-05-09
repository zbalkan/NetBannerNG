# NetBannerNG Architecture

This document is for developers and system administrators.

## Solution structure

- `src/NetBannerNG`  
  WPF desktop app (`net481`) that renders the banner and optional borders.

- `src/NetBannerNG.Service`  
  Windows Service (`net481`) that hosts service-side control logic. Interactive hosting is only available in Debug builds for local debugging.

- `src/NetBannerNG.Common`  
  Shared primitives: appbar helpers, native interop, named pipe contracts, process/security helpers.

- `GPO/NetBanner.admx`  
  NetBanner-compatible policy definition template.

- `installer/setup.iss`  
  Inno Setup installer definition.

## Runtime model

At a high level:

1. Policy/config values are read from registry.
2. Service/app coordinate process lifecycle and communication.
3. WPF UI project renders top banner + optional borders across monitors.
4. Banner updates on heartbeat/refresh intervals and setting changes.

## Configuration and policy

### Group Policy path (legacy-compatible)

- `HKLM\Software\Policies\Microsoft\NetBanner`

Supported policy areas in the supplied ADMX include:

- Classification
- Custom colors/text
- INFOCON
- FPCON
- Caveats

### Local NetBannerNG settings path

- `HKLM\SOFTWARE\NetBannerNG`

Defaults currently used by app settings loader:

- `Classification = Public`
- `BannerColor = Green`
- `FontColor = White`
- `FontSize = 9`
- `BannerSize = 20`
- `Heartbeat = 20`
- `DisableBorders = false`

## Build prerequisites

- Windows
- .NET Framework 4.8.1 targeting pack / toolchain
- Visual Studio 2022 (recommended)

## Build

```bash
cd src
dotnet restore NetBannerNG.sln
dotnet build NetBannerNG.sln -c Release
```

## Service debug mode (Debug builds only)

`NetBannerNG.Service` validates startup args and currently allows:

- `--debug` (accepted only in Debug builds)

Interactive debug run example:

```bash
cd src/NetBannerNG.Service/bin/Debug/net481
NetBannerNG.Service.exe --debug
```

In interactive debug mode, unhandled exception details are dumped to `%TEMP%`.

In Release builds, interactive hosting is disabled and the executable only runs in Windows Service context.

## Installer behavior

`installer/setup.iss` currently:

- Installs files under `Program Files\NetBannerNG`
- Creates shortcuts
- Registers service name `netbannerng`
- Starts service on install
- Stops/deletes service on uninstall

## Operational notes

- HKLM writes require elevation.
- Deploy GPO settings before broad rollout to avoid inconsistent endpoint display.
- Validate on multi-monitor systems if border rendering is enabled.

## Codebase analysis summary

After reviewing the service, desktop app, shared library, and tests, the runtime model is:

1. **Service watchdog and session awareness**
   - `NetBannerNG.Service` tracks the active interactive session and recreates its named-pipe server when sessions change.
   - A watchdog loop relaunches the UI child process with throttling + exponential backoff and health counters.

2. **IPC boundary**
   - Service and UI communicate through session-scoped named pipes from `NetBannerNG.Common.NamedPipes`.
   - Validation/sanitization helpers and authorization-focused tests are present to harden pipe usage.

3. **UI lifecycle and single-instance behavior**
   - `App.xaml.cs` enforces single-instance startup, resolves optional `--pipe=` args, and performs graceful shutdown with dispatcher-safe sequencing.

4. **Banner/border rendering model**
   - `BorderManager` creates per-monitor border groups (top banner + optional bottom/left/right bars), keeps them synced with monitor changes, and applies group health policies to avoid thrashing on repeated failures.

5. **Policy-first settings resolution**
   - `Settings` loads local defaults from `HKLM\SOFTWARE\NetBannerNG`, then overlays managed policy values from `HKLM\SOFTWARE\Policies\Microsoft\NetBanner` when present.
   - Classification text composition includes optional custom display text, INFOCON/FPCON/CPCON values, and caveats.

6. **Test coverage direction**
   - The test project emphasizes watchdog transitions, named-pipe identity/security behavior, policy resolution, and monitor/border behaviors.

This confirms NetBannerNG is architected as a service-supervised, per-user UI renderer with policy-compatible configuration and explicit resilience mechanisms for long-running endpoint operation.
