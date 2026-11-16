<p align="center">
  <img src="验机软件/Assets/logo-antam.png" alt="Vi Tinh An Tam logo" width="160">
</p>

# An Tâm Check

[![Build Windows portable](https://github.com/YHresearcher/PCcheck/actions/workflows/build-windows.yml/badge.svg)](https://github.com/YHresearcher/PCcheck/actions/workflows/build-windows.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-0078D6.svg)]
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4.svg)]

[Tiếng Việt](README_VI.md)

A tool for testing used PCs / laptops before buying — it answers one question: **CAN THIS MACHINE BE BOUGHT OR NOT**, for both technicians and buyers. Translated and customized from [BibaYanji](https://github.com/Bibapcs/BibaYanji) v0.0.4 by Bibapcs (MIT). Original Chinese README: `README-BibaYanji-goc.md`.

## Screenshots

![Start a check: Fast (under 5 minutes) or Deep check, then one test at a time](docs/screenshots/app-wizard-intro-en.png)

![Config Check: CPU-Z-style hardware info, battery wear and drive health](docs/screenshots/app-config-check-en.png)

![Summary: buy verdict, progress and the main reason](docs/screenshots/app-summary-en.png)

## Virus scan (latest Release)

The portable `AnTamPCtest-portable.zip` (build-99-1) was scanned on the [Kaspersky Threat Intelligence Portal](https://opentip.kaspersky.com/) on 16 Nov 2026 — result **Clean**: 0 malware detections, 0 suspicious activities, 0 network activities. Scanned file (build-99-1): 209.32 MB (219,483,503 bytes), SHA256 `88819946ECB2790408B8086EA1A344AC193BA3F627C83E80144DC06EB0E88883`, MD5 `D2A728BAF52CFB60EEFC195FDD11741C` — these hashes match the build-99-1 zip exactly (newer releases are repackaged, so their hashes may differ).

![Kaspersky scan result: Clean](docs/screenshots/kaspersky-report-clean.png)

![Kaspersky dynamic analysis summary: 0 detections, 0 suspicious activities, 0 network activities](docs/screenshots/kaspersky-dynamic-analysis.png)

## Requirements

- Windows 10 (version 2004 or later) or Windows 11, 64-bit.
- Administrator rights (to read drive SMART data and some sensors).
- No .NET installation needed — the portable package is self-contained.
- The PawnIO driver (installed separately on each test machine) is only needed to read CPU temperature and power while stress-testing; without it those two readings are blank and the app still runs.
- To build from source only: [.NET SDK 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).

## How to use

Open the app → the **Dashboard** shows progress and the current verdict → click **Kiểm tra chi tiết** (Detailed check) to enter the wizard (one test at a time; finishing a test auto-advances to the next unfinished one) → when all are done, go to **Tổng kết** (Summary) to see whether the machine can be bought → click **Lưu hồ sơ phiên này** (Save this session) to save the report to the USB drive before handing the machine over.

## Modules

| # | Module | What it does |
|---|--------|--------------|
| 0 | Dashboard | Progress + current verdict. Read-only, not a test. |
| 10 | Config Check | Scans CPU, RAM, drives, GPU and battery, then compares them against the order configuration (CPU-Z style). |
| 20 | Keyboard Test | 104-key keyboard; the key you press lights up. |
| 30 | Dead Pixel Test | Full-screen color changes to find dead / bright pixels. |
| 40 | Camera - Mic - Speaker | Live camera preview, mic level meter, test music for the speaker. |
| 50 | Stress Test | Stresses the CPU (Prime95) / GPU (FurMark) with live temperature and power charts. |
| 60 | Ports & Charging | Scans ports and runs a 10-second charger test (voltage + power). |
| 70 | Appearance | 8 manual checkboxes; a swollen battery blocks passing. |
| 80 | Disk Speed Test | Measures sequential and random read / write speeds and concludes whether the drive is good. |
| 85 | Device Health | Read-only: Pass / Warning / Blocked / Not tested. |
| 90 | Summary | Buy verdict + the "An Tâm nhắn bạn" note + save session. |

## Buy verdict (Summary page)

- A Blocked module → **NOT RECOMMENDED**
- No Blocked module but a Warning → **CHECK MORE / NEGOTIATE**
- All Pass → **CAN BUY**
- Tests still unfinished → **NOT ENOUGH DATA — NO VERDICT YET**
- Technician marks it manually → **DO NOT BUY THIS MACHINE**

The verdict is rule-based, **no 100-point score**. Verdict sentences are read from `Plugins/Summary/Phrasings.json` (edit the sentences there, no code change needed).

## Drive verdict (automatic, in Config Check)

- Serious errors > 0, or health < 70% → **BLOCKED** (replace the drive and re-test).
- Health 70–89%, CRC errors, or no serious-error field readable → **Warning**.
- Health ≥ 90% with no serious errors → **Pass**.
- SMART unreadable → **Unknown** (check manually with CrystalDiskInfo; never assumed good).

## Credits and license

- BibaYanji by Bibapcs — main code, MIT license, original LICENSE file kept. Bundled components keep their own licenses: LibreHardwareMonitor (MPL-2.0), Prime95 (GIMPS freeware), PawnIO (GPL-2.0), CrystalDiskMark and diskspd (MIT), NAudio (Ms-PL), OpenTK (MIT), Be Vietnam Pro (OFL-1.1). The Ports & Charging module was ported from Tanxe Studio's laptop-diagnostic-tool (MIT).
- The app integrates no closed-source commercial software (e.g. HWiNFO). Hardware data comes only from LibreHardwareMonitor and built-in Windows interfaces (WMI, registry, Win32).
- Test music on the Camera - Mic - Speaker page: see `AUDIO-LICENSE.md`.
