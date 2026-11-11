# Comic Paper UI Pack v2 -> Unity sprites (6 Nov 2026). Source: the user's pack (Codex image_gen output), default
# output/ComicPaperUI-Pack-v2. No external tools (Windows .NET System.Drawing).
#   powershell -ExecutionPolicy Bypass -File Tools/Art/ComicPack/comicpack_prepare.ps1 [-Pack <folder>]
# Every sprite: cropped to its alpha bounds (+pad), resized to a game size (multiple of 4 so it can be block-compressed),
# written with a .meta (9-slice border, BC7, no mipmaps). Existing outputs keep their GUID (meta guid is reused).
# Runtime sprites go to Assets/Resources/ComicPack (loaded by ComicKit); the rest to Assets/Art/UI/ComicPack (not in the build
# until something references them).
# Derived (not AI) variants:
#   - button hover / pressed / disabled: from the NORMAL sprite (lighten / darken / desaturate the paint, ink untouched), so all
#     states share one silhouette. The pack's own hover/pressed differ in shape by up to ~32 px and are not used.
#   - blue button: the green button's paint rotated in hue (the pack has no blue button).
#   - sidebar_navy: the blurred erased-text smudge near the top is painted over with the panel's navy.
#   - bar_fill: the bar track's interior as a white, tintable fill (same rectangle as bar_track).
param([string]$Pack = '')
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
if ($Pack -eq '') { $Pack = Join-Path $repo 'output\ComicPaperUI-Pack-v2' }
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class ComicPack
{
    public sealed class Img { public int W, H; public byte[] P; }   // BGRA, stride W*4
    public static Img Load(string path)
    {
        using (var b = new Bitmap(path))
        {
            var d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var img = new Img { W = b.Width, H = b.Height, P = new byte[b.Width * b.Height * 4] };
            for (int y = 0; y < b.Height; y++) Marshal.Copy(d.Scan0 + y * d.Stride, img.P, y * b.Width * 4, b.Width * 4);
            b.UnlockBits(d); return img;
        }
    }
    static Bitmap ToBitmap(Img img)
    {
        var b = new Bitmap(img.W, img.H, PixelFormat.Format32bppArgb);
        var d = b.LockBits(new Rectangle(0, 0, img.W, img.H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < img.H; y++) Marshal.Copy(img.P, y * img.W * 4, d.Scan0 + y * d.Stride, img.W * 4);
        b.UnlockBits(d); return b;
    }
    // Through a memory stream (GDI+ cannot overwrite a PNG: "generic error"); short retries while a scanner holds the file.
    public static void Save(Img img, string path) { using (var b = ToBitmap(img)) using (var ms = new System.IO.MemoryStream()) { b.Save(ms, ImageFormat.Png); var bytes = ms.ToArray(); for (int k = 0; ; k++) { try { System.IO.File.WriteAllBytes(path, bytes); return; } catch (System.IO.IOException) { if (k >= 5) throw; } catch (UnauthorizedAccessException) { if (k >= 5) throw; } System.Threading.Thread.Sleep(300); } } }
    static float Lum(byte[] p, int i) { return p[i + 2] * .299f + p[i + 1] * .587f + p[i] * .114f; }
    static float Smooth(float a, float b, float x) { float t = Math.Max(0, Math.Min(1, (x - a) / (b - a))); return t * t * (3 - 2 * t); }

    public static Img Crop(Img s, int pad)
    {
        int x0 = s.W, y0 = s.H, x1 = -1, y1 = -1;
        for (int y = 0; y < s.H; y++) for (int x = 0; x < s.W; x++) if (s.P[(y * s.W + x) * 4 + 3] > 8) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
        x0 = Math.Max(0, x0 - pad); y0 = Math.Max(0, y0 - pad); x1 = Math.Min(s.W - 1, x1 + pad); y1 = Math.Min(s.H - 1, y1 + pad);
        var o = new Img { W = x1 - x0 + 1, H = y1 - y0 + 1 }; o.P = new byte[o.W * o.H * 4];
        for (int y = 0; y < o.H; y++) Buffer.BlockCopy(s.P, ((y0 + y) * s.W + x0) * 4, o.P, y * o.W * 4, o.W * 4);
        return o;
    }
    // Area-weighted downscale in premultiplied alpha (no dark/light fringes at the alpha edge).
    public static Img Resize(Img s, int w, int h)
    {
        var o = new Img { W = w, H = h, P = new byte[w * h * 4] };
        double sx = (double)s.W / w, sy = (double)s.H / h;
        for (int y = 0; y < h; y++)
        {
            double fy0 = y * sy, fy1 = (y + 1) * sy;
            for (int x = 0; x < w; x++)
            {
                double fx0 = x * sx, fx1 = (x + 1) * sx, r = 0, g = 0, b = 0, a = 0, area = 0;
                for (int yy = (int)fy0; yy < Math.Min(s.H, (int)Math.Ceiling(fy1)); yy++)
                {
                    double wy = Math.Min(fy1, yy + 1) - Math.Max(fy0, yy); if (wy <= 0) continue;
                    for (int xx = (int)fx0; xx < Math.Min(s.W, (int)Math.Ceiling(fx1)); xx++)
                    {
                        double wx = Math.Min(fx1, xx + 1) - Math.Max(fx0, xx); if (wx <= 0) continue;
                        int i = (yy * s.W + xx) * 4; double wgt = wx * wy, al = s.P[i + 3] / 255.0;
                        b += s.P[i] * al * wgt; g += s.P[i + 1] * al * wgt; r += s.P[i + 2] * al * wgt; a += al * wgt; area += wgt;
                    }
                }
                int k = (y * w + x) * 4;
                if (a > 0) { o.P[k] = (byte)Math.Min(255, b / a + .5); o.P[k + 1] = (byte)Math.Min(255, g / a + .5); o.P[k + 2] = (byte)Math.Min(255, r / a + .5); }
                o.P[k + 3] = (byte)Math.Min(255, a / area * 255 + .5);
            }
        }
        return o;
    }
    // Paint weight: 0 on the dark ink, 1 on coloured paint (smooth in between, so no hard edge appears).
    static float Paint(byte[] p, int i) { return Smooth(36f, 58f, Lum(p, i)); }   // ink lum ~20-35, the darkest paint (3D side) ~60
    // mode: hover (lighter), pressed (darker), disabled (grey, a bit darker). Ink and alpha untouched.
    public static Img State(Img s, string mode)
    {
        var o = new Img { W = s.W, H = s.H, P = (byte[])s.P.Clone() };
        for (int i = 0; i < o.P.Length; i += 4)
        {
            if (o.P[i + 3] == 0) continue; float t = Paint(s.P, i), grey = Lum(s.P, i);
            for (int c = 0; c < 3; c++)
            {
                float v = s.P[i + c], n;
                if (mode == "hover") n = v + (255 - v) * .17f;
                else if (mode == "pressed") n = v * .80f;
                else n = (v + (grey * .92f - v) * .88f) * .92f;
                o.P[i + c] = (byte)Math.Max(0, Math.Min(255, v + (n - v) * t + .5f));
            }
        }
        return o;
    }
    // Paint multiplied toward a colour (amount 0..1); ink and alpha untouched. Used for the shop card's selected state (gold).
    public static Img Tint(Img s, int r, int g, int b, float amount)
    {
        var o = new Img { W = s.W, H = s.H, P = (byte[])s.P.Clone() };
        int[] tint = { b, g, r };
        for (int i = 0; i < o.P.Length; i += 4)
        {
            if (o.P[i + 3] == 0) continue; float t = Paint(s.P, i) * amount;
            for (int c = 0; c < 3; c++) { float v = s.P[i + c]; o.P[i + c] = (byte)Math.Max(0, Math.Min(255, v + (v * tint[c] / 255f - v) * t + .5f)); }
        }
        return o;
    }
    // Hue rotation of the paint (ink keeps its colour); degrees.
    public static Img Hue(Img s, float degrees, float saturation, float value)
    {
        var o = new Img { W = s.W, H = s.H, P = (byte[])s.P.Clone() };
        for (int i = 0; i < o.P.Length; i += 4)
        {
            if (o.P[i + 3] == 0) continue; float t = Paint(s.P, i); if (t <= 0) continue;
            float r = s.P[i + 2] / 255f, g = s.P[i + 1] / 255f, b = s.P[i] / 255f;
            float mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)), d = mx - mn, h = 0, sat = mx == 0 ? 0 : d / mx, val = mx;
            if (d > 0) { if (mx == r) h = 60 * (((g - b) / d) % 6); else if (mx == g) h = 60 * ((b - r) / d + 2); else h = 60 * ((r - g) / d + 4); }
            h = ((h + degrees) % 360 + 360) % 360; sat = Math.Min(1, sat * saturation); val = Math.Min(1, val * value);
            float C = val * sat, X = C * (1 - Math.Abs(h / 60 % 2 - 1)), m = val - C, rr, gg, bb;
            if (h < 60) { rr = C; gg = X; bb = 0; } else if (h < 120) { rr = X; gg = C; bb = 0; } else if (h < 180) { rr = 0; gg = C; bb = X; }
            else if (h < 240) { rr = 0; gg = X; bb = C; } else if (h < 300) { rr = X; gg = 0; bb = C; } else { rr = C; gg = 0; bb = X; }
            float[] n = { bb + m, gg + m, rr + m };
            for (int c = 0; c < 3; c++) { float v = s.P[i + c]; o.P[i + c] = (byte)Math.Max(0, Math.Min(255, v + (n[c] * 255 - v) * t + .5f)); }
        }
        return o;
    }
    // Paints over a rectangle (source pixel coords): light or semi-transparent pixels become the median colour of the dark
    // opaque pixels in the same rectangle, fully opaque.
    public static string PaintOver(Img s, int x0, int y0, int x1, int y1, float lightAbove)
    {
        var rs = new List<int>(); var gs = new List<int>(); var bs = new List<int>();
        for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) { int i = (y * s.W + x) * 4; if (s.P[i + 3] >= 250 && Lum(s.P, i) <= lightAbove) { bs.Add(s.P[i]); gs.Add(s.P[i + 1]); rs.Add(s.P[i + 2]); } }
        if (rs.Count == 0) return "no reference pixels";
        rs.Sort(); gs.Sort(); bs.Sort(); int mid = rs.Count / 2; int changed = 0;
        for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
        {
            int i = (y * s.W + x) * 4;
            if (s.P[i + 3] < 250 || Lum(s.P, i) > lightAbove) { s.P[i] = (byte)bs[mid]; s.P[i + 1] = (byte)gs[mid]; s.P[i + 2] = (byte)rs[mid]; s.P[i + 3] = 255; changed++; }
        }
        return string.Format("painted {0} px with rgb {1},{2},{3}", changed, rs[mid], gs[mid], bs[mid]);
    }
    // Interior of the bar track as a white fill: flood fill from the centre through pixels close to the centre colour (halftone
    // dots and the inner ink line stop it), small holes closed; everything else transparent.
    public static Img BarFill(Img s, out string log)
    {
        int w = s.W, h = s.H, seed = (h / 2) * w + w / 2; int si = seed * 4;
        Func<int, bool> Same = p => { int i = p * 4; if (s.P[i + 3] < 200) return false; int dr = s.P[i + 2] - s.P[si + 2], dg = s.P[i + 1] - s.P[si + 1], db = s.P[i] - s.P[si]; return dr * dr + dg * dg + db * db < 38 * 38; };
        var inside = new bool[w * h]; var q = new Queue<int>(); q.Enqueue(seed); inside[seed] = true;
        while (q.Count > 0)
        {
            int p = q.Dequeue(), x = p % w;
            int[] n = { x > 0 ? p - 1 : -1, x < w - 1 ? p + 1 : -1, p - w, p + w };
            foreach (int c in n) { if (c < 0 || c >= w * h || inside[c] || !Same(c)) continue; inside[c] = true; q.Enqueue(c); }
        }
        var closed = (bool[])inside.Clone(); int count = 0;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            int p = y * w + x; if (inside[p]) { count++; continue; }
            bool l = false, r = false, u = false, d = false;
            for (int k = 1; k <= 6; k++)
            {
                if (!l && x - k >= 0 && inside[p - k]) l = true; if (!r && x + k < w && inside[p + k]) r = true;
                if (!u && y - k >= 0 && inside[p - k * w]) u = true; if (!d && y + k < h && inside[p + k * w]) d = true;
            }
            if (l && r && u && d) { closed[p] = true; count++; }
        }
        var o = new Img { W = w, H = h, P = new byte[w * h * 4] };
        for (int p = 0; p < w * h; p++) if (closed[p]) { int i = p * 4; byte v = (byte)(inside[p] ? 255 : 205); o.P[i] = o.P[i + 1] = o.P[i + 2] = v; o.P[i + 3] = 255; }
        log = string.Format("interior {0} px ({1:0.0}% of the box)", count, 100.0 * count / (w * h));
        return o;
    }
}
"@

