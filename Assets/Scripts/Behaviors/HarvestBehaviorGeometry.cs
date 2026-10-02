using System.Collections.Generic;
using UnityEngine;

public static class HarvestBehaviorGeometry
{
    public static Vector3 Arc(Vector3 from, Vector3 to, float t, float bend)
    {
        Vector3 side = Vector3.Cross(Vector3.up, to - from).normalized;
        return Vector3.Lerp(from, to, t) + side * (Mathf.Sin(Mathf.PI * t) * bend);
    }

    public static float SegmentDistanceSquared(Vector3 point, Vector3 a, Vector3 b)
    {
        point.y = a.y = b.y = 0;
        Vector3 delta = b - a;
        float t = delta.sqrMagnitude < .0001f ? 0 : Mathf.Clamp01(Vector3.Dot(point - a, delta) / delta.sqrMagnitude);
        return (point - a - delta * t).sqrMagnitude;
    }

    // Patlamanın yarıçapı, hücre birimiyle ve hücre merkezleri arasında ölçülür. Normal patlama (PlanterBrain.TryExplode) saksının
    // dört yöndeki komşularını vurur: merkezler arası 1 hücre; çapraz komşular 1,41 hücre uzaktadır. 1,2 bu hedef kümesini birebir
    // verir (1 ≤ 1,2 < 1,41). Artçı Patlama bu yarıçapı ödülün oranıyla çarpar: ×1,25 = 1,5 hücre → çapraz komşular da.
    public const float ExplosionRadiusCells = 1.2f;

    // Ayak izindeki herhangi bir hücrenin merkezine radiusCells içinde olan, ayak izinin dışındaki hücreler (tekrarsız).
    public static void ExplosionCells(IReadOnlyList<GridPosition> footprint, float radiusCells, List<GridPosition> targets)
    {
        targets.Clear();
        if (footprint == null || radiusCells <= 0f) return;
        int span = Mathf.FloorToInt(radiusCells + 1e-4f);
        float limit = radiusCells * radiusCells + 1e-4f;
        foreach (var cell in footprint)
            for (int dx = -span; dx <= span; dx++)
            for (int dz = -span; dz <= span; dz++)
            {
                if ((dx == 0 && dz == 0) || dx * dx + dz * dz > limit) continue;
                var next = new GridPosition(cell.x + dx, cell.z + dz);
                bool skip = false;
                foreach (var p in footprint) if (p.x == next.x && p.z == next.z) { skip = true; break; }
                if (!skip) foreach (var p in targets) if (p.x == next.x && p.z == next.z) { skip = true; break; }
                if (!skip) targets.Add(next);
            }
    }

    // Four diagonal rays start at the footprint corners, never inside the planter.
    public static void ElectricCells(IReadOnlyList<GridPosition> footprint, List<GridPosition> targets, List<GridPosition> origins)
    {
        targets.Clear(); origins.Clear();
        if (footprint.Count == 0) return;
        for (int dx = -1; dx <= 1; dx += 2)
        for (int dz = -1; dz <= 1; dz += 2)
        {
            GridPosition corner = footprint[0];
            foreach (var p in footprint)
                if (p.x * dx + p.z * dz > corner.x * dx + corner.z * dz) corner = p;
            for (int step = 1; step <= 2; step++)
            {
                var next = new GridPosition(corner.x + dx * step, corner.z + dz * step);
                bool inside = false, duplicate = false;
                foreach (var p in footprint) if (p.x == next.x && p.z == next.z) inside = true;
                foreach (var p in targets) if (p.x == next.x && p.z == next.z) duplicate = true;
                if (inside || duplicate) continue;
                targets.Add(next); origins.Add(corner);
            }
        }
    }
}
