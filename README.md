# Monitor Control

A small Windows tray app for controlling an external monitor over DDC/CI: brightness, contrast and input source. It was built for the AOC 27G2G3 but works with most monitors that have DDC/CI turned on.

- Panel styled like macOS Control Center, following Windows light/dark mode
- Brightness and contrast sliders (drag, scroll wheel or arrow keys), plus brightness presets
- Input switching (HDMI 1, HDMI 2, DisplayPort, VGA), with a confirmation before switching away from this PC
- Detects input changes made with the monitor's own buttons
- Optional "Open at Login"
- Portable: no installer, no admin rights

## Build

Run `build.cmd`. It uses the C# compiler that ships with Windows (.NET Framework 4.8), so you don't need an SDK.

The output goes to `dist\Monitor Control\` and is also zipped as `dist\MonitorControl-<version>-portable.zip`.

## Run

- Double-click `Monitor Control.exe`.
- On PCs where Smart App Control blocks the unsigned exe, run `Monitor Control.ps1` instead. It compiles the sources in `app\` in memory inside Windows PowerShell.
- `package\README.txt` explains how to create a shortcut that opens it without a console window.

## Layout

| Path | Contents |
| --- | --- |
| `src\` | App source (WPF and WinForms tray, DDC/CI through `dxva2.dll`) |
| `package\` | Launcher script and the README that ships with the portable build |
| `tools\make-icon.ps1` | Generates `assets\app.ico` |

## License

[MIT](LICENSE): free for anyone to use, modify and share.