# name, source (pack-relative), destination (R = Resources/ComicPack, A = Art/UI/ComicPack), width, border L,B,R,T as share of
# the output size, operation.
$items = @(
  # buttons: normal from the pack, states derived; blue from green
  @{ n='button_green';  s='Buttons/button-green-normal.png';  d='R'; w=768; b=@(.10,.30,.14,.30); op='states' }
  @{ n='button_gold';   s='Buttons/button-gold-normal.png';   d='R'; w=768; b=@(.10,.30,.14,.30); op='states' }
  @{ n='button_purple'; s='Buttons/button-purple-normal.png'; d='R'; w=768; b=@(.10,.30,.14,.30); op='states' }
  @{ n='button_red';    s='Buttons/button-red-normal.png';    d='R'; w=768; b=@(.10,.30,.14,.30); op='states' }
  @{ n='button_blue';   s='Buttons/button-green-normal.png';  d='R'; w=768; b=@(.10,.30,.14,.30); op='blue' }
  # slots
  @{ n='slot_white';    s='Slots/square-white-normal.png';    d='R'; w=256; b=@(.22,.22,.22,.22); op='' }
  @{ n='slot_charcoal'; s='Slots/grid-square-charcoal.png';   d='R'; w=256; b=@(.22,.22,.22,.22); op='' }
  @{ n='slot_blue';     s='Slots/square-blue-normal.png';     d='R'; w=256; b=@(.22,.22,.22,.22); op='' }
  @{ n='slot_gold';     s='Slots/square-gold-normal.png';     d='R'; w=256; b=@(.22,.22,.22,.22); op='' }
  @{ n='slot_navy';     s='Slots/grid-square-navy.png';       d='R'; w=256; b=@(.22,.22,.22,.22); op='' }
  # Skill tree (6 Nov 2026): completed node = green square (the blue square's paint rotated toward the old "max" green), tier
  # dots = small gold / charcoal squares.
  @{ n='slot_green';    s='Slots/square-blue-normal.png';     d='R'; w=256; b=@(.22,.22,.22,.22); op='green' }
  @{ n='pip_gold';      s='Slots/square-gold-normal.png';     d='R'; w=40;  b=@(0,0,0,0); op='' }
  @{ n='pip_empty';     s='Slots/grid-square-charcoal.png';   d='R'; w=40;  b=@(0,0,0,0); op='' }
  # panels
  @{ n='panel_cream';         s='Panels/panel-landscape-cream.png'; d='R'; w=768; b=@(.12,.18,.12,.24); op='' }
  @{ n='card_portrait_cream'; s='Panels/card-portrait-cream.png';   d='R'; w=512; b=@(.16,.12,.16,.14); op='card' }
  @{ n='info_cream';          s='Panels/info-small-cream.png';      d='R'; w=768; b=@(.10,.24,.12,.30); op='' }
  @{ n='tooltip_cream';       s='Panels/tooltip-cream.png';         d='A'; w=640; b=@(.12,.26,.14,.20); op='' }
  @{ n='window_navy';         s='Panels/window-large-navy.png';     d='R'; w=768; b=@(.10,.14,.12,.18); op='' }
  @{ n='sidebar_navy';        s='Panels/sidebar-tall-navy.png';     d='A'; w=384; b=@(.18,.10,.20,.12); op='sidebar' }
  @{ n='skill_tooltip_blue';   s='Panels/skill-tooltip-blue-right.png';   d='A'; w=640; b=@(.12,.18,.14,.24); op='' }
  @{ n='skill_tooltip_gold';   s='Panels/skill-tooltip-gold-right.png';   d='A'; w=640; b=@(.12,.18,.14,.24); op='' }
  @{ n='skill_tooltip_green';  s='Panels/skill-tooltip-green-right.png';  d='A'; w=640; b=@(.12,.18,.14,.24); op='' }
  @{ n='skill_tooltip_purple'; s='Panels/skill-tooltip-purple-right.png'; d='A'; w=640; b=@(.12,.18,.14,.24); op='' }
  # HUD strips and tabs
  @{ n='header_navy';   s='HUD/header-strip-navy.png';    d='R'; w=768; b=@(.08,.30,.10,.30); op='' }
  @{ n='label_cream';   s='HUD/label-strip-cream.png';    d='R'; w=768; b=@(.08,.28,.10,.28); op='' }
  @{ n='resource_navy'; s='HUD/resource-counter-navy.png'; d='A'; w=640; b=@(.12,.28,.12,.28); op='' }
  @{ n='tab_blue';         s='HUD/tab-blue-normal.png';  d='A'; w=512; b=@(.12,.30,.12,.30); op='' }
  @{ n='tab_blue_hover';   s='HUD/tab-blue-hover.png';   d='A'; w=512; b=@(.12,.30,.12,.30); op='' }
  @{ n='tab_blue_pressed'; s='HUD/tab-blue-pressed.png'; d='A'; w=512; b=@(.12,.30,.12,.30); op='' }
  # parts
  @{ n='bar_track'; s='Parts/bar-track-empty.png';  d='A'; w=768; b=@(.06,.30,.06,.30); op='bar' }
  @{ n='divider';   s='Parts/divider-horizontal.png'; d='A'; w=768; b=@(.04,0,.04,0); op='' }
  # Skill tree connections (6 Nov 2026): the same cream ink line, smaller, in Resources.
  @{ n='line_cream'; s='Parts/divider-horizontal.png'; d='R'; w=512; b=@(.05,0,.05,0); op='' }
)

