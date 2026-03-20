Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Drawing.Drawing2D|Out-Null

$srcPath = "C:\Users\48565\.gemini\antigravity\brain\03b2bfae-6e26-431d-9efb-c4e7df2bc0fe\fastcli_icon_1773970071431.png"
$fixedPngPath = "C:\Users\48565\.gemini\antigravity\brain\03b2bfae-6e26-431d-9efb-c4e7df2bc0fe\fastcli_icon_fixed.png"
$icoPath = "d:\workroom\project\study\fast-cli\FastCli.Desktop\assets\FastCli.ico"

$src = [System.Drawing.Bitmap]::new($srcPath)
$sz = $src.Width

# 去除生成的 AI 图标周边的死角白边，向内裁剪 4% 并重新应用超级平滑的抗锯齿圆角
$margin = [int]($sz * 0.04)
$innerSz = $sz - 2 * $margin
$r = [int]($innerSz * 0.22) # 22% 圆角半径，类似 iOS 风格

$dst = [System.Drawing.Bitmap]::new($innerSz, $innerSz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($dst)

$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.Clear([System.Drawing.Color]::Transparent)

# 构造圆角剪切路径
$path = [System.Drawing.Drawing2D.GraphicsPath]::new()
$path.AddArc(0, 0, $r * 2, $r * 2, 180, 90)
$path.AddArc($innerSz - $r * 2, 0, $r * 2, $r * 2, 270, 90)
$path.AddArc($innerSz - $r * 2, $innerSz - $r * 2, $r * 2, $r * 2, 0, 90)
$path.AddArc(0, $innerSz - $r * 2, $r * 2, $r * 2, 90, 90)
$path.CloseFigure()

$g.SetClip($path)

$srcRect = [System.Drawing.Rectangle]::new($margin, $margin, $innerSz, $innerSz)
$dstRect = [System.Drawing.Rectangle]::new(0, 0, $innerSz, $innerSz)

$g.DrawImage($src, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)

$g.Dispose()
$path.Dispose()

$dst.Save($fixedPngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$src.Dispose()

Write-Host "1. 透明圆角重构成功: fastcli_icon_fixed.png"

# 生成多分辨率高清 ICO
function Convert-PngToIco {
    param($PngFile, $IcoFile)
    $img = [System.Drawing.Bitmap]::new($PngFile)
    $fs = [System.IO.File]::Create($IcoFile)
    $bw = [System.IO.BinaryWriter]::new($fs)
    
    $sizes = @(256, 128, 64, 48, 32, 24, 16)
    
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$sizes.Length)
    
    $offset = 6 + (16 * $sizes.Length)
    $imgData = @()
    
    foreach ($size in $sizes) {
        $bmp = [System.Drawing.Bitmap]::new($size, $size)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.DrawImage($img, 0, 0, $size, $size)
        $g.Dispose()
        
        $ms = [System.IO.MemoryStream]::new()
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
}

Convert-PngToIco -PngFile $fixedPngPath -IcoFile $icoPath
Write-Host "2. ICO 图标替换完成"
