@echo off
rem Builds the portable app into dist\ using the C# compiler that ships with
rem Windows (.NET Framework 4.8); no SDK needed.
setlocal
cd /d "%~dp0"

set VERSION=1.0.0
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set OUT=dist\Monitor Control

if not exist assets\app.ico powershell -NoProfile -File tools\make-icon.ps1 || exit /b 1

if exist dist rmdir /s /q dist
mkdir "%OUT%"

"%FW%\csc.exe" /nologo /target:winexe /optimize+ /platform:anycpu ^
  /out:"%OUT%\Monitor Control.exe" ^
  /win32icon:assets\app.ico /win32manifest:src\app.manifest ^
  /lib:"%FW%\WPF" ^
  /r:PresentationFramework.dll /r:PresentationCore.dll /r:WindowsBase.dll /r:System.Xaml.dll ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Management.dll ^
  /resource:src\MainWindow.xaml,MonitorControl.MainWindow.xaml ^
  /resource:assets\app.ico,MonitorControl.app.ico ^
  src\*.cs || exit /b 1

copy /y package\README.txt "%OUT%\" >nul

rem Source + launcher for PCs where Smart App Control blocks the unsigned .exe.
mkdir "%OUT%\app"
copy /y src\*.cs "%OUT%\app\" >nul
copy /y src\MainWindow.xaml "%OUT%\app\" >nul
copy /y assets\app.ico "%OUT%\app\" >nul
copy /y "package\Monitor Control.ps1" "%OUT%\" >nul
powershell -NoProfile -Command "Compress-Archive -Path 'dist\Monitor Control' -DestinationPath 'dist\MonitorControl-%VERSION%-portable.zip' -Force" || exit /b 1

echo Built "%OUT%\Monitor Control.exe" and dist\MonitorControl-%VERSION%-portable.zip
