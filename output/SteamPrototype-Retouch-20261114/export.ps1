$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$source=[System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot 'source.png'))
$manifest=@()
foreach($size in @(@(920,430),@(1840,860))){
 $w=$size[0];$h=$size[1]
 $canvas=[System.Drawing.Bitmap]::new($w,$h)
 $g=[System.Drawing.Graphics]::FromImage($canvas)
 $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
 $g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
 $scale=[Math]::Max($w/[double]$source.Width,$h/[double]$source.Height)
 $rect=[System.Drawing.RectangleF]::new(($w-$source.Width*$scale)/2,($h-$source.Height*$scale)/2,$source.Width*$scale,$source.Height*$scale)
 $g.DrawImage($source,$rect)
 $g.Dispose()
 $name="erigone_$($w)x$($h).png"
 $dest=Join-Path $PSScriptRoot $name
 $canvas.Save($dest,[System.Drawing.Imaging.ImageFormat]::Png)
 $canvas.Dispose()
 $check=[System.Drawing.Bitmap]::FromFile($dest)
 if($check.Width -ne $w -or $check.Height -ne $h){throw 'Unexpected dimensions'}
 $check.Dispose()
 $manifest += [ordered]@{file=$name;width=$w;height=$h;sha256=(Get-FileHash $dest).Hash}
}
$source.Dispose()
$manifest | ConvertTo-Json | Set-Content (Join-Path $PSScriptRoot 'manifest.json') -Encoding utf8
$manifest | Format-Table
