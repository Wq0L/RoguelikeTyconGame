using System.Collections.Generic;
using UnityEngine;

// Shared circle/square contact geometry for gameplay and the three canonical aim samples used by Sis/measurements.
// BestCells samples cell centre, edge midpoint and corner; it is not an exhaustive optimum over all possible aim points.
public static class HarvestArea
{
    // Squared distance from circle center to the closest point on the cell's square surface (XZ).
    public static float DistanceToCell(Vector2 offset, float cellSize)
    {
        float half = cellSize * .5f;
        float x = Mathf.Max(0f, Mathf.Abs(offset.x) - half);
        float z = Mathf.Max(0f, Mathf.Abs(offset.y) - half);
        return x * x + z * z;
    }

    public static bool TouchesCell(Vector3 center, Vector3 cell, float radius, float cellSize)
    {
        radius = Mathf.Max(0f, radius);
        return DistanceToCell(new Vector2(center.x - cell.x, center.z - cell.z), cellSize) <= radius * radius + 1e-6f;
    }
    // Nişan türleri (hücre birimiyle): hücre merkezi, iki hücrenin arası, dört hücrenin köşesi.
    private static readonly Vector2[] Aims = { new Vector2(0f, 0f), new Vector2(.5f, 0f), new Vector2(.5f, .5f) };

    // Dolu tarlada en iyi nişanın vurduğu hücre sayısı.
    public static int BestCells(float radius, float cellSize)
    {
        if (cellSize <= 0f) return 0;
        float reach = Mathf.Max(0f, radius) / cellSize;
        int span = Mathf.CeilToInt(reach) + 1, best = 0;
        foreach (Vector2 aim in Aims)
        {
            int count = 0;
            for (int x = -span; x <= span; x++)
                for (int z = -span; z <= span; z++)
                    if (DistanceToCell(new Vector2(x - aim.x, z - aim.y), 1f) <= reach * reach + 1e-6f) count++;
            if (count > best) best = count;
        }
        return best;
    }

    // En iyi nişanın hedef sayısını bir basamak düşüren en büyük yarıçap (verilen yarıçaptan küçük). Düşecek basamak yoksa aynı yarıçap.
    public static float StepDownRadius(float radius, float cellSize)
    {
        if (cellSize <= 0f || radius <= 0f) return radius;
        int current = BestCells(radius, cellSize);
        float reach = (radius + cellSize * .5f) / cellSize;
        int span = Mathf.CeilToInt(reach) + 1;
        // Sayının değişebildiği yarıçaplar: her nişan türünde bir hücre merkezinin tam erişim sınırına geldiği değerler.
        var thresholds = new List<float>();
        foreach (Vector2 aim in Aims)
            for (int x = -span; x <= span; x++)
                for (int z = -span; z <= span; z++)
                {
                    float threshold = Mathf.Sqrt(DistanceToCell(new Vector2(x - aim.x, z - aim.y), 1f)) * cellSize;
                    if (threshold > 0f && threshold <= radius + 1e-4f) thresholds.Add(threshold);
                }
        thresholds.Sort();
        for (int i = thresholds.Count - 1; i >= 0; i--)
        {
            float candidate = thresholds[i] - .01f;
            if (candidate > 0f && BestCells(candidate, cellSize) < current) return candidate;
        }
        return radius;
    }
}
