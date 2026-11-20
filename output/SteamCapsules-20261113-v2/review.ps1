$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=$PSScriptRoot
$sheet=[System.Drawing.Bitmap]::new(1360,1100)
$g=[System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.ColorTranslator]::FromHtml('#e9e4dc'))
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$font=[System.Drawing.Font]::new('Segoe UI',15)
$ink=[System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#241426'))
function Place([string]$file,[int]$x,[int]$y,[int]$w,[int]$h,[string]$label){
 $b=[System.Drawing.Bitmap]::FromFile((Join-Path $root ('PNG/'+$file)))
 $g.DrawString($label,$font,$ink,$x,($y-30))
 $g.DrawImage($b,[System.Drawing.Rectangle]::new($x,$y,$w,$h));$b.Dispose()
}
Place 'main_1232x706.png' 20 55 820 470 'MAIN / 1232 x 706'
Place 'header_920x430.png' 20 600 820 383 'HEADER / 920 x 430'
Place 'vertical_748x896.png' 875 55 450 539 'VERTICAL / 748 x 896'
Place 'small_462x174.png' 875 665 462 174 'SMALL / 462 x 174'
Place 'small_462x174.png' 885 930 184 69 '184 x 69'
Place 'small_462x174.png' 1140 930 120 45 '120 x 45'
$sheet.Save((Join-Path $root 'Review.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$sheet.Dispose();$font.Dispose();$ink.Dispose()
$rows=@()
foreach($entry in (Get-Content (Join-Path $root 'design.json') -Raw | ConvertFrom-Json)){
 $name=$entry.id+'_'+$entry.width+'x'+$entry.height+'.png'
 $path=Join-Path $root ('PNG/'+$name)
 $b=[System.Drawing.Bitmap]::FromFile($path)
 if($b.Width -ne $entry.width -or $b.Height -ne $entry.height){throw 'Dimension mismatch'}
 $rows += [ordered]@{file=('PNG/'+$name);width=$b.Width;height=$b.Height;sha256=(Get-FileHash $path -Algorithm SHA256).Hash;font='Bungee Regular';status='awaiting-user-approval'}
 $b.Dispose()
}
$rows | ConvertTo-Json | Set-Content (Join-Path $root 'manifest.json') -Encoding utf8
Write-Output 'All four exact sizes verified.'
