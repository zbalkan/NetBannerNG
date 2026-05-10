# Feature Parity Matrix

Audience: migration planners, security engineering leads, endpoint platform owners.

This matrix helps evaluate NetBannerNG relative to Microsoft NetBanner and SystemBanner.

## Legend
- ✅: Supported
- ⚠️: Partial / implementation-dependent
- ❌: Not currently supported
- ℹ️: Unknown from available public evidence

## Capability matrix

| Capability | NetBannerNG | Microsoft NetBanner | SystemBanner |
|---|---:|---:|---:|
| Group Policy-based configuration | ✅ | ✅ | ✅ |
| Policy path compatibility `HKLM\Software\Policies\Microsoft\NetBanner` | ✅ | ✅ | ✅ (documented compatibility intent) |
| Top classification banner | ✅ | ✅ | ✅ |
| Optional desktop border rendering | ✅ | ⚠️ (varies by implementation/version) | ✅ (AppBar-based behavior documented) |
| Custom text / caveats | ✅ | ✅ | ✅ |
| INFOCON support | ✅ | ⚠️ | ℹ️ |
| FPCON support | ✅ | ⚠️ | ℹ️ |
| Multi-monitor awareness | ✅ | ✅ | ✅ |
| Fullscreen-aware placement | ✅ (top-most toggled off in fullscreen) | ⚠️ | ✅ (documented) |
| Mouse-over opacity behavior | ❌ | ℹ️ | ✅ (documented in README) |
| Dedicated Windows Service host | ✅ | ⚠️ | ℹ️ |
| Explicit inter-process named pipe coordination | ✅ | ℹ️ | ℹ️ |
| Installer workflow for enterprise deployment | ✅ (Inno Setup + service create/start) | ✅ | ✅ (MSI/scripts documented) |
| Dedicated test project in repo | ✅ | ℹ️ | ℹ️ |

## Migration interpretation

### Why choose NetBannerNG
- If you need policy-key compatibility with a modular architecture (UI + service + common).
- If service lifecycle control and IPC-based coordination are important.
- If you need INFOCON/FPCON support in the current codebase.

### Why stay/choose SystemBanner
- If your org prefers a simpler app-centric deployment model.
- If documented mouse-over opacity behavior is a hard requirement.
- If existing admin processes already rely on its MSI/script conventions.

### Typical migration path to NetBannerNG
1. Keep existing NetBanner policy path.
2. Deploy NetBannerNG in pilot ring.
3. Validate monitor/fullscreen behavior and caveats.
4. Expand deployment in phases with rollback checkpoints.

## Evidence notes

- NetBannerNG statuses are based on this repository’s source and docs.
- Microsoft NetBanner and SystemBanner entries are based on public behavior descriptions; some cells remain ⚠️/ℹ️ where source-level verification is unavailable in this environment.
