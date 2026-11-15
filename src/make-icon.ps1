Add-Type -AssemblyName System.Drawing

$outDir = 'C:\Users\USER\Desktop\впн\ОБХОД\app\icon_preview'
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [float]$size
    $pad = [float]($s * 0.04)
    $rect = New-Object System.Drawing.RectangleF($pad, $pad, ($s - 2 * $pad), ($s - 2 * $pad))
    $radius = [float]($s * 0.24)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc(($rect.Right - $d), $rect.Y, $d, $d, 270, 90)
    $path.AddArc(($rect.Right - $d), ($rect.Bottom - $d), $d, $d, 0, 90)
    $path.AddArc($rect.X, ($rect.Bottom - $d), $d, $d, 90, 90)
    $path.CloseFigure()

    $c1 = [System.Drawing.Color]::FromArgb(255, 37, 99, 235)
    $c2 = [System.Drawing.Color]::FromArgb(255, 124, 58, 237)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $c1, $c2, 55.0)
    $g.FillPath($brush, $path)

    # щит
    $shield = New-Object System.Drawing.Drawing2D.GraphicsPath
    $cx = $s / 2.0
    $top = $s * 0.22
    $w = $s * 0.46
    $h = $s * 0.56
    $left = $cx - $w / 2.0
    $right = $cx + $w / 2.0
    $midY = $top + $h * 0.52

    $shield.AddBezier($left, $top, ($left + $w * 0.25), ($top + $h * 0.06), ($right - $w * 0.25), ($top + $h * 0.06), $right, $top)
    $shield.AddLine($right, $top, $right, $midY)
    $shield.AddBezier($right, $midY, $right, ($midY + $h * 0.28), ($cx + $w * 0.18), ($top + $h * 0.95), $cx, ($top + $h))
    $shield.AddBezier($cx, ($top + $h), ($cx - $w * 0.18), ($top + $h * 0.95), $left, ($midY + $h * 0.28), $left, $midY)
    $shield.AddLine($left, $midY, $left, $top)
    $shield.CloseFigure()

    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $g.FillPath($white, $shield)

    # прорезь: диагональная полоса + светлая кромка
    $oldClip = $g.Clip
    $g.SetClip($shield, [System.Drawing.Drawing2D.CombineMode]::Intersect)

    $bandW = [float]($s * 0.085)
    $g.TranslateTransform($cx, ($top + $h * 0.5))
    $g.RotateTransform(-52.0)
    $bandRect = New-Object System.Drawing.RectangleF((-$s * 0.6), (-$bandW / 2), ($s * 1.2), $bandW)
    $bandBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $bandRect,
        [System.Drawing.Color]::FromArgb(255, 30, 41, 120),
        [System.Drawing.Color]::FromArgb(255, 49, 46, 129),
        90.0)
    $g.FillRectangle($bandBrush, $bandRect)

    $edge = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(210, 191, 219, 254), [float]([Math]::Max(1.0, $s * 0.016)))
    $g.DrawLine($edge, (-$s * 0.6), (-$bandW / 2), ($s * 0.6), (-$bandW / 2))
    $g.ResetTransform()

    $g.Clip = $oldClip

    $g.Dispose()
    return $bmp
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $p = Join-Path $outDir ("icon_{0}.png" -f $s)
    $bmp.Save($p, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += [pscustomobject]@{ Size = $s; Bytes = [IO.File]::ReadAllBytes($p) }
    $bmp.Dispose()
}

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$pngs.Count)
$offset = 6 + 16 * $pngs.Count
foreach ($p in $pngs) {
    $bw.Write([Byte]$(if ($p.Size -ge 256) { 0 } else { $p.Size }))
    $bw.Write([Byte]$(if ($p.Size -ge 256) { 0 } else { $p.Size }))
    $bw.Write([Byte]0); $bw.Write([Byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$p.Bytes.Length)
    $bw.Write([UInt32]$offset)
    $offset += $p.Bytes.Length
}
foreach ($p in $pngs) { $bw.Write($p.Bytes) }
$bw.Flush()
[IO.File]::WriteAllBytes('C:\Users\USER\Desktop\впн\ОБХОД\app\icon.ico', $ms.ToArray())
$bw.Close()
"ico: " + (Get-Item 'C:\Users\USER\Desktop\впн\ОБХОД\app\icon.ico').Length + " bytes"
