using System.Collections.Generic;
using UnityEngine;

public sealed class ElectricBurst : MonoBehaviour
{
    [SerializeField] Material boltMaterial;
    [SerializeField] float lifetime = .28f;
    readonly LineRenderer[] bolts = new LineRenderer[16];
    readonly Vector3[] start = new Vector3[8], end = new Vector3[8];
    HarvestBehaviorManager owner;
    PlanterBrain source;
    int count;
    float age;
    void Awake()
    {
        for (int i = 0; i < bolts.Length; i++)
        {
            var go = new GameObject(i % 2 == 0 ? "Electric cyan rim" : "Electric white core");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>(); bolts[i] = line;
            line.sharedMaterial = boltMaterial; line.useWorldSpace = true; line.positionCount = 9;
            line.numCapVertices = 3; line.numCornerVertices = 2;
            line.startWidth = line.endWidth = i % 2 == 0 ? .17f : .055f;
            line.startColor = line.endColor = i % 2 == 0 ? new Color(.08f, .7f, 1f) : new Color(.88f, 1f, 1f);
            line.enabled = false;
        }
    }
    public void Launch(HarvestBehaviorManager manager, PlanterBrain planter, GridSystem grid,
        IReadOnlyList<GridPosition> targets, IReadOnlyList<GridPosition> origins, int damage)
    {
        owner = manager; source = planter; age = 0; count = 0;
        for (int i = 0; i < targets.Count && count < 8; i++)
        {
            var entry = grid.GetGridObject(targets[i]); var cell = entry?.GetGroundCellCached();
            var origin = grid.GetGridObject(origins[i])?.GetGroundCellCached();
            if (cell == null || cell.IsLocked || origin == null || entry.GetPlanterBrain() == planter) continue;
            start[count] = origin.transform.position + Vector3.up * .8f;
            end[count] = cell.transform.position + Vector3.up * .8f;
            bolts[count * 2].enabled = bolts[count * 2 + 1].enabled = true; count++;
            var plant = entry.GetPlantObject();
            if (plant != null && plant.TryGetComponent<PlantHealth>(out var health) && !health.IsDead)
            {
                Vector3 point = plant.transform.position;
                health.TakeDamage(damage, DamageType.Electric);
                VFXManager.Instance?.PlayHit(point, damage, false);
            }
        }
        Draw();
    }
    void Update()
    {
        if (owner == null) return;
        if (source == null || source.OccupiedGrids.Count == 0 || RoundManager.Instance == null || !RoundManager.Instance.IsRoundActive)
        { owner.Release(this); return; }
        age += Time.deltaTime;
        if (age >= lifetime) { owner.Release(this); return; }
        Draw();
    }
    void Draw()
    {
        for (int b = 0; b < count; b++)
        {
            var side = Vector3.Cross(Vector3.up, end[b] - start[b]).normalized;
            float fade = 1f - age / lifetime;
            for (int j = 0; j < 9; j++)
            {
                float t = j / 8f;
                // Deterministic visual jitter: does not consume gameplay Random state.
                float jitter = j == 0 || j == 8 ? 0 : Mathf.Sin(j * 18.7f + b * 3.1f + Mathf.Floor(age * 30) * 7.3f) * .22f;
                var pos = Vector3.Lerp(start[b], end[b], t) + side * jitter;
                bolts[b * 2].SetPosition(j, pos); bolts[b * 2 + 1].SetPosition(j, pos);
            }
            bolts[b * 2].startWidth = bolts[b * 2].endWidth = .17f * fade;
            bolts[b * 2 + 1].startWidth = bolts[b * 2 + 1].endWidth = .055f * fade;
        }
    }
    void OnDisable()
    {
        foreach (var bolt in bolts) if (bolt != null) bolt.enabled = false;
        count = 0; owner = null; source = null;
    }
}
