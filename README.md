# NetBannerNG
[![PR Tests](https://github.com/zbalkan/NetBannerNG/actions/workflows/tests.yml/badge.svg)](https://github.com/zbalkan/NetBannerNG/actions/workflows/tests.yml)
[![Dependabot Updates](https://github.com/zbalkan/NetBannerNG/actions/workflows/dependabot/dependabot-updates/badge.svg)](https://github.com/zbalkan/NetBannerNG/actions/workflows/dependabot/dependabot-updates)

NetBannerNG is a free, open-source Windows desktop classification banner utility.

It is built for organizations that want persistent, policy-driven desktop marking without license servers, web portals, online activation, or paid feature gates.

NetBannerNG is designed to fit into **existing Windows administration workflows**:
- install with a standard Windows installer
- configure locally for standalone systems
- manage centrally with Group Policy for domain environments
- operate in connected, disconnected, and air-gapped networks

---

## Why NetBannerNG exists

Many environments need classification/security banners, but do not want operational dependency on external management services or licensing infrastructure.

NetBannerNG focuses on practical endpoint operation:
- no license management
- no online activation
- no cloud/portal dependency
- no artificial feature tiers
- Group Policy-friendly configuration model

This project is not a replacement for a full centralized management platform. It is an endpoint utility that works with Windows-native management paths administrators already use.

---

## What it does

NetBannerNG provides persistent desktop marking with support for common operational requirements:
- classification banner display
- custom display text and caveats
- INFOCON / FPCON / CPCON metadata display
- configurable foreground/background colors
- configurable font size and banner size
- optional border display
- optional full-width **bottom banner** mode (or legacy bottom border when disabled)
- multi-monitor support
- service-backed runtime supervision

It is suitable for physical endpoints, VMs, VDI/AVD sessions, lab systems, and controlled enterprise Windows estates.

---

## Management model

In domain environments, Group Policy should be treated as the primary management interface.

### Classification catalog profiles

NetBannerNG supports profile-based classification catalogs via:
- `ClassificationProfile` policy value

Supported profile keys:
- `NATO` (default)
- `US`
- `UK`
- `CA`
- `AU`
- `DE`
- `DK`
- `EE`
- `EUCI`
- `EP` (European Parliament)
- `FI`
- `FR`
- `IT`
- `LT`
- `LV`
- `NO`
- `NZ`
- `PL`
- `SE`
- `FVEY`
- International/intergovernmental catalogs: `AG`, `CCEB`, `COE`, `ESA`, `EURATOM`, `ICC`, `ICTY`, `NSG`, `OECD`, `OPCW`, `OSCE`, `UN`, `WASSENAAR`, `WTO`

Backward compatibility behavior:
- If `ClassificationProfile` is not configured and legacy `Classification` is `1..4`, NetBannerNG auto-dispatches to the `US` catalog.
- Otherwise, default behavior is `NATO`.

Color handling rule:
- `NATO` and `US` profiles include built-in color mappings.
- Most country, EU, and organization profiles are treated as **textual marking presets**; colors remain a local policy decision unless explicitly set by administrators.

Policy/registry paths:
- Group Policy values are written/read at `HKLM\SOFTWARE\Policies\NetBannerNG`.
- Local fallback settings are stored at `HKLM\SOFTWARE\NetBannerNG`.

---

## Deployment model

NetBannerNG is packaged with an **Inno Setup** installer and is intended to be installed/uninstalled through the installer workflow.

Operationally, this supports:
- interactive manual installs
- software distribution tooling
- disconnected/offline media deployment
- golden image workflows (where applicable)

Because installer logic manages service lifecycle, documentation guidance is installer-first for install/remove/version-change operations.

---

## Architecture overview

NetBannerNG separates service supervision from user-session UI:
- **Service (`NetBannerNGWatchdog`)**: lifecycle/control in background
- **UI process (`NetBannerNG.exe`)**: renders banner in interactive desktop session

This follows normal Windows session boundaries (service in Session 0, UI in user session).

---

## Build from source

1. Open `src/NetBannerNG.sln` in Visual Studio 2022+.
2. Restore NuGet packages.
3. Build `Release | Any CPU`.
4. Outputs are under project `bin\Release\net481` paths.

---

## Policy templates

Group Policy templates are included under:
- `GPO/NetBannerNG.admx`
- `GPO/en-US/NetBannerNG.adml` (or `GPO/en-GB/NetBannerNG.adml`)

Import into your Group Policy Central Store to manage NetBannerNG settings through GPMC.

---

## Documentation

- `docs/ADMIN_OPERATIONS.md` — install/remove/version-change/runbook guidance
- `docs/ARCHITECTURE.md` — internal architecture and process model
- `docs/FEATURE_PARITY_MATRIX.md` — capability comparison matrix

---

## Security and compliance note

NetBannerNG is a **display/awareness utility**, not an access control or data-loss-prevention boundary.

Organizations are responsible for validating behavior against their own baselines (STIG/SCAP/internal policy/regulatory requirements).

---

## Support and project status

NetBannerNG is a FOSS project under active development.

- Report bugs/features through GitHub Issues.
- Review release notes/tags before production rollout.
- Validate behavior in pilot rings before broad deployment.
