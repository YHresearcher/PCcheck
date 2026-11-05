@echo off
chcp 65001 >nul
REM ============================================================
REM  Tao shortcut "An Tâm PC Test" tren Desktop
REM  - Dat file nay NGAY CANH AnTamPCtest.exe roi bam doi de chay.
REM  - Chay 1 lan la du; shortcut tro toi exe trong thu muc nay,
REM    mo app thang tu Desktop, khong can mo thu muc chay exe nua.
REM  - Khong can quyen Admin (chi tao shortcut tren Desktop cua ban).
REM ============================================================
set "APP_DIR=%~dp0"
set "EXE=%APP_DIR%AnTamPCtest.exe"

if not exist "%EXE%" (
  echo Khong thay AnTamPCtest.exe trong thu muc nay.
  echo Hay dat file .bat nay ngay canh AnTamPCtest.exe roi chay lai.
  pause
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "$desktop=[Environment]::GetFolderPath('Desktop'); $lnk=Join-Path $desktop 'An Tâm PC Test.lnk'; $s=(New-Object -ComObject WScript.Shell).CreateShortcut($lnk); $s.TargetPath='%EXE%'; $s.WorkingDirectory='%APP_DIR%'; $s.Description='An Tâm PC Test - Kiem tra PC/Laptop cu'; $s.Save(); if (Test-Path $lnk) { Write-Host 'DA TAO SHORTCUT: An Tâm PC Test (Desktop)' } else { Write-Host 'TAO SHORTCUT THAT BAI - thu chay file nay bang Run as administrator' }"

pause