$template = Get-Content (Join-Path $repo 'Assets\Resources\HudKit\hud_panel_blue.png.meta') -Raw
function Write-Sprite($img, [string]$path, $border) {
  [ComicPack]::Save($img, $path)
  $meta = "$path.meta"
  $guid = if (Test-Path $meta) { ((Get-Content $meta | Select-String '^guid: ').Line -replace '^guid: ', '') } else { [guid]::NewGuid().ToString('N') }
  $l = [math]::Round($img.W * $border[0]); $bo = [math]::Round($img.H * $border[1]); $r = [math]::Round($img.W * $border[2]); $t = [math]::Round($img.H * $border[3])
  $text = $template -replace 'guid: [0-9a-f]{32}', "guid: $guid" -replace 'spriteBorder: \{[^}]*\}', "spriteBorder: {x: $l, y: $bo, z: $r, w: $t}" `
    -replace 'maxTextureSize: 2048', 'maxTextureSize: 1024' -replace 'textureCompression: 0', 'textureCompression: 2'
  [IO.File]::WriteAllText($meta, $text.Replace("`r`n", "`n"))
  return "{0}x{1} border L{2} B{3} R{4} T{5}" -f $img.W, $img.H, $l, $bo, $r, $t
}
function Folder-Meta([string]$dir) {
  if (Test-Path "$dir.meta") { return }
  [IO.File]::WriteAllText("$dir.meta", "fileFormatVersion: 2`nguid: $([guid]::NewGuid().ToString('N'))`nfolderAsset: yes`nDefaultImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n")
}
$dirs = @{ R = Join-Path $repo 'Assets\Resources\ComicPack'; A = Join-Path $repo 'Assets\Art\UI\ComicPack' }
foreach ($d in $dirs.Values) { New-Item -ItemType Directory -Force $d | Out-Null; Folder-Meta $d }

