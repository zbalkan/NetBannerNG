# Feature Parity Matrix

Audience: contributors, migration planners, security engineering leads, endpoint platform owners.

This matrix is a contributor-facing reference for current known feature parity between NetBannerNG, Microsoft NetBanner, and SystemBanner to support migration planning and implementation decisions.

## Legend

- ✅: Supported
- ⚠️: Partial / implementation-dependent
- ❌: Not currently supported
- ℹ️: Unknown from available public evidence

## Capability matrix

| Capability | NetBannerNG | Microsoft NetBanner | SystemBanner |
| --- | ---: | ---: | ---: |
| Group Policy-based configuration | ✅ | ✅ | ✅ |
| Policy-selectable classification profiles/schemes | ✅ (`ClassificationSelection`) | ⚠️ | ℹ️ |
| Top classification banner | ✅ | ✅ | ✅ |
| Optional desktop border rendering | ✅ | ⚠️ (varies by implementation/version) | ✅ |
| Optional full-width bottom banner mode | ✅ | ⚠️ | ℹ️ |
| Custom text / caveats | ✅ | ✅ | ✅ |
| INFOCON support | ✅ | ⚠️ | ℹ️ |
| FPCON support | ✅ | ⚠️ | ℹ️ |
| Multi-monitor awareness | ✅ | ✅ | ✅ |
| Fullscreen-aware placement | ✅ | ⚠️ | ✅ |
| Mouse-over opacity behavior | ❌ | ℹ️ | ✅ |
| Dedicated Windows Service host | ✅ | ⚠️ | ℹ️ |
| Explicit inter-process named pipe coordination | ✅ | ℹ️ | ℹ️ |
| Installer workflow for enterprise deployment | ✅ (Inno Setup + service lifecycle) | ✅ | ✅ |
| Textual-only EUCI/EP/national/international preset model (non-authoritative colors) | ✅ | ℹ️ | ℹ️ |
| Dedicated test project in repo | ✅ | ℹ️ | ℹ️ |

## Architectural deltas that matter in migration

### NetBannerNG strengths

- Modular runtime split (`UI + Service + Common`) improves maintainability and governance.
- Service-supervised lifecycle reduces user-session drift and enables deterministic restart control.
- Named-pipe contract and security-focused tests provide clearer IPC hardening path.

### SystemBanner strengths

- Simpler app-centric operational mental model.
- Strongly documented end-user UX expectations (notably mouse-over opacity).
- Straightforward install/remove messaging for admin operators.

## Migration interpretation

### Choose NetBannerNG when you need

- service-controlled lifecycle supervision,
- richer policy composition (INFOCON/FPCON + catalogs),
- explicit compatibility path for NetBanner policy migration.

### Keep/choose SystemBanner when you need

- documented mouse-over opacity as a strict requirement,
- simpler single-application operational profile.

### Typical migration path to NetBannerNG

1. Preserve policy path compatibility during pilot.
2. Validate multi-monitor and fullscreen behavior in ring-0.
3. Validate session behavior in console and RDP usage patterns.
4. Phase rollout with rollback checkpoints.

## Evidence notes

- NetBannerNG entries come from local source/docs in this repository.
- Microsoft NetBanner/SystemBanner cells reflect public behavior descriptions; unknowns remain where source-level verification was unavailable.
