Monitor Control 1.0.0
=====================

Brightness, contrast and input control for external monitors, from the
Windows system tray.

Requirements
------------
- Windows 10 or 11 (.NET Framework 4.8 is already part of Windows).
- A monitor with DDC/CI turned on in its on-screen menu (on most monitors
  it's on by default).

Run it
------
No installation. Keep this folder anywhere you like (Documents, a USB
stick, ...) and double-click "Monitor Control.exe".

A sun icon appears in the system tray (you may need to drag it out of
the ^ overflow area so it stays visible).

- Left-click the icon to open the panel; click anywhere else to close it.
- Right-click it for presets, input switching and Quit.
- Drag the sliders, or scroll the mouse wheel over them.
- "Open at Login" starts it quietly in the tray when you sign in. If you
  move this folder, the login entry follows it the next time you run it.

Switching input
---------------
Switching away from the input this PC is plugged into makes the screen
go dark for this PC. To come back, use the monitor's own input button
(some monitors also accept the switch from the tray menu while another
input is showing).

"Windows protected your PC" / Smart App Control
-----------------------------------------------
This app isn't code-signed, so Windows may warn about it:
- SmartScreen: click "More info" -> "Run anyway".
- Smart App Control (Windows 11) blocks the .exe outright. On those PCs,
  start the app with "Monitor Control.ps1" instead: it compiles the source
  in the "app" folder inside Windows PowerShell and runs it from memory.
  It works the same, takes a second or two longer to start, and shows up
  as "Windows PowerShell" in Task Manager.

  For a Start Menu shortcut without a console window, create a shortcut to:
    C:\Windows\System32\conhost.exe --headless powershell.exe -NoProfile -WindowStyle Hidden -File "<this folder>\Monitor Control.ps1"
  and set its icon to app\app.ico.

Uninstall
---------
Turn off "Open at Login", quit from the tray menu, and delete this folder.
Error logs, if any, are in %LOCALAPPDATA%\MonitorControl.
