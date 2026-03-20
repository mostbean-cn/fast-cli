Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\48565\.gemini\antigravity\brain\03b2bfae-6e26-431d-9efb-c4e7df2bc0fe\fastcli_icon_1773970071431.png"
$fixedPngPath = "C:\Users\48565\.gemini\antigravity\brain\03b2bfae-6e26-431d-9efb-c4e7df2bc0fe\fastcli_icon_fixed.png"
$icoPath = "d:\workroom\project\study\fast-cli\assets\FastCli.ico"

$src = New-Object System.Drawing.Bitmap($srcPath)
$sz = $src.Width
$margin = [int]($sz * 0.05)
$innerSz = $sz - 2 * $margin
$r = [int]($innerSz * 0.22)

$dst = New-Object System.Drawing.Bitmap($innerSz, $innerSz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($dst)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.Clear([System.Drawing.Color]::Transparent)

$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$path.AddArc(0, 0, $r * 2, $r * 2, 180, 90)
$path.AddArc($innerSz - $r * 2, 0, $r * 2, $r * 2, 270, 90)
$path.AddArc($innerSz - $r * 2, $innerSz - $r * 2, $r * 2, $r * 2, 0, 90)
$path.AddArc(0, $innerSz - $r * 2, $r * 2, $r * 2, 90, 90)
$path.CloseFigure()

$g.SetClip($path)
$srcRect = New-Object System.Drawing.Rectangle($margin, $margin, $innerSz, $innerSz)
$dstRect = New-Object System.Drawing.Rectangle(0, 0, $innerSz, $innerSz)
$g.DrawImage($src, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)

$g.Dispose()
$path.Dispose()
$dst.Save($fixedPngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$src.Dispose()

$sizes = @(256, 128, 64, 48, 32, 24, 16)
$img = New-Object System.Drawing.Bitmap($fixedPngPath)
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)

$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$sizes.Length)

$offset = 6 + (16 * $sizes.Length)
$imgData = @()

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g2 = [System.Drawing.Graphics]::FromImage($bmp)
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g2.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g2.DrawImage($img, 0, 0, $size, $size)
    $g2.Dispose()
    
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $b = $ms.ToArray()
    $imgData += ,$b
    $ms.Dispose()
    $bmp.Dispose()
    
    $w = if ($size -eq 256) { 0 } else { $size }
    $bw.Write([byte]$w)
    $bw.Write([byte]$w)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$b.Length)
    $bw.Write([uint32]$offset)
    
    $offset += $b.Length
}

foreach ($b in $imgData) {
    $bw.Write($b)
}

$bw.Close()
$fs.Close()
$img.Dispose()
Write-Host "Success! FastCli.ico heavily patched."
