# NetBannerNG Admin Operations Guide

Audience: Windows endpoint administrators and deployment engineers.

## Scope

This guide covers day-2 operations for NetBannerNG:
- Install
- Remove
- Upgrade
- Rollback

> NetBannerNG installs a Windows service named `netbannerng` and reads policy from `HKLM\Software\Policies\Microsoft\NetBanner`.

---

## 1) Install

### Option A: Installer package (recommended)
1. Run the NetBannerNG setup executable as Administrator.
2. Verify files are installed under:
   - `C:\Program Files\NetBannerNG`
3. Verify service registration/start:

```powershell
Get-Service netbannerng
sc.exe qc netbannerng
```

Expected state after install:
- Service exists and is configured for auto-start.
- Service is running.
- Banner appears for logged-on users (after policy is applied).

### Option B: scripted/service-only install example

```cmd
set APPDIR=C:\Program Files\NetBannerNG
sc.exe create "netbannerng" start= auto binPath= "%APPDIR%\NetBannerNG.Service.exe" displayname= "NetBannerNG Service"
sc.exe start "netbannerng"
```

---

## 2) Remove (Uninstall)

### Option A: Use installer uninstall (recommended)
- Uninstall from Apps & Features (or run uninstaller from install directory).

### Option B: scripted remove example

```cmd
sc.exe stop "netbannerng"
sc.exe delete "netbannerng"
```

Then remove residual binaries if needed:

```cmd
rmdir /s /q "C:\Program Files\NetBannerNG"
```

---

## 3) Upgrade

Recommended pattern:
1. Export/backup relevant registry keys and current installer artifacts.
2. Apply new installer version (in-place upgrade).
3. Confirm service status and banner behavior.

### Pre-upgrade backup examples

```powershell
reg export "HKLM\SOFTWARE\Policies\Microsoft\NetBanner" "C:\Temp\NetBanner-policy-backup.reg" /y
reg export "HKLM\SOFTWARE\NetBannerNG" "C:\Temp\NetBannerNG-local-backup.reg" /y
```

### Post-upgrade verification examples

```powershell
Get-Service netbannerng
Get-Item "C:\Program Files\NetBannerNG\NetBannerNG.Service.exe" | Select-Object FullName,LastWriteTime,Length
```

Operational checks:
- Service running without restart loops.
- Banner renders on all monitors.
- Fullscreen transitions move banner behind fullscreen windows and restore afterward.

---

## 4) Rollback

Use rollback when an upgrade introduces rendering or service issues.

Recommended rollback pattern:
1. Stop and remove current service.
2. Reinstall prior known-good version.
3. Restore registry backups if policy/local settings were changed unexpectedly.
4. Start service and validate behavior.

### Rollback command examples

```cmd
sc.exe stop "netbannerng"
sc.exe delete "netbannerng"
```

Install prior version, then restore backups if needed:

```powershell
reg import "C:\Temp\NetBanner-policy-backup.reg"
reg import "C:\Temp\NetBannerNG-local-backup.reg"
Get-Service netbannerng
```

---

## 5) Troubleshooting quick checks

```powershell
Get-Service netbannerng
sc.exe query netbannerng
tasklist /fi "imagename eq NetBannerNG.exe"
```

If service starts but banner does not appear:
- Confirm policy key exists and contains expected values.
- Confirm interactive user session is present.
- Check Event Viewer logs for service exceptions.

---

## 6) Change management recommendations

- Pilot on a small endpoint ring first.
- Validate multi-monitor and fullscreen app scenarios.
- Keep one prior installer version for fast rollback.
- Version and archive exported policy snapshots per deployment wave.
