# UlanziAdapter.Filter

Experimental KMDF HID filter driver for the Ulanzi D100H path.

This driver intercepts `IOCTL_HID_READ_REPORT` before the report reaches normal HID consumers. It applies byte-level rules:

- match raw HID report bytes;
- suppress the report by zeroing it;
- or replace it with another raw report.

This is the architecture required to stop D100H `VolumeUp` / `VolumeDown` reports before Windows changes system volume.

## Requirements

- Windows 10 or later.
- Visual Studio with Desktop C++ workload.
- Windows Driver Kit matching the Visual Studio version.
- Test signing or a properly signed driver package.
- Administrator rights for installation.

## Important

The INF contains a placeholder hardware ID:

```text
HID\VID_0000&PID_0000&MI_00
```

Replace it with the actual D100H HID interface hardware ID from Device Manager before installing.

## Build

Use `tools\build-driver.ps1` from a Windows Developer PowerShell with WDK installed.

## Install

After replacing the hardware ID and building/signing:

```powershell
pnputil /add-driver .\drivers\UlanziAdapter.Filter\UlanziAdapter.Filter.inf /install
```

Uninstall through Device Manager or `pnputil` after identifying the published OEM INF.
