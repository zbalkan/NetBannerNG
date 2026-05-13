# NetBannerNG

[![PR Tests](https://github.com/zbalkan/NetBannerNG/actions/workflows/tests.yml/badge.svg)](https://github.com/zbalkan/NetBannerNG/actions/workflows/tests.yml)
[![Dependabot Updates](https://github.com/zbalkan/NetBannerNG/actions/workflows/dependabot/dependabot-updates/badge.svg)](https://github.com/zbalkan/NetBannerNG/actions/workflows/dependabot/dependabot-updates)

NetBannerNG is a free, open-source Windows classification banner utility for managed enterprise endpoints.

It is designed for organizations that need persistent, policy-driven desktop marking without cloud portals, license servers, or feature gating.

## What users get

- Persistent top classification banner with optional border rendering
- Optional full-width bottom banner mode
- INFOCON / FPCON / CPCON and caveat display support
- Multi-monitor and fullscreen-aware behavior
- Group Policy-first management model with local fallback settings
- Service-supervised runtime for resilient operation

## Management model

NetBannerNG is intended to fit existing Windows admin workflows:
- Deploy with installer-based software distribution
- Configure with ADMX/ADML-backed Group Policy
- Support standalone, domain-joined, disconnected, and air-gapped systems

Policy/config registry paths:
- Managed policy: `HKLM\SOFTWARE\Policies\NetBannerNG`
- Local settings fallback: `HKLM\SOFTWARE\NetBannerNG`

## Classification catalogs

Set `ClassificationProfile` to select a marking catalog. Built-in keys include:
- `NATO` (default), `US`, `UK`, `CA`, `AU`, `DE`, `DK`, `EE`, `EUCI`, `EP`, `FI`, `FR`, `IT`, `LT`, `LV`, `NO`, `NZ`, `PL`, `SE`, `FVEY`
- International/intergovernmental: `AG`, `CCEB`, `COE`, `ESA`, `EURATOM`, `ICC`, `ICTY`, `NSG`, `OECD`, `OPCW`, `OSCE`, `UN`, `WASSENAAR`, `WTO`

Notes:
- If `ClassificationProfile` is unset and legacy `Classification` is `1..4`, NetBannerNG maps to `US` for compatibility.
- Otherwise, default behavior is `NATO`.
- `NATO` and `US` include built-in colors; most other profiles are textual presets and should be paired with org-defined colors.

## Documentation

- `docs/ADMIN_OPERATIONS.md` — install/remove/upgrade/rollback runbook
- `docs/DEVELOPMENT.md` — architecture, internals, build/test guidance
- `docs/FEATURE_PARITY_MATRIX.md` — NetBannerNG vs NetBanner/SystemBanner capability matrix

## Security note

NetBannerNG is a display/awareness utility, not an access-control or data-loss-prevention boundary.
