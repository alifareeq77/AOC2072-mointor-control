# Draws the app icon (a white sun on an amber rounded square) and writes a
# multi-size .ico. Small sizes are stored as 32-bit DIBs, large ones as PNG.
param([string]$Out = (Join-Path $PSScriptRoot '..\assets\app.ico'))

Add-Type -AssemblyName System.Drawing

function New-IconBitmap([int]$size) {
    $bmp = New-Object Drawing.Bitmap $size, $size, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([Drawing.Color]::Transparent)

    $pad = [Math]::Max(0.5, $size * 0.06)
    $side = $size - 2 * $pad
    $rect = New-Object Drawing.RectangleF $pad, $pad, $side, $side
    $d = $side * 0.45
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $top = [Drawing.Color]::FromArgb(255, 255, 214, 10)
    $bottom = [Drawing.Color]::FromArgb(255, 255, 149, 0)
    $fill = New-Object Drawing.Drawing2D.LinearGradientBrush $rect, $top, $bottom, 90
    $g.FillPath($fill, $path)

    $c = $size / 2
    $pen = New-Object Drawing.Pen ([Drawing.Color]::White), ([Math]::Max(1.3, $size * 0.07))
    $pen.StartCap = 'Round'
    $pen.EndCap = 'Round'
    for ($i = 0; $i -lt 8; $i++) {
        $a = $i * [Math]::PI / 4
        $g.DrawLine($pen, [float]($c + [Math]::Cos($a) * $size * 0.25), [float]($c + [Math]::Sin($a) * $size * 0.25),
                          [float]($c + [Math]::Cos($a) * $size * 0.33), [float]($c + [Math]::Sin($a) * $size * 0.33))
    }
    $r = $size * 0.145
    $g.FillEllipse([Drawing.Brushes]::White, [float]($c - $r), [float]($c - $r), [float](2 * $r), [float](2 * $r))
    $g.Dispose()
    return $bmp
}

function Get-DibBytes([Drawing.Bitmap]$bmp) {
    $s = $bmp.Width
    $ms = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter $ms
    $maskRow = [int]([Math]::Ceiling($s / 32) * 4)
    # BITMAPINFOHEADER; the height counts the color and mask halves together.
    $w.Write([int32]40); $w.Write([int32]$s); $w.Write([int32]($s * 2))
    $w.Write([int16]1); $w.Write([int16]32); $w.Write([int32]0)
    $w.Write([int32]($s * $s * 4 + $maskRow * $s))
    $w.Write([int32]0); $w.Write([int32]0); $w.Write([int32]0); $w.Write([int32]0)
    $data = $bmp.LockBits((New-Object Drawing.Rectangle 0, 0, $s, $s), 'ReadOnly', ([Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $row = New-Object byte[] ($s * 4)
    for ($y = $s - 1; $y -ge 0; $y--) {
        [Runtime.InteropServices.Marshal]::Copy([IntPtr]($data.Scan0.ToInt64() + $y * $data.Stride), $row, 0, $s * 4)
        $w.Write($row)
    }
    $bmp.UnlockBits($data)
    $w.Write((New-Object byte[] ($maskRow * $s)))
    $w.Flush()
    return ,$ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    if ($s -ge 128) {
        $ms = New-Object IO.MemoryStream
        $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
        $bytes = $ms.ToArray()
    } else {
        $bytes = Get-DibBytes $bmp
    }
    $bmp.Dispose()
    [pscustomobject]@{ Size = $s; Bytes = $bytes }
}

New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
$fs = [IO.File]::Create($Out)
$w = New-Object IO.BinaryWriter $fs
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $dim = if ($img.Size -ge 256) { 0 } else { $img.Size }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([int16]1); $w.Write([int16]32)
    $w.Write([int32]$img.Bytes.Length); $w.Write([int32]$offset)
    $offset += $img.Bytes.Length
}
foreach ($img in $images) { $w.Write([byte[]]$img.Bytes) }
$w.Close()
"Wrote $Out"
