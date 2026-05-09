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

- `src/setup.iss`  
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

`src/setup.iss` currently:

- Installs files under `Program Files\NetBannerNG`
- Creates shortcuts
- Registers service name `netbannerng`
- Starts service on install
- Stops/deletes service on uninstall

## Operational notes

- HKLM writes require elevation.
- Deploy GPO settings before broad rollout to avoid inconsistent endpoint display.
- Validate on multi-monitor systems if border rendering is enabled.
