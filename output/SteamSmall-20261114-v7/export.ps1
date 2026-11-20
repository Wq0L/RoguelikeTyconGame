$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$src=[System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot 'source.png'))
foreach($size in @(@(462,174),@(1848,696),@(184,69),@(120,45))){
 $w=$size[0];$h=$size[1]
 $b=[System.Drawing.Bitmap]::new($w,$h)
 $g=[System.Drawing.Graphics]::FromImage($b)
 $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
 $g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
 $factor=[Math]::Max($w/[double]$src.Width,$h/[double]$src.Height)
 $rect=[System.Drawing.RectangleF]::new(($w-$src.Width*$factor)/2,($h-$src.Height*$factor)/2,$src.Width*$factor,$src.Height*$factor)
 $g.DrawImage($src,$rect)
 $g.Dispose()
 $name="small_$($w)x$($h).png"
 $b.Save((Join-Path $PSScriptRoot $name),[System.Drawing.Imaging.ImageFormat]::Png)
 $b.Dispose()
 Write-Output $name
}
$src.Dispose()
