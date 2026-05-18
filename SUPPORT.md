# NetBannerNG Support Policy

NetBannerNG is a community-maintained, GPLv3 open-source project. This document describes what is supported, what to expect from maintainers, and how the project handles servicing and end-of-life for released versions.

This is a best-effort policy for a volunteer-maintained project. It is not a commercial service-level agreement.

## Supported platforms

NetBannerNG ships as a 64-bit Windows desktop + service application built on .NET Framework 4.8.1.

| Component            | Supported                                  |
| -------------------- | ------------------------------------------ |
| Operating system     | Windows 10 (in-support builds), Windows 11, Windows Server 2022+ (untested) |
| Architecture         | x64 (`x64compatible` per `installer/setup.iss`) |
| Runtime              | .NET Framework 4.8.1 (`net481`)            |
| Privilege model      | Per-machine install, requires Administrator |
| Deployment model     | Inno Setup installer (`installer/setup.iss`) |
| Management surface   | Group Policy (ADMX/ADML) + local registry fallback |

Out of scope for support:

- Windows versions that have reached Microsoft end-of-support.
- Windows Server SKUs older than 2022.
- 32-bit (x86) and ARM64 Windows.
- Side-by-side `sc.exe`-driven installs or other non-installer deployment paths.
- Cross-session or kiosk configurations not validated by the project's tests.

## Supported releases

- The current `master` branch and the most recently published GitHub Release are the primary supported surface.
- Critical security fixes may be backported to the latest released minor line on a best-effort basis. Older minor lines are not actively serviced.
- Development snapshots and unsigned local builds are not supported for production use.

## Versioning

NetBannerNG uses a 3-part `major.minor.patch` version (Semantic Versioning intent) declared in `src/Directory.Build.props` and enforced by `.github/workflows/release.yml`.

- **Major** — incompatible changes to policy schema, registry layout, pipe contracts, or installer behavior.
- **Minor** — reserved for exceptional maintenance needs; not expected under the current bug-fix and security-update policy.
- **Patch** — backward-compatible fixes and security updates.

## How to get help

Pick the channel that matches your need:

| Need                              | Where to go |
| --------------------------------- | ----------- |
| Bug reports, crashes, regressions | GitHub Issues |
| Feature requests, design ideas    | Out of scope under the current maintenance policy |
| Configuration / GPO questions     | GitHub Discussions, after reading `docs/ADMIN_OPERATIONS.md` |
| Security vulnerabilities          | Use GitHub's private vulnerability reporting on the repository — do **not** open a public issue |

Before filing a bug, please include:

- NetBannerNG version (`Get-ItemProperty` on the install path or installer log).
- Windows edition + build (`winver`).
- Whether the system is domain-joined, workgroup, or air-gapped.
- Output of:
  ```powershell
  Get-Service NetBannerNGWatchdog
  Get-ItemProperty "HKLM:\SOFTWARE\Policies\NetBannerNG"
  ```
- Relevant Event Viewer entries from the `Application` log, source `NetBannerNG`.

## Response expectations

Maintainer time is donated. The targets below are aspirations, not contractual SLAs:

| Item                                | Target initial response |
| ----------------------------------- | ----------------------- |
| Security vulnerability (private report) | 7 days |
| Crash / data-loss bug on a supported platform | 14 days |
| Functional bug on a supported platform | 30 days |
| Feature request                     | Out of scope; will be closed |

Responses are triage, not resolution. Fix timelines depend on severity, reproducibility, and contributor availability.

## End-of-life policy

A released minor line (for example `1.0.x`) is supported until **one of**:

- A newer minor line has been generally available for at least 90 days, **or**
- The project explicitly announces EOL for that line in the release notes.

After EOL, no further patches — including security patches — will be issued for that line. Upgrade to the current release.

If `master` migrates off .NET Framework 4.8.1 or drops a supported Windows version, the change will be announced in the release notes for the first version that requires it.

## Out-of-scope items (will be closed)

- New feature requests, including new catalog families, new policy values, new banner modes, new deployment models, or new management surfaces.
- Reports asking for features that contradict the security note in `README.md` (NetBannerNG is not an access-control, authorization, or DLP boundary).
- Reports against unmodified, unsupported third-party forks.
- Requests for paid support, custom integrations, or private builds.

## Contributing

Patches, reproductions, and documentation improvements are welcome. See `docs/DEVELOPMENT.md` for build/test setup.
