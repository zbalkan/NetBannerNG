# NetBannerNG

NetBannerNG is an open-source alternative to Microsoft NetBanner for Windows endpoints.

It displays a persistent desktop classification banner and supports existing NetBanner Group Policy settings so organizations can migrate with minimal policy changes.

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

## Policy compatibility

NetBannerNG reads NetBanner-compatible policy values from:

- `HKLM\Software\Policies\Microsoft\NetBanner`

The repository includes policy templates in `GPO/`.

## Installation

An installer script is included at:

- `src/setup.iss`

The installer places binaries under Program Files and installs/starts the NetBannerNG Windows service.

## Documentation

- **User/Operator overview:** this `README.md`
- **Developer + sysadmin internals:** `ARCHITECTURE.md`

## Project status

- FPCON and INFOCON support added
- Orignalk Netbanner GPO files added
