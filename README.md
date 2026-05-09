# NetBannerNG

NetBannerNG is an open-source alternative to Microsoft NetBanner for Windows endpoints.

It displays a persistent desktop classification banner and supports existing NetBanner Group Policy settings so organizations can migrate with minimal policy changes.

## Background (Microsoft NetBanner and USAF)

Microsoft NetBanner appears to have been distributed as a U.S. government/USAF-oriented desktop classification banner utility for Windows endpoints. Public traces include:

- Historic Microsoft Q&A references discussing NetBanner versioning/support.
- File metadata listings for `netbanner.exe` showing product attribution as **Microsoft NetBanner**.
- Community deployment artifacts (for example, infrastructure automation formulas) that reference installing Microsoft NetBanner in enterprise environments.

NetBannerNG is intended as an open-source, modernized replacement that preserves policy compatibility where practical while making behavior and operations transparent.

## Features

- NetBanner policy compatibility
- Classification banner display
- INFOCON display
- FPCON display
- Custom text/caveats support
- Optional border display around the desktop

## Who this is for

- Security teams that require endpoint classification markings
- Organizations currently using Microsoft NetBanner policies
- Administrators deploying banner settings through Group Policy

## Quick start

1. Install NetBannerNG on a Windows endpoint.
2. Deploy your NetBanner-compatible policies via Group Policy.
3. Confirm the NetBannerNG service is running.
4. Log in as a user and verify the banner appears with expected values.

## Solution structure

- `src/NetBannerNG`: WPF desktop UI application that renders the classification banner and optional borders.
- `src/NetBannerNG.Common`: Shared class library for interop, appbar helpers, IPC contracts, and utilities.
- `src/NetBannerNG.Service`: Executable service host for Windows Service runtime (interactive mode is Debug-build only with `--debug`).
- `src/NetBannerNG.Tests`: MSTest project for automated tests.

## Build from source

1. Open `src/NetBannerNG.sln` in Visual Studio 2022 (or newer).
2. Restore NuGet packages when prompted.
3. Build the `Release` configuration for `Any CPU`.
4. Locate binaries in `src/NetBannerNG/bin/Release/`.

## Behavior under fullscreen and mouse-over

The expected UX is based on current implementation behavior:

- **Normal windowed use:** banner/borders are top-most so classification markings remain visible.
- **Fullscreen foreground window detected:** NetBannerNG sends its owned banner windows behind other windows (`Topmost=false`) to reduce interference with fullscreen apps.
- **Leaving fullscreen:** NetBannerNG restores banner windows to top-most (`Topmost=true`).
- **Mouse-over behavior:** there is **no hover-to-fade or hover-opacity mode** in current code; pointer movement does not change banner opacity.

Notes:
- Fullscreen detection is monitor/window bounds based, so edge cases may occur with borderless-windowed games/apps.
- Multi-monitor behavior follows monitor events and refreshes border windows accordingly.

## Policy compatibility

NetBannerNG reads NetBanner-compatible policy values from:

- `HKLM\Software\Policies\Microsoft\NetBanner`

The repository includes policy templates in `GPO/`.

## Installation

An installer script is included at:

- `installer/setup.iss`

The installer places binaries under Program Files and installs/starts the NetBannerNG Windows service.

## Documentation

- **User/Operator overview:** this `README.md`
- **Architecture + internals:** `docs/ARCHITECTURE.md`
- **Admin operations (install/remove/upgrade/rollback):** `docs/ADMIN_OPERATIONS.md`
- **Feature parity matrix (NetBannerNG vs Microsoft NetBanner/SystemBanner):** `docs/FEATURE_PARITY_MATRIX.md`
- **Detailed comparative gap analysis:** `docs/GAP_ANALYSIS_SystemBanner_vs_NetBannerNG.md`

## Project status

- FPCON and INFOCON support added
- Original NetBanner GPO files added
