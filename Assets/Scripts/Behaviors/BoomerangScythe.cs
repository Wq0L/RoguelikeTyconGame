using System.Collections.Generic;
using UnityEngine;

public sealed class BoomerangScythe : MonoBehaviour
{
    [SerializeField] Transform visual;
    [SerializeField] TrailRenderer trail;
    [SerializeField, Min(.1f)] float travelSpeed = 12f;
    [SerializeField, Min(0)] float damageMultiplier = .65f;
    readonly HashSet<PlantHealth> hitThisLeg = new();
    HarvestBehaviorManager owner;
    PlanterBrain source;
    Vector3 from, to;
    float progress, duration;
    int damage;
    bool returning;

    public void Launch(HarvestBehaviorManager manager, PlanterBrain planter, Vector3 start, Vector3 destination, int baseDamage)
    {
        owner = manager; source = planter; returning = false; progress = 0;
        from = start + Vector3.up * .8f; to = destination + Vector3.up * .8f;
        float distance = Vector3.Distance(from, to);
        duration = Mathf.Clamp(distance / travelSpeed, .4f, 1.6f);
        damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * damageMultiplier));
        hitThisLeg.Clear(); transform.position = from; transform.rotation = Quaternion.identity;
        if (visual != null) visual.localRotation = Quaternion.identity;
        if (trail != null) { trail.Clear(); trail.emitting = true; }
    }
    void Update()
    {
        if (owner == null) return;
        if (source == null || source.OccupiedGrids.Count == 0 || RoundManager.Instance == null || !RoundManager.Instance.IsRoundActive)
        { owner.Release(this); return; }
        if (Time.deltaTime <= 0) return;
        if (visual != null) visual.Rotate(Vector3.up, 1000f * Time.deltaTime, Space.Self);
        float remaining = Time.deltaTime / duration;
        // Sweep segments so low FPS cannot skip plants or the return turn.
        while (remaining > 0)
        {
            float step = Mathf.Min(remaining, .035f, 1f - progress);
            Vector3 previous = transform.position;
            progress += step; remaining -= step;
            transform.position = Vector3.Lerp(returning ? to : from, returning ? from : to, progress);
            HitSegment(previous, transform.position);
            if (progress < .99999f) continue;
            if (returning) { owner.Release(this); return; }
            returning = true; progress = 0; hitThisLeg.Clear();
        }
    }
    void HitSegment(Vector3 a, Vector3 b)
    {
        var manager = GridManager.Instance; if (manager == null) return;
        var grid = manager.GetGridSystem();
        float radius = Vector3.Distance(grid.GetWorldPosition(0, 0), grid.GetWorldPosition(1, 0)) * .42f;
        for (int x = 0; x < manager.GetWidth(); x++) for (int z = 0; z < manager.GetHeight(); z++)
        {
            var entry = grid.GetGridObject(new GridPosition(x, z));
            var cell = entry?.GetGroundCellCached(); var plant = entry?.GetPlantObject();
            if (cell == null || cell.IsLocked || plant == null ||
                HarvestBehaviorGeometry.SegmentDistanceSquared(cell.transform.position, a, b) > radius * radius) continue;
            if (!plant.TryGetComponent<PlantHealth>(out var health) || health.IsDead || !hitThisLeg.Add(health)) continue;
            Vector3 point = plant.transform.position;
            health.TakeDamage(damage, DamageType.Boomerang);
            VFXManager.Instance?.PlayHit(point, damage, false);
        }
    }
    void OnDisable()
    {
        if (trail != null) { trail.emitting = false; trail.Clear(); }
        hitThisLeg.Clear(); owner = null; source = null; progress = 0;
    }
}
