$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$rows=@()
foreach($item in (Get-Content "$PSScriptRoot/design.json" -Raw | ConvertFrom-Json)){
 $src=[System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot $item.source))
 [int]$w=$item.width;[int]$h=$item.height
 $b=[System.Drawing.Bitmap]::new($w,$h)
 $g=[System.Drawing.Graphics]::FromImage($b)
 $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
 $g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
 $scale=[Math]::Max($w/[double]$src.Width,$h/[double]$src.Height)
 $g.DrawImage($src,[System.Drawing.RectangleF]::new(($w-$src.Width*$scale)/2,($h-$src.Height*$scale)/2,$src.Width*$scale,$src.Height*$scale))
 $file="PNG/$($item.id)_$($w)x$($h).png"
 $b.Save((Join-Path $PSScriptRoot $file),[System.Drawing.Imaging.ImageFormat]::Png)
 $g.Dispose();$b.Dispose();$src.Dispose()
 $verify=[System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot $file))
 if($verify.Width -ne $w -or $verify.Height -ne $h){throw 'Size mismatch'}
 $verify.Dispose()
 $rows += @{file=$file;width=$w;height=$h;sha256=(Get-FileHash (Join-Path $PSScriptRoot $file)).Hash}
}
$rows | ConvertTo-Json | Set-Content "$PSScriptRoot/manifest.json" -Encoding utf8
Write-Output 'All PNG sizes verified.'
