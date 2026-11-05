# Eri'nin hal tabloları (Faz 3, 5 Kasım 2026): kare sınırlarını ölçer, konuşma kutusu silüetlerini üretir. Dış araç yok (Windows'un
# .NET System.Drawing'i).
#   powershell -ExecutionPolicy Bypass -File Tools/Art/EriMoods/eri_frames.ps1
# Çıktı:
#   - Ekrana MascotFrames için C# sınır dizileri: kare başına (x0, y0, x1, y1), kaynak piksel, y yukarıdan, bitiş dahil değil.
#     Kareler düzgün ızgarada değil: önce boş satır aralıklarından 4 sıra, her sırada boş sütunlardan 4 kare bulunur.
#     Bugünkü tabloda (GreenhouseMascotFrames.png) koddaki elle ölçülmüş sınırlarla 1–2 piksel içinde örtüştüğü doğrulandı.
#   - Kubbe ölçüsü: kare 0'ın üst %55'indeki en sağ opak sütunun dolgulu kare genişliğine oranı (DialogueBox.DomeEdgeMax ile kıyas).
#   - Assets/Resources/Dialogue/EriSilhouette_<Hal>.png: kare 0'ın dolgulu (2 px) kesiti, 1254'lük referans ölçeğe küçültülmüş,
#     renk beyaz, saydamlık ×3 (bugünkü EriSilhouette.png ile aynı yöntem).
param([int]$Threshold = 8, [int]$MinCount = 3, [int]$MergeGap = 6)
$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class EriFrames
{
    static List<int[]> Runs(int[] count, int length, int minCount, int mergeGap)
    {
        var runs = new List<int[]>();
        int start = -1, lastOn = -1;
        for (int i = 0; i < length; i++)
        {
            if (count[i] < minCount) continue;
            if (start < 0) start = i;
            else if (i - lastOn > mergeGap) { runs.Add(new[] { start, lastOn }); start = i; }
            lastOn = i;
        }
        if (start >= 0) runs.Add(new[] { start, lastOn });
        return runs;
    }

    // Döndürür: 16 sınır (x0, y0, x1 hariç, y1 hariç) ve kubbe oranı.
    public static int[][] Measure(string path, int t, int minCount, int mergeGap, out double dome, out int size)
    {
        using (var bmp = new Bitmap(path))
        {
            int w = bmp.Width, h = bmp.Height; size = w;
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var px = new byte[data.Stride * h]; Marshal.Copy(data.Scan0, px, 0, px.Length); int stride = data.Stride; bmp.UnlockBits(data);
            Func<int, int, bool> A = (x, y) => px[y * stride + x * 4 + 3] > t;
            var rows = new int[h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (A(x, y)) rows[y]++;
            var bands = Runs(rows, h, minCount, mergeGap);
            if (bands.Count != 4) throw new Exception(path + ": " + bands.Count + " sıra bulundu (4 bekleniyor)");
            var result = new List<int[]>(); dome = 0;
            foreach (var band in bands)
            {
                var cols = new int[w];
                for (int y = band[0]; y <= band[1]; y++) for (int x = 0; x < w; x++) if (A(x, y)) cols[x]++;
                var segs = Runs(cols, w, minCount, mergeGap);
                if (segs.Count != 4) throw new Exception(path + ": bir sırada " + segs.Count + " kare bulundu (4 bekleniyor)");
                foreach (var seg in segs)
                {
                    int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
                    for (int y = band[0]; y <= band[1]; y++) for (int x = seg[0]; x <= seg[1]; x++)
                        if (A(x, y)) { if (x < x0) x0 = x; if (y < y0) y0 = y; if (x > x1) x1 = x; if (y > y1) y1 = y; }
                    if (result.Count == 0)
                    {
                        int fx0 = x0 - 2, fw = x1 - x0 + 4, top = y0 + (int)((y1 - y0) * 0.55f), right = -1;
                        for (int y = y0; y <= top; y++) for (int x = x1; x >= x0; x--) if (A(x, y)) { if (x > right) right = x; break; }
                        dome = (right - fx0) / (double)fw;
                    }
                    result.Add(new[] { x0, y0, x1 + 1, y1 + 1 });
                }
            }
            return result.ToArray();
        }
    }

    // Kare 0'ın dolgulu kesiti, referans ölçeğe (1254) küçültülür; beyaz, saydamlık ×3.
    public static void Silhouette(string path, int[] b, int size, string output)
    {
        using (var bmp = new Bitmap(path))
        {
            var crop = new Rectangle(b[0] - 2, b[1] - 2, b[2] - b[0] + 4, b[3] - b[1] + 4);
            double scale = 1254.0 / size;
            int ow = (int)Math.Round(crop.Width * scale), oh = (int)Math.Round(crop.Height * scale);
            using (var small = new Bitmap(ow, oh, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(small))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(bmp, new Rectangle(0, 0, ow, oh), crop, GraphicsUnit.Pixel);
                }
                var data = small.LockBits(new Rectangle(0, 0, ow, oh), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                var px = new byte[data.Stride * oh]; Marshal.Copy(data.Scan0, px, 0, px.Length);
                for (int i = 0; i < px.Length; i += 4) { px[i] = 255; px[i + 1] = 255; px[i + 2] = 255; px[i + 3] = (byte)Math.Min(255, px[i + 3] * 3); }
                Marshal.Copy(px, 0, data.Scan0, px.Length); small.UnlockBits(data);
                small.Save(output, ImageFormat.Png);
            }
        }
    }
}
"@
foreach ($mood in @('Mutlu', 'Uzgun', 'Seytani')) {
    $sheet = Join-Path $repo "Assets\Resources\GreenhouseMascotFrames_$mood.png"
    $dome = 0.0; $size = 0
    $b = [EriFrames]::Measure($sheet, $Threshold, $MinCount, $MergeGap, [ref]$dome, [ref]$size)
    $lines = for ($r = 0; $r -lt 4; $r++) { ($b[($r*4)..($r*4+3)] | ForEach-Object { "new Vector4($($_ -join ','))" }) -join ',' }
    "// $mood ($size px), kubbe {0:0.000}" -f $dome
    "{" + ($lines -join ",`n ") + "}"
    [EriFrames]::Silhouette($sheet, $b[0], $size, (Join-Path $repo "Assets\Resources\Dialogue\EriSilhouette_$mood.png"))
}
