@echo off
REM Build ban Test May An Tam (ban dich tieng Viet tu BibaYanji) tren may Windows.
REM Yeu cau: Windows 64-bit, .NET SDK 10. Chay file nay tu thu muc goc cua repo.
REM Thu tu KHONG DUOC DOI: publish FurMark truoc, roi moi publish app chinh.
REM Thu muc publish thu duoc la goi tu chua, copy ca thu muc sang USB la chay portable.
where dotnet >nul 2>&1
if %errorlevel% neq 0 (
  echo CHUA CO .NET SDK: Cai .NET SDK 10 tren may Windows nay truoc, roi chay lai file nay.
  pause
  exit /b
)
cd /d "%~dp0"
echo Buoc 1/2: publish FurMark...
dotnet publish FurMark\FurMark.csproj -p:PublishProfile=FolderProfile
if %errorlevel% neq 0 (
  echo Publish FurMark that bai, xem loi o tren.
  pause
  exit /b
)
echo Buoc 2/2: publish app chinh...
dotnet publish 验机软件\验机软件.csproj -p:PublishProfile=FolderProfile
if %errorlevel% neq 0 (
  echo Publish app that bai, xem loi o tren.
  pause
  exit /b
)
echo.
echo Xong. Thu muc publish nam o: %~dp0publish
echo Copy ca thu muc publish sang USB, giu nguyen cau truc ben trong, roi cam USB vao may can test.
echo Muon dong goi trinh cai dat thi chay tiep bang Inno Setup 6: "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" installer\setup.iss
pause
