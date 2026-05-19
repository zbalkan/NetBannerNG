# NetBannerNG

[![PR Tests](https://github.com/zbalkan/NetBannerNG/actions/workflows/tests.yml/badge.svg)](https://github.com/zbalkan/NetBannerNG/actions/workflows/tests.yml)
[![Dependabot Updates](https://github.com/zbalkan/NetBannerNG/actions/workflows/dependabot/dependabot-updates/badge.svg)](https://github.com/zbalkan/NetBannerNG/actions/workflows/dependabot/dependabot-updates)

NetBannerNG is a free, open-source Windows classification banner utility for managed enterprise endpoints.

It is designed for organizations that need persistent, policy-driven desktop marking without cloud portals, license servers, usage tracking, or feature gating.

## What users get

- Persistent top classification banner with optional screen-border marking
- Optional full-width bottom banner mode
- INFOCON / FPCON and caveat display support
- Multi-monitor behavior and fullscreen-aware operation
- Group Policy-first management with local fallback settings
- Service-supervised runtime for resilient operation

## Management model

NetBannerNG is intended to fit existing Windows administration workflows:

- Deploy with installer-based software distribution
- Configure with Group Policy
- Support standalone, domain-joined, disconnected, and air-gapped systems

Policy/configuration registry paths:

- Managed policy: `HKLM\SOFTWARE\Policies\NetBannerNG`
- Local settings fallback: `HKLM\SOFTWARE\NetBannerNG`

## Classification catalogs

Set `ClassificationSelection` to choose both catalog and level in the format `<Catalog> - <Classification>` (for example `NATO - COSMIC TOP SECRET`). Built-in catalog keys include:

- `NOT_CONFIGURED` (default when selection is missing/invalid)
- `NATO`
- `US`, `UK`, `CA`, `AU`, `DE`, `DK`, `EE`, `FI`, `FR`, `IT`, `PL`, `SE`, `TR`, `UA`
- `EUCI`, `EP`
- `AG`, `CCEB`, `COE`, `ESA`, `EURATOM`, `ICC`, `ICTY`, `NSG`, `OECD`, `OPCW`, `OSCE`, `UN`, `WASSENAAR`, `WTO`

Compatibility behavior:

- If `ClassificationSelection` is unset and the legacy `Classification` value is `1..4`, NetBannerNG maps the value to the `US` profile for compatibility.
- Otherwise, NetBannerNG uses the `NOT_CONFIGURED` default profile ("Classification not configured").

Catalog notes:

- `NATO` and `US` include full built-in color definitions; some other profiles include limited entry-specific colors (for example `AU` for `SECRET`).
- Most other profiles provide textual marking presets only.
- Organizations remain responsible for validating markings, colors, translations, caveats, and policy suitability against their own rules.

## Documentation

- `docs/ADMIN_OPERATIONS.md` — install, remove, upgrade, and rollback runbook
- `docs/DEVELOPMENT.md` — architecture, internals, build, and test guidance
- `docs/FEATURE_PARITY_MATRIX.md` — NetBannerNG vs NetBanner/SystemBanner capability matrix
- `SUPPORT.md` — supported platforms, versioning policy, response expectations, and EOL policy

## Security note

NetBannerNG is a display and awareness utility. It is not an access-control, authorization, information-flow-control, or data-loss-prevention boundary.

### Service/client pipe trust model (operations summary)

- The service endpoint uses a session-bound pipe name (`NetBannerNG.<SessionId>`) so only clients targeting the current interactive session name are considered.
- Pipe ACLs are built with the active interactive user SID and explicit deny rules for network principals.
- Client identity is verified against the active interactive user SID when connection metadata exposes SID details.
- Interactive username fallback is **permanently disabled** in Release builds. Available only in Debug builds via `NETBANNERNG_PIPE_IDENTITY_FALLBACK=1` for local troubleshooting.
- If fallback is used because SID/username metadata are unavailable on a transport, the service emits a dedicated high-signal event (`EventId 3018`) with connection type, pipe name, and reason.

## Source acknowledgements

The following references are cited in inline code comments and influenced parts of the implementation:

- https://erikengberg.com/named-pipes-in-net-6-with-tray-icon-and-service/
- https://github.com/PhilipRieck/WpfAppBar
- https://stackoverflow.com/questions/1109271/launching-gui-app-from-windows-service-window-does-not-appear/1109443
- https://bytes.com/topic/c-sharp/answers/463942-using-openprocesstoken
- https://fleexlab.blogspot.com/2015/04/remote-desktop-surprise.html
- http://csharptest.net/1043/how-to-prevent-users-from-killing-your-service-process/index.html
