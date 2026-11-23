# Kitin küçük ikonları (22 Kasım 2026): AI çıktısı değil, betikle çizilir (Windows .NET System.Drawing, dış araç / ikon paketi yok;
# üçüncü taraf lisansı gerekmez).
#   & .\Tools\Art\ComicPack\draw_icons.ps1
# Kitin dili (Docs/UI-Dili.md): düz basılı renk, koyu erik-mürekkep kontur (54, 39, 54), sağ altta sert gölge (3B kenar), alt yarıda
# düz bir ton bandı; parlama / gradyan yok. 256×256, saydam zemin; Sprite olarak (BC7, mipmap yok) Assets/Art/UI/ComicPack'e
# yazılır, var olan .meta'nın GUID'i korunur.
#   icon_gear       ayarlar dişlisi (krem)
#   icon_sound      ses açık: krem hoparlör + iki dalga (canlı, kendi rengi)
#   icon_sound_off  ses kapalı: gri hoparlör (gri = pasif) + kırmızı çapraz çizgi (kırmızı = kapalı / iptal)
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
Add-Type -AssemblyName System.Drawing

$ink = [System.Drawing.Color]::FromArgb(255, 54, 39, 54)
$cream = [System.Drawing.Color]::FromArgb(255, 255, 242, 210)
$creamShade = [System.Drawing.Color]::FromArgb(255, 214, 196, 160)
$grey = [System.Drawing.Color]::FromArgb(255, 172, 166, 172)
$greyShade = [System.Drawing.Color]::FromArgb(255, 140, 134, 142)
$red = [System.Drawing.Color]::FromArgb(255, 222, 72, 58)

function Pt([float]$x, [float]$y) { New-Object System.Drawing.PointF $x, $y }

function New-Canvas {
  $bmp = New-Object System.Drawing.Bitmap 256, 256, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.Clear([System.Drawing.Color]::Transparent)
  return @($bmp, $g)
}

function Ink-Pen([float]$width) {
  $pen = New-Object System.Drawing.Pen $ink, $width
  $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
  $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
  $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
  return $pen
}

# Gövde: sert gölge (sağ alta kayık mürekkep kopya) + düz dolgu + alt yarıda ton bandı + kalın mürekkep kontur.
function Draw-Body($g, $path, $shadowPath, $fill, $shade, [float]$bandY) {
  $pen = Ink-Pen 12
  $g.FillPath((New-Object System.Drawing.SolidBrush $ink), $shadowPath)
  $g.DrawPath($pen, $shadowPath)
  $g.FillPath((New-Object System.Drawing.SolidBrush $fill), $path)
  $g.SetClip((New-Object System.Drawing.Region $path), [System.Drawing.Drawing2D.CombineMode]::Replace)
  $band = New-Object System.Drawing.Drawing2D.GraphicsPath
  $band.AddPolygon(@((Pt 0 ($bandY + 25)), (Pt 256 ($bandY - 25)), (Pt 256 256), (Pt 0 256)))
  $g.FillPath((New-Object System.Drawing.SolidBrush $shade), $band)
  $g.ResetClip()
  $g.DrawPath($pen, $path)
}

# Konturlu kalın çizgi (dalga / çapraz çizgi): önce gölge, sonra mürekkep, sonra renk.
function Draw-Stroke($g, [scriptblock]$draw, $color, [float]$width) {
  $shadow = Ink-Pen ($width + 14); $outline = Ink-Pen ($width + 14)
  $fill = New-Object System.Drawing.Pen $color, $width
  $fill.StartCap = [System.Drawing.Drawing2D.LineCap]::Round; $fill.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
  $g.TranslateTransform(9, 11); & $draw $g $shadow; $g.ResetTransform()
  & $draw $g $outline
  & $draw $g $fill
}

