$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$packRoot = $PSScriptRoot
$entries = Get-Content -LiteralPath (Join-Path $packRoot 'prompts.json') -Raw | ConvertFrom-Json
$pngRoot = Join-Path $packRoot 'PNG'
New-Item -ItemType Directory -Force -Path $pngRoot | Out-Null
Add-Type -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives,System.Private.Windows.GdiPlus,System.Private.Windows.Core -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
public static class SkillExport {
  public static Rectangle Bounds(Bitmap b) {
    int l=b.Width,t=b.Height,r=-1,d=-1;
    for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).A>8){l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);d=Math.Max(d,y);}
    if(r<l)throw new Exception("Empty image");
    return new Rectangle(l,t,r-l+1,d-t+1);
  }
  public static void Export(string source,string target) {
    using(var src=new Bitmap(source)) using(var dst=new Bitmap(512,512,PixelFormat.Format32bppArgb)) using(var g=Graphics.FromImage(dst)) {
      var bounds=Bounds(src);
      float scale=408f/Math.Max(bounds.Width,bounds.Height);
      int w=(int)Math.Round(bounds.Width*scale),h=(int)Math.Round(bounds.Height*scale);
      g.Clear(Color.Transparent);g.CompositingMode=CompositingMode.SourceCopy;
      g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
      g.DrawImage(src,new Rectangle((512-w)/2,(512-h)/2,w,h),bounds,GraphicsUnit.Pixel);
      dst.Save(target,ImageFormat.Png);
    }
  }
}
'@
$manifest = @()
foreach ($e in $entries) {
  $sourceCopy = Join-Path $packRoot ('source/' + $e.id + '.png')
  Copy-Item -LiteralPath $e.source -Destination $sourceCopy
  $dest = Join-Path $pngRoot ($e.id + '.png')
  [SkillExport]::Export($sourceCopy,$dest)
  $b = [System.Drawing.Bitmap]::FromFile($dest)
  $bounds = [SkillExport]::Bounds($b)
  $manifest += [ordered]@{id=$e.id;file=('PNG/'+$e.id+'.png');width=$b.Width;height=$b.Height;alpha=$true;bounds=@($bounds.X,$bounds.Y,$bounds.Width,$bounds.Height);cornerAlpha=$b.GetPixel(0,0).A;sha256=(Get-FileHash -LiteralPath $dest -Algorithm SHA256).Hash}
  if ($b.Width -ne 512 -or $b.Height -ne 512 -or $b.GetPixel(0,0).A -ne 0) { throw 'Invalid PNG output' }
  $b.Dispose()
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $packRoot 'manifest.json') -Encoding utf8
# Review sheet only: original PNGs remain transparent and text-free.
$sheet = New-Object System.Drawing.Bitmap 1100,1320
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.ColorTranslator]::FromHtml('#eee9df'))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$font = New-Object System.Drawing.Font 'Segoe UI',12
$titleFont = New-Object System.Drawing.Font 'Segoe UI',19,([System.Drawing.FontStyle]::Bold)
$ink = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#241426'))
$dark = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#303747'))
$g.DrawString('SKILL ICONS / 19 / APPROVAL PREVIEW',$titleFont,$ink,20,12)
$g.DrawString('Large + 60px + 80px samples | PNG files: 512 x 512, transparent',$font,$ink,20,48)
for ($i=0; $i -lt $entries.Count; $i++) {
  $x = ($i % 5)*220
  $y = 85 + [int][Math]::Floor($i/5)*305
  $e = $entries[$i]
  $b = [System.Drawing.Bitmap]::FromFile((Join-Path $pngRoot ($e.id+'.png')))
  $g.DrawString(('{0:00}  {1}' -f ($i+1),$e.id),$font,$ink,($x+16),$y)
  $g.DrawImage($b,[System.Drawing.Rectangle]::new($x+22,$y+25,176,176))
  $g.FillRectangle($dark,$x+12,$y+207,196,86)
  $g.DrawImage($b,[System.Drawing.Rectangle]::new($x+28,$y+220,60,60))
  $g.DrawImage($b,[System.Drawing.Rectangle]::new($x+112,$y+210,80,80))
  $b.Dispose()
}
$sheet.Save((Join-Path $packRoot 'Review.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$sheet.Dispose();$font.Dispose();$titleFont.Dispose();$ink.Dispose();$dark.Dispose()
Write-Output ('Exported and checked '+$entries.Count+' transparent 512x512 icons.')
