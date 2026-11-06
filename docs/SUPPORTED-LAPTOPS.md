# Supported laptops

Clevo RGB talks to the single-zone keyboard controller through the vendor
`InsydeDCHU.dll` (command `0x67`). It does **not** use per-key or multi-zone
protocols.

## Tested configuration

| Item | Value |
|---|---|
| Laptop | THUNDEROBOT 911 Plus |
| Barebone | Clevo NH79HX-H |
| CPU | Intel Core i5-11400H |
| System family | 911 Series |
| BIOS | INSYDE 1.07.07TQLM10 (2021-10-26) |
| OS | Windows 11 build 26100 |
| Keyboard USB ID | `VID_32C2&PID_0066` |
| Vendor stack | Control Center 3.0, `InsydeDCHU.dll` |
| Lighting | Single zone, all keys one color |

## What “supported” means

The app needs three things:

1. Windows 10 or 11, x64.
2. A single-zone keyboard controlled by `InsydeDCHU.dll`.
3. That DLL available next to the app or in a Control Center installation.

If the app finds the DLL and the first color write succeeds, the status in the
top-right corner shows **Подключено**. If the keyboard changes color, the model
is compatible in practice.

## Compatibility table

| Device family | Status | Notes |
|---|---|---|
| THUNDEROBOT 911 Plus / Clevo NH79HX-H | ✅ Tested | Reference machine for this project |
| Clevo NH79HX / NH7xHX family | 🟡 Likely | Same Control Center 3.0 / DCHU generation |
| Clevo P955ET1 and similar single-zone models | 🟡 Likely | Same `0x67` color protocol |
| Tongfang / Clevo resellers using the same DCHU driver | 🟡 Likely | Check the DLL path and test the status |
| Per-key RGB keyboards | ❌ No | Requires an addressable-key protocol |
| 3-zone keyboards | ❌ No | Requires zone-specific commands |
| Lightbar-only models | ❌ No | Different controller path |
| Lenovo, ASUS, Acer, HP, MSI, Dell | ❌ No | Different EC / vendor SDK |

“Likely” means the hardware generation and driver stack match, but the model
has not been tested by the maintainer. Please open an issue with your result.

## Check your machine

```powershell
Test-Path "${env:ProgramFiles(x86)}\ControlCenter\DCHU\InsydeDCHU.dll"
```

Also check the 64-bit path:

```powershell
Test-Path "${env:ProgramFiles}\ControlCenter\DCHU\InsydeDCHU.dll"
```

If both return `False`, install Control Center 3.0 or copy the DLL next to
`ClevoRgbLite.exe`.

## Report a new compatible model

Open an issue and include:

- exact laptop model and barebone name
- Windows version
- whether the app status says **Подключено**
- whether the keyboard actually changes color
- the first lines of `%LOCALAPPDATA%\ClevoRGBLite\app.log`

Do not upload the vendor DLL.