function Save-Icon($bmp, [string]$name) {
  $out = Join-Path $repo "Assets\Art\UI\ComicPack\$name.png"
  $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  # .meta: kitin sprite şablonu (hud_panel_blue), kenar payı yok; var olan GUID korunur.
  $template = Get-Content (Join-Path $repo 'Assets\Resources\HudKit\hud_panel_blue.png.meta') -Raw
  $meta = "$out.meta"
  $guid = if (Test-Path $meta) { ((Get-Content $meta | Select-String '^guid: ').Line -replace '^guid: ', '') } else { [guid]::NewGuid().ToString('N') }
  $text = $template -replace 'guid: [0-9a-f]{32}', "guid: $guid" -replace 'spriteBorder: \{[^}]*\}', 'spriteBorder: {x: 0, y: 0, z: 0, w: 0}' `
    -replace 'maxTextureSize: 2048', 'maxTextureSize: 1024' -replace 'textureCompression: 0', 'textureCompression: 2'
  [IO.File]::WriteAllText($meta, $text.Replace("`r`n", "`n"))
  "${name}: $out"
}

# ---------------------------------------------------------------- dişli
function Gear-Path([float]$cx, [float]$cy, [float]$outer, [float]$root, [float]$hole, [int]$teeth) {
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $path.FillMode = [System.Drawing.Drawing2D.FillMode]::Alternate
  $pts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
  $step = 360.0 / $teeth
  for ($i = 0; $i -lt $teeth; $i++) {
    $a = $i * $step - 90
    # diş: kökte geniş, uçta dar (yamuk); dişler arası kök çemberi
    foreach ($p in @(@((-$step * .30), $root), @((-$step * .17), $outer), @(($step * .17), $outer), @(($step * .30), $root))) {
      $t = ($a + $p[0]) * [Math]::PI / 180
      $pts.Add((Pt ($cx + [Math]::Cos($t) * $p[1]) ($cy + [Math]::Sin($t) * $p[1])))
    }
    for ($k = 1; $k -le 4; $k++) {
      $t = ($a + $step * .30 + ($step * .40) * $k / 5) * [Math]::PI / 180
      $pts.Add((Pt ($cx + [Math]::Cos($t) * $root) ($cy + [Math]::Sin($t) * $root)))
    }
  }
  $path.AddPolygon($pts.ToArray())
  $path.AddEllipse($cx - $hole, $cy - $hole, 2 * $hole, 2 * $hole)
  return $path
}

$c = New-Canvas; $bmp = $c[0]; $g = $c[1]
Draw-Body $g (Gear-Path 124 124 100 78 30 8) (Gear-Path 134 136 100 78 30 8) $cream $creamShade 145
$g.DrawEllipse((Ink-Pen 6), 124 - 52, 124 - 52, 104, 104)   # iç halka (comic iç çizgi)
$g.Dispose(); Save-Icon $bmp 'icon_gear'

# ---------------------------------------------------------------- hoparlör
function Speaker-Path([float]$dx, [float]$dy) {
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $path.AddPolygon(@((Pt (26 + $dx) (92 + $dy)), (Pt (72 + $dx) (92 + $dy)), (Pt (132 + $dx) (40 + $dy)), (Pt (132 + $dx) (212 + $dy)),
                     (Pt (72 + $dx) (160 + $dy)), (Pt (26 + $dx) (160 + $dy))))
  return $path
}
$waves = { param($g, $pen) $g.DrawArc($pen, 112, 82, 64, 92, -50, 100); $g.DrawArc($pen, 104, 46, 128, 164, -50, 100) }

# açık: krem hoparlör + iki dalga
$c = New-Canvas; $bmp = $c[0]; $g = $c[1]
Draw-Stroke $g $waves $cream 14
Draw-Body $g (Speaker-Path 0 0) (Speaker-Path 10 12) $cream $creamShade 140
$g.Dispose(); Save-Icon $bmp 'icon_sound'

# kapalı: gri hoparlör (dalga yok) + kırmızı çapraz çizgi
$c = New-Canvas; $bmp = $c[0]; $g = $c[1]
Draw-Body $g (Speaker-Path 18 0) (Speaker-Path 28 12) $grey $greyShade 140
Draw-Stroke $g { param($g, $pen) $g.DrawLine($pen, 40, 216, 214, 42) } $red 22
$g.Dispose(); Save-Icon $bmp 'icon_sound_off'
