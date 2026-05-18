# NetBannerNG Admin Operations Guide

Audience: Windows endpoint administrators and deployment engineers.

## Scope

This guide covers day-2 operations for NetBannerNG:

- Install
- Remove
- Upgrade
- Rollback

> NetBannerNG installs a Windows service named `NetBannerNGWatchdog` and reads policy from `HKLM\SOFTWARE\Policies\NetBannerNG`.
> NetBannerNG supports profile-based classification catalogs via policy value `ClassificationSelection` using the format `<Catalog> - <Classification>` (for example: `NATO - COSMIC TOP SECRET`, `TR - HİZMETE ÖZEL`, `US - TOP SECRET//SENSITIVE COMPARTMENT INFORMATION`).

---

## 1) Install

### Installer package (required)

1. Run the NetBannerNG setup executable as Administrator.
2. Verify files are installed under:
   - `C:\Program Files\NetBannerNG`
3. Verify service registration/start:

```powershell
Get-Service NetBannerNGWatchdog
sc.exe qc NetBannerNGWatchdog
```

Expected state after install:

- Service exists and is configured for auto-start.

- Service is running.
- Banner appears for logged-on users (after policy is applied).

Installer behavior reference:

- Service lifecycle (stop/configure/start) is handled by Inno Setup `[Code]` in `installer/setup.iss`.

- Do not perform standalone `sc.exe create/config/delete` as a primary install path.

---

## 2) Remove (Uninstall)

### Use installer uninstall (required)

- Uninstall from Apps & Features (or run uninstaller from install directory).

---

## 3) Upgrade

Current status:

- NetBannerNG does **not** currently provide a formal, supported in-place upgrade path.
- Use uninstall + install workflow for version changes today.
- Inno Setup service lifecycle automation (`installer/setup.iss`) is the intended foundation for future first-class upgrade support.

Recommended version-change pattern (today):

1. Export/backup relevant registry keys and current installer artifacts.
2. Uninstall current version using installer/uninstaller.
3. Install target version using installer package.
4. Confirm service status and banner behavior.

### Pre-upgrade backup examples

```powershell
reg export "HKLM\SOFTWARE\Policies\NetBannerNG" "C:\Temp\NetBanner-policy-backup.reg" /y
```

### Post-upgrade verification examples

```powershell
Get-Service NetBannerNGWatchdog
Get-Item "C:\Program Files\NetBannerNG\NetBannerNG.Watchdog.exe" | Select-Object FullName,LastWriteTime,Length
```

Operational checks after reinstall/upgrade:

- Service running without restart loops.
- Banner renders on all monitors.
- Fullscreen transitions move banner behind fullscreen windows and restore afterward.
- Expected classification color profile is active for the managed OU(s).

### Classification profile verification

```powershell
Get-ItemProperty "HKLM:\SOFTWARE\Policies\NetBannerNG" | Select-Object ClassificationSelection,EnableBottomBanner,CustomSettings,CustomDisplayText
```

Notes:

- Installer/runtime seed missing policy values to **Not Configured** defaults only when values do not exist.
- Existing GPO-provided values are never overwritten by default seeding.
- `CustomSettings=1` uses explicit custom colors and bypasses automatic catalog-based background/foreground selection.

---

## 4) Rollback

Use rollback when an upgrade introduces rendering or service issues.

Recommended rollback pattern:

1. Uninstall current version using installer/uninstaller.
2. Reinstall prior known-good version using installer package.
3. Restore registry backups if policy/local settings were changed unexpectedly.
4. Start service and validate behavior.

Restore backups if needed:

```powershell
reg import "C:\Temp\NetBanner-policy-backup.reg"
Get-Service NetBannerNGWatchdog
```

---

## 5) Troubleshooting quick checks

```powershell
Get-Service NetBannerNGWatchdog
sc.exe query NetBannerNGWatchdog
tasklist /fi "imagename eq NetBannerNG.exe"
```

If service starts but banner does not appear:

- Confirm policy key exists and contains expected values.
- Confirm interactive user session is present.
- Check Event Viewer logs for service exceptions.

---

## 6) GPO management playbook (administrator training)

NetBannerNG policy management is intended to be driven through Group Policy (`Computer Configuration` scope).

Registry backend reminder:

- ADMX/ADML policy settings are ultimately stored in registry by Group Policy processing.
- Treat registry as the backend state, not the primary management interface.
- Plan for registry tattooing behavior in change/rollback procedures.

### Where to configure

1. Import/update `NetBannerNG.admx` and either `en-US/NetBannerNG.adml`, `en-GB/NetBannerNG.adml` or `tr-TR/NetBannerNG.adml` into your Central Store.
2. Open Group Policy Management Editor for the target OU.
3. Navigate to the NetBanner policy node and configure:
   - `Classification` (writes `ClassificationSelection`)
   - Optional: `CustomSettings`, `CustomDisplayText`, `Caveats`, `InfoCon`, `FpCon`
4. For non-`NATO`/`US` textual schemes (including country, EU, and international-organization catalogs such as `ESA`, `OPCW`, `OSCE`, `UN`), define foreground/background colors in policy if your organization requires specific visual standards.
5. Configure `EnableBottomBanner` when you want a full mirrored banner at the bottom edge (otherwise NetBannerNG keeps the legacy bottom border behavior).

> **NOTE:** If you try to test using `gpedit.mcs` with `Local Group Policy Editor` on a domain-joined computer, the registry changes will still occur after a `gpupdate` connected to a domain. If gpupdate fails, it not only fails to download polciies from the DC but also fails to process local policy as well. If you try it in a computer joined to a `WORKGROUP` instead, then you can promptly test the changes and see the changes via `regedit`.

### Recommended profile selection workflow

1. Start with `ClassificationSelection = NOT CONFIGURED - Classification not configured` unless your policy baseline explicitly requires a specific catalog/classification.
2. Set an explicit value (for example `NATO - COSMIC TOP SECRET` or `US - SECRET`) when your organization requires specific labels/colors.
3. Avoid mixing profile intent and free-form labels in `CustomDisplayText` unless mission policy requires it.

### Validation workflow after GPO changes

1. Force policy refresh (`gpupdate /force`) on a pilot endpoint.
2. Verify effective values:

```powershell
   Get-ItemProperty "HKLM:\SOFTWARE\Policies\NetBannerNG" | Select-Object ClassificationSelection,EnableBottomBanner,CustomSettings,CustomDisplayText,Caveats,InfoCon,FpCon
```

3. Confirm banner colors/labels match the selected profile catalog.
4. Expand rollout ring-by-ring after pilot validation.

### Tattooing warning (important)

- Some registry values may persist after GPO changes/unlinking depending on policy state transitions and operational handling.
- Always validate effective values on pilot systems after:
  - GPO unlink/removal
  - OU moves
  - rollback to older policy baselines
- Keep explicit rollback scripts/backups for policy and local keys.

---

## 7) Change management recommendations

- Pilot on a small endpoint ring first.
- Validate multi-monitor and fullscreen app scenarios.
- Keep one prior installer version for fast rollback.
- Version and archive exported policy snapshots per deployment wave.

## 8) Documentation map

- `README.md`: user-facing purpose and high-level behavior
- `docs/DEVELOPMENT.md`: architecture, build/test, and developer lifecycle details
- `docs/FEATURE_PARITY_MATRIX.md`: migration and capability comparison
