# Gap Analysis: NetBannerNG vs SystemBanner

Date: 2026-05-04

## Scope and evidence

This analysis compares:
- **NetBannerNG** (this repository), using local source and architecture/docs.
- **SystemBanner** (`awawrzyniak10/SystemBanner`), using publicly visible GitHub repository metadata and README content.

Because direct git clone/raw fetch from this environment is blocked, SystemBanner internals are inferred from repository structure and README claims.

---

## Executive summary

- **NetBannerNG** is a **multi-project** architecture with explicit separation between UI app, Windows Service host, and common library. It appears designed for operational robustness (service-managed lifecycle, named-pipe communication, shared contracts/helpers).
- **SystemBanner** appears to be a **single-application-centric** implementation (Windows Forms app) with deployment scripts/MSI and GPO templates, emphasizing practical install/admin usability.
- Both tools target the same core mission: persistent classification banners and GPO-based control on Windows endpoints.
- The largest delta is **architectural layering and runtime control model** (service + IPC in NetBannerNG vs app + startup registry pattern in SystemBanner), while the largest functional overlap is **classification messaging, custom colors, and multi-monitor AppBar behavior**.

---

## 1) Architecture and design comparison

### NetBannerNG
- Layered solution:
  - `NetBannerNG` (WPF UI)
  - `NetBannerNG.Service` (Windows Service)
  - `NetBannerNG.Common` (interop/contracts/helpers)
- Runtime model explicitly documents service/app coordination and refresh/heartbeat behavior.
- Includes dedicated components for:
  - named pipe client/server messaging
  - monitor/window watchers
  - app lifecycle service
  - border management

### SystemBanner
- README states it is a C# .NET Framework 4.7.2 **Windows Forms** application.
- Repository shape suggests simpler composition (main app under `Code/SystemBanner`, install/remove scripts, MSI, GPO templates).
- README indicates startup behavior through registry entries and app-level runtime handling for monitor changes/fullscreen opacity behavior.

### Gap interpretation
- **NetBannerNG advantage:** cleaner separation of concerns, better extensibility and testability pathways via dedicated service/common abstractions.
- **SystemBanner advantage:** potentially simpler deployment/debug mental model (single-app focus).

---

## 2) Dependencies / platform strategy

### NetBannerNG
- Targets .NET Framework `net481`.
- Uses modern supporting packages such as `H.Pipes`, `H.Formatters.MessagePack`, `Polly`, `System.Text.Json`, and separate service controller/access control packages.
- Enables comprehensive Roslyn analysis modes (`AnalysisMode=All`, `AnalysisModeSecurity=All`) across projects.

### SystemBanner
- README states .NET Framework 4.7.2 base and WinForms.
- Dependency granularity is not fully visible from this environment, but operational dependencies include registry/GPO/AppBar Win32 behavior.

### Gap interpretation
- **NetBannerNG advantage:** stronger explicit resiliency/IPC/tooling dependency footprint.
- **SystemBanner gap (from available evidence):** less visibility into reliability patterns/libraries and static analysis posture.

---

## 3) Engineering principles (inferred)

### NetBannerNG likely principles
- Separation of concerns.
- Service-oriented runtime control.
- Shared-contract IPC.
- Policy compatibility-first migration path.

### SystemBanner likely principles
- Admin-first deployability.
- Immediate policy-driven behavior via GPO/registry.
- Runtime usability under fullscreen/mouse-over conditions.

### Gap interpretation
- **NetBannerNG -> SystemBanner:** stronger formal modularity.
- **SystemBanner -> NetBannerNG:** stronger documented emphasis on user interaction nuances (fullscreen/mouse-over opacity) and install/remove scripts called out prominently.

---

## 4) Feature/capability overlap

## Common capabilities
- Windows endpoint classification banners.
- Group Policy templates and registry-driven policy control.
- Multi-monitor/adaptive AppBar behavior.
- Custom text/color-oriented marking support.

## NetBannerNG notable capabilities
- Explicit NetBanner policy compatibility path (`HKLM\Software\Policies\Microsoft\NetBanner`).
- INFOCON and FPCON support called out in docs.
- Optional border rendering around desktop.
- Service installation/start lifecycle through installer.

## SystemBanner notable capabilities
- Out-of-box US classified + CUI-oriented policy templates (per README language).
- Fullscreen/mouse-over opacity behavior explicitly documented.
- Direct install/remove admin scripts and MSI flow explicitly described.

---

## 5) Missing functionality / improvement opportunities

## What NetBannerNG appears to miss (relative to SystemBanner README emphasis)
1. **More explicit UX behaviors in top-level docs**
   - SystemBanner prominently documents fullscreen and mouse-over opacity handling; NetBannerNG docs are less explicit at user-facing level.
2. **Turnkey script-based install/remove parity messaging**
   - NetBannerNG has installer definition, but less “one-script” documentation visibility than SystemBanner’s install/remove bat narrative.
3. **Public-facing policy catalog clarity**
   - Could benefit from side-by-side table of supported markings/policy knobs like SystemBanner narrative style.

## What SystemBanner appears to miss (relative to NetBannerNG design)
1. **Service-oriented lifecycle management abstraction**
   - NetBannerNG’s dedicated service + IPC pattern is structurally stronger for enterprise process governance.
2. **Shared common library boundary**
   - NetBannerNG’s `Common` project indicates better code reuse and clearer contract boundaries.
3. **Documented resilience/tooling posture**
   - NetBannerNG explicitly includes retry/IPC packages and broad analyzer settings.
4. **Automated test project visibility**
   - NetBannerNG has a test project in solution; SystemBanner test surface is not evident from available metadata.

---

## 6) Bi-directional gap analysis matrix

| Dimension | NetBannerNG -> SystemBanner (what NetBannerNG has) | SystemBanner -> NetBannerNG (what SystemBanner has) |
|---|---|---|
| Architecture | Multi-project separation (UI/Service/Common), IPC contracts | Simpler single-app conceptual model |
| Runtime control | Service-managed lifecycle, named-pipe control | Startup-registry-centric execution model is straightforward |
| Policy compatibility | Explicit Microsoft NetBanner key compatibility + GPO artifacts | Strong narrative around EO/CUI presets and admin usability |
| UX behavior docs | Border manager, monitor/window watchers in codebase | Clear README emphasis on fullscreen + mouse-over opacity behavior |
| Dependencies/tooling | Polly, H.Pipes, MessagePack, analyzers-all posture | Lighter visible dependency surface (from available data) |
| Testing/quality | Dedicated test project present | No obvious test project visibility in surfaced metadata |
| Deployment narrative | Inno Setup + service registration in architecture doc | Highly explicit install/remove batch workflow messaging |

---

## 7) Recommendations

### For NetBannerNG
1. Add a “Behavior under fullscreen and mouse-over” section in README with concrete expected UX.
2. Publish a concise admin operations guide (install, remove, upgrade, rollback) with script examples.
3. Add a feature parity matrix vs Microsoft NetBanner/SystemBanner to aid migration decisions.

### For SystemBanner
1. Consider splitting code into app/service/common layers (or at least shared core + host) for long-term maintainability.
2. Introduce explicit IPC/service control path for enterprise runtime governance.
3. Add CI/static analysis/test project visibility to improve confidence and contributor onboarding.

---

## Confidence and limitations

- **High confidence** on NetBannerNG findings (local source inspected).
- **Moderate confidence** on SystemBanner internals: conclusions are based on README and repo metadata visible through GitHub web content in this environment, not full source checkout.
