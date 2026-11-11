# HUD kit slicer (6 Nov 2026). Source: Docs/HUD-Toon/Kaynak/ui_kit_gpt.png (user-made with ChatGPT image generation, 4x3 grid of
# card-style UI pieces on a transparent background). No external tools (Windows .NET System.Drawing).
#   powershell -ExecutionPolicy Bypass -File Tools/Art/HudKit/hudkit_slice.ps1
# Output: Assets/Resources/HudKit/hud_<name>.png, one file per piece (tight bounds + 3 px padding).
# Bars (third row) also get two derived files for an animated fill:
#   hud_<bar>_frame.png : the bar with its interior painted the flat empty grey,
#   hud_<bar>_fill.png  : only the interior, filled with the bar colour (halftone kept), transparent elsewhere.
# The interior is found by flood fill from a fill pixel through non-ink pixels (the inner ink line encloses it).
param([int]$Threshold = 16, [int]$MinCount = 3, [int]$Pad = 3)
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class HudKit
{
    static List<int[]> Runs(int[] count, int length, int minCount)
    {
        var runs = new List<int[]>(); int start = -1;
        for (int i = 0; i <= length; i++)
        {
            bool on = i < length && count[i] >= minCount;
            if (on && start < 0) start = i;
            if (!on && start >= 0) { runs.Add(new[] { start, i }); start = -1; }
        }
        return runs;
    }
    static byte[] Read(Bitmap b, out int stride)
    {
        var d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        stride = d.Stride; var px = new byte[stride * b.Height]; Marshal.Copy(d.Scan0, px, 0, px.Length); b.UnlockBits(d); return px;
    }
    static void Write(byte[] px, int w, int h, string path)
    {
        using (var b = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            var d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(px, 0, d.Scan0, px.Length); b.UnlockBits(d); b.Save(path, ImageFormat.Png);
        }
    }
    public static string Slice(string source, string outDir, string[] names, int threshold, int minCount, int pad)
    {
        var log = new System.Text.StringBuilder();
        using (var src = new Bitmap(source))
        {
            int W = src.Width, H = src.Height, stride; byte[] px = Read(src, out stride);
            Func<int, int, int> A = (x, y) => px[y * stride + x * 4 + 3];
            var rows = new int[H];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (A(x, y) > threshold) rows[y]++;
            int index = 0;
            foreach (var band in Runs(rows, H, minCount))
            {
                var cols = new int[W];
                for (int y = band[0]; y < band[1]; y++) for (int x = 0; x < W; x++) if (A(x, y) > threshold) cols[x]++;
                foreach (var run in Runs(cols, W, 1))
                {
                    if (run[1] - run[0] < 20) continue;
                    // Tight vertical bounds of this piece.
                    int y0 = int.MaxValue, y1 = -1;
                    for (int y = band[0]; y < band[1]; y++) for (int x = run[0]; x < run[1]; x++) if (A(x, y) > threshold) { if (y < y0) y0 = y; y1 = y; }
                    int x0 = Math.Max(0, run[0] - pad), x1 = Math.Min(W, run[1] + pad), ya = Math.Max(0, y0 - pad), yb = Math.Min(H, y1 + 1 + pad);
                    int w = x1 - x0, h = yb - ya;
                    var crop = new byte[w * h * 4];
                    for (int y = 0; y < h; y++) Buffer.BlockCopy(px, (ya + y) * stride + x0 * 4, crop, y * w * 4, w * 4);
                    string name = index < names.Length ? names[index] : ("piece" + index);
                    Write(crop, w, h, System.IO.Path.Combine(outDir, "hud_" + name + ".png"));
                    log.AppendLine(string.Format("{0}: {1}x{2} at ({3},{4})", name, w, h, x0, ya));
                    if (name.StartsWith("bar_")) log.AppendLine(Bar(crop, w, h, outDir, name));
                    index++;
                }
            }
        }
        return log.ToString();
    }
    static string Bar(byte[] px, int w, int h, string outDir, string name)
    {
        Func<int, float> Lum = i => (px[i + 2] * .299f + px[i + 1] * .587f + px[i] * .114f);
        Func<int, float> Sat = i => { int r = px[i + 2], g = px[i + 1], b = px[i]; int mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)); return mx == 0 ? 0 : (mx - mn) / (float)mx; };
        Func<int, bool> Ink = i => px[i + 3] < 128 || Lum(i) < 70f;
        // Seed: the most saturated pixel on the middle row, left third (the fill always starts at the left).
        int seed = -1; float best = 0;
        int my = h / 2;
        for (int x = w / 10; x < w / 3; x++) { int i = (my * w + x) * 4; if (!Ink(i) && Sat(i) > best) { best = Sat(i); seed = my * w + x; } }
        if (seed < 0) return "  no seed";
        var inside = new bool[w * h]; var q = new Queue<int>(); q.Enqueue(seed); inside[seed] = true; int count = 0;
        while (q.Count > 0)
        {
            int p = q.Dequeue(); count++; int x = p % w, y = p / w;
            int[] n = { p - 1, p + 1, p - w, p + w };
            for (int k = 0; k < 4; k++)
            {
                int c = n[k]; if (c < 0 || c >= w * h) continue;
                if ((k == 0 && x == 0) || (k == 1 && x == w - 1)) continue;
                if (inside[c] || Ink(c * 4)) continue;
                inside[c] = true; q.Enqueue(c);
            }
        }
        // Close small holes (halftone dots, specks along the inner edge): a pixel enclosed by interior pixels on all four sides
        // within 5 px joins the interior. The inner ink line has interior on one side only, so it stays.
        var closed = (bool[])inside.Clone();
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            int p = y * w + x; if (inside[p]) continue;
            bool l = false, r = false, u = false, d = false;
            for (int k = 1; k <= 5; k++)
            {
                if (!l && x - k >= 0 && inside[p - k]) l = true;
                if (!r && x + k < w && inside[p + k]) r = true;
                if (!u && y - k >= 0 && inside[p - k * w]) u = true;
                if (!d && y + k < h && inside[p + k * w]) d = true;
            }
            if (l && r && u && d) { closed[p] = true; count++; }
        }
        inside = closed;
        // Fill colour: average of saturated interior pixels; empty colour: average of light unsaturated interior pixels.
        double fr = 0, fg = 0, fb = 0, er = 0, eg = 0, eb = 0; int fn = 0, en = 0;
        for (int p = 0; p < w * h; p++)
        {
            if (!inside[p]) continue; int i = p * 4;
            if (Sat(i) > .35f) { fr += px[i + 2]; fg += px[i + 1]; fb += px[i]; fn++; }
            else if (Lum(i) > 150f) { er += px[i + 2]; eg += px[i + 1]; eb += px[i]; en++; }
        }
        if (fn == 0 || en == 0) return "  interior colours not found";
        byte FR = (byte)(fr / fn), FG = (byte)(fg / fn), FB = (byte)(fb / fn), ER = (byte)(er / en), EG = (byte)(eg / en), EB = (byte)(eb / en);
        // Where the original fill ends on each row (last saturated interior pixel): the shaded end edge is replaced by the plain fill
        // colour, otherwise it would show as a line in the middle of a growing fill. Only the left part keeps its halftone.
        var fillEnd = new int[h];
        for (int y = 0; y < h; y++) { fillEnd[y] = -1; for (int x = 0; x < w; x++) { int p = y * w + x; if (inside[p] && Sat(p * 4) > .35f) fillEnd[y] = x; } }
        var frame = (byte[])px.Clone(); var fill = new byte[px.Length];
        for (int p = 0; p < w * h; p++)
        {
            if (!inside[p]) continue; int i = p * 4, x = p % w, y = p / w;
            frame[i] = EB; frame[i + 1] = EG; frame[i + 2] = ER; frame[i + 3] = 255;
            bool keep = x < fillEnd[y] - 10 && x < w / 3 && (Sat(i) > .35f || Lum(i) < 150f);   // left halftone dots only
            fill[i] = keep ? px[i] : FB; fill[i + 1] = keep ? px[i + 1] : FG; fill[i + 2] = keep ? px[i + 2] : FR; fill[i + 3] = 255;
        }
        Write(frame, w, h, System.IO.Path.Combine(outDir, "hud_" + name + "_frame.png"));
        Write(fill, w, h, System.IO.Path.Combine(outDir, "hud_" + name + "_fill.png"));
        return string.Format("  interior {0} px ({1:0.0}% of the box), fill rgb {2},{3},{4}, empty rgb {5},{6},{7}", count, 100.0 * count / (w * h), FR, FG, FB, ER, EG, EB);
    }
}
"@
$source = Join-Path $repo 'Docs\HUD-Toon\Kaynak\ui_kit_gpt.png'
$out = Join-Path $repo 'Assets\Resources\HudKit'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$names = @('panel_blue', 'panel_cream', 'panel_yellow', 'plate_navy', 'plate_red', 'plate_grey', 'bar_blue', 'bar_yellow', 'bar_red', 'card_tab_cream', 'banner_navy', 'slider_blue')
[HudKit]::Slice($source, $out, $names, $Threshold, $MinCount, $Pad)
