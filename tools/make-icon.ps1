param(
    [string]$SourceImage
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repo = Split-Path -Parent $PSScriptRoot
$defaultSource = Join-Path $PSScriptRoot 'icon-source.png'

if ($SourceImage -and (Test-Path $SourceImage)) {
    $srcBmpTemp = [System.Drawing.Bitmap]::FromFile($SourceImage)
    $srcBmpTemp.Save($defaultSource, [System.Drawing.Imaging.ImageFormat]::Png)
    $srcBmpTemp.Dispose()
}

if (-not (Test-Path $defaultSource)) {
    throw "Source icon image not found at $defaultSource"
}

function New-RoundedRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Min($r * 2.0, [Math]::Min($w, $h))
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-MaskedIconBitmap([System.Drawing.Image]$source, [int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $inset = [Math]::Max(0.5, $size * 0.025)
    $box = $size - 2.0 * $inset
    $radius = $box * 0.225

    $clipPath = New-RoundedRectPath $inset $inset $box $box $radius
    $g.SetClip($clipPath)

    # Slight zoom-in crop (5.5% each edge) so the keyboard fills the squircle boldly even at 16-32px
    $cropMarginX = $source.Width * 0.055
    $cropMarginY = $source.Height * 0.055
    $srcRect = New-Object System.Drawing.RectangleF $cropMarginX, $cropMarginY, ($source.Width - 2.0 * $cropMarginX), ($source.Height - 2.0 * $cropMarginY)
    $dstRect = New-Object System.Drawing.RectangleF $inset, $inset, $box, $box
    $g.DrawImage($source, $dstRect, $srcRect, ([System.Drawing.GraphicsUnit]::Pixel))

    $g.ResetClip()

    # Subtle glass rim + crisp gradient outline so edges look sharp on dark & light taskbars
    $rimWidth = [Math]::Max(1.0, $size * 0.018)
    $rimBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (
        $dstRect,
        ([System.Drawing.Color]::FromArgb(200, 56, 189, 248)),
        ([System.Drawing.Color]::FromArgb(160, 99, 102, 241)),
        ([System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
    )
    $rimPen = New-Object System.Drawing.Pen $rimBrush, $rimWidth
    $g.DrawPath($rimPen, $clipPath)

    $rimPen.Dispose()
    $rimBrush.Dispose()
    $clipPath.Dispose()
    $g.Dispose()
    return $bmp
}

function ConvertTo-PngBytes([System.Drawing.Bitmap]$bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = [byte[]]$ms.ToArray()
    $ms.Dispose()
    return $bytes
}

function ConvertTo-DibBytes([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width
    $h = $bmp.Height
    $andStride = [int]([Math]::Ceiling($w / 32.0) * 4)
    $xorSize = $w * $h * 4
    $andSize = $andStride * $h

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms

    $bw.Write([UInt32]40)
    $bw.Write([Int32]$w)
    $bw.Write([Int32]($h * 2))
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]0)
    $bw.Write([UInt32]($xorSize + $andSize))
    $bw.Write([Int32]0)
    $bw.Write([Int32]0)
    $bw.Write([UInt32]0)
    $bw.Write([UInt32]0)

    for ($y = $h - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $w; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $bw.Write([byte]$c.B)
            $bw.Write([byte]$c.G)
            $bw.Write([byte]$c.R)
            $bw.Write([byte]$c.A)
        }
    }

    # 1bpp AND mask bottom-up (1 = transparent where alpha == 0)
    $andMask = New-Object byte[] $andSize
    for ($y = $h - 1; $y -ge 0; $y--) {
        $rowOffset = ($h - 1 - $y) * $andStride
        for ($x = 0; $x -lt $w; $x++) {
            if ($bmp.GetPixel($x, $y).A -eq 0) {
                $byteIndex = $rowOffset + [int]([Math]::Floor($x / 8.0))
                $andMask[$byteIndex] = $andMask[$byteIndex] -bor (1 -shl (7 - ($x % 8)))
            }
        }
    }
    $bw.Write($andMask, 0, $andMask.Length)
    $bw.Flush()

    $bytes = [byte[]]$ms.ToArray()
    $bw.Dispose()
    $ms.Dispose()
    return $bytes
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = New-Object System.Collections.ArrayList

$docsDir = Join-Path $repo 'docs'
if (-not (Test-Path $docsDir)) { New-Item -ItemType Directory -Path $docsDir | Out-Null }
$previewPath = Join-Path $docsDir 'icon.png'

$sourceBmp = [System.Drawing.Bitmap]::FromFile($defaultSource)
try {
    foreach ($size in $sizes) {
        $bmp = New-MaskedIconBitmap $sourceBmp $size
        if ($size -eq 256) {
            $bmp.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }

        $frame = if ($size -le 48) { ConvertTo-DibBytes $bmp } else { ConvertTo-PngBytes $bmp }
        $bmp.Dispose()
        [void]$images.Add([byte[]]$frame)
    }
}
finally {
    $sourceBmp.Dispose()
}

$target = Join-Path $repo 'src\VirtualKeyboard.App\Assets\VirtualKeyboard.ico'
$dir = Split-Path -Parent $target
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }

$stream = [System.IO.File]::Create($target)
$writer = New-Object System.IO.BinaryWriter $stream
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$sizes.Count)

$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $size = $sizes[$i]
    $length = $images[$i].Length
    $dim = if ($size -ge 256) { 0 } else { $size }
    $writer.Write([byte]$dim)
    $writer.Write([byte]$dim)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$length)
    $writer.Write([UInt32]$offset)
    $offset += $length
}

foreach ($image in $images) {
    $writer.Write([byte[]]$image, 0, $image.Length)
}
$writer.Flush()
$writer.Close()
$stream.Close()

Write-Output ("wrote " + $target + " (" + (Get-Item $target).Length + " bytes)")
Write-Output ("wrote " + $previewPath + " (" + (Get-Item $previewPath).Length + " bytes)")