foreach ($it in $items) {
  $src = Join-Path $Pack $it.s
  $img = [ComicPack]::Crop([ComicPack]::Load($src), 6)
  if ($it.op -eq 'sidebar') {
    # Smudge rectangle measured on the source (887x1774, before the crop): x 200-690, y 150-280.
    $full = [ComicPack]::Load($src)
    Write-Output ("sidebar_navy: " + [ComicPack]::PaintOver($full, 200, 150, 690, 280, 70))
    $img = [ComicPack]::Crop($full, 6)
  }
  $h = [int]([math]::Round($it.w * $img.H / $img.W / 4) * 4)
  $img = [ComicPack]::Resize($img, $it.w, $h)
  $dir = $dirs[$it.d]
  if ($it.op -eq 'blue') { $img = [ComicPack]::Hue($img, 46, 1.30, 1.10) }
  if ($it.op -eq 'green') { $img = [ComicPack]::Hue($img, -52, 1.35, 1.02) }
  if ($it.op -eq 'states' -or $it.op -eq 'blue') {
    Write-Output ("{0}_normal: {1}" -f $it.n, (Write-Sprite $img (Join-Path $dir ($it.n + '_normal.png')) $it.b))
    foreach ($m in 'hover', 'pressed', 'disabled') { $null = Write-Sprite ([ComicPack]::State($img, $m)) (Join-Path $dir ("{0}_{1}.png" -f $it.n, $m)) $it.b }
  } else {
    Write-Output ("{0}: {1}" -f $it.n, (Write-Sprite $img (Join-Path $dir ($it.n + '.png')) $it.b))
  }
  if ($it.op -eq 'card') {
    # Shop card states from the same silhouette: hover lighter, pressed darker, selected warm gold paper.
    $null = Write-Sprite ([ComicPack]::State($img, 'hover')) (Join-Path $dir ($it.n + '_hover.png')) $it.b
    $null = Write-Sprite ([ComicPack]::State($img, 'pressed')) (Join-Path $dir ($it.n + '_pressed.png')) $it.b
    $null = Write-Sprite ([ComicPack]::Tint($img, 255, 196, 84, .62)) (Join-Path $dir ($it.n + '_selected.png')) $it.b
  }
  if ($it.op -eq 'bar') {
    $log = ''; $fill = [ComicPack]::BarFill($img, [ref]$log)
    Write-Output ("bar_fill: " + (Write-Sprite $fill (Join-Path $dir 'bar_fill.png') $it.b) + " " + $log)
  }
}
