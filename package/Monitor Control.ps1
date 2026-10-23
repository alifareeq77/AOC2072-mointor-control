# Starts Monitor Control by compiling its source (in .\app) in memory inside
# Windows PowerShell. Use this on PCs where Smart App Control blocks the
# unsigned "Monitor Control.exe". The Start Menu shortcut runs it through
# "conhost --headless" so no console window appears.
param([switch]$Tray)

$appDir = Join-Path $PSScriptRoot 'app'

# Already running? Just open its panel; no need to compile again.
$show = $null
if ([Threading.EventWaitHandle]::TryOpenExisting('Local\MonitorControl.App.Show', [ref]$show)) {
    [void]$show.Set()
    return
}

try {
    $refs = 'PresentationFramework', 'PresentationCore', 'WindowsBase', 'System.Xaml',
            'System.Windows.Forms', 'System.Drawing', 'System.Management', 'System.Xml'
    Add-Type -AssemblyName $refs
    Add-Type -Path (Get-ChildItem $appDir -Filter *.cs).FullName -ReferencedAssemblies $refs -IgnoreWarnings -ErrorAction Stop

    [MonitorControl.Program]::BaseDirectory = $appDir
    [MonitorControl.Program]::LaunchCommand =
        "`"$env:WINDIR\System32\conhost.exe`" --headless `"$PSHOME\powershell.exe`" -NoProfile -WindowStyle Hidden -File `"$PSCommandPath`" -Tray"

    # Not "$x = if ... else { @() }": PowerShell turns an empty array from a statement into $null.
    $appArgs = [string[]]@()
    if ($Tray) { $appArgs = [string[]]@('--tray') }
    [MonitorControl.Program]::Main($appArgs)
}
catch {
    $logDir = Join-Path $env:LOCALAPPDATA 'MonitorControl'
    New-Item -ItemType Directory -Force $logDir | Out-Null
    Add-Content (Join-Path $logDir 'error.log') "$(Get-Date -Format s)  Launcher: $_"
    Add-Type -AssemblyName System.Windows.Forms
    [void][Windows.Forms.MessageBox]::Show("Monitor Control couldn't start:`n`n$_", 'Monitor Control', 'OK', 'Error')
}
