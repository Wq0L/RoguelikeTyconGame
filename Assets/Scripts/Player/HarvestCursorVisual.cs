using UnityEngine;

// One persistent visual, never allocated per attack. Uses the same authored scythe.
public sealed class HarvestCursorVisual : MonoBehaviour
{
    [SerializeField] Transform scythe;
    [SerializeField] Material overlayMaterial;
    readonly LineRenderer[] wind = new LineRenderer[3];
    float angle;
    Vector3 grip;
    Quaternion flatRotation = Quaternion.identity;
    float modelLength = 1.8f;
    // Saldırı anı: dönüş hızlanır, tırpan büyür, rüzgar izleri parlar. Crit'te turuncuya döner.
    float pulse;
    bool pulseCrit;
    TrailRenderer trail;
    static readonly Color WindStart = new Color(.3f, 1f, .9f, .6f), WindEnd = new Color(.6f, 1f, 1f, .05f);
    static readonly Color CritStart = new Color(1f, .62f, .2f, .9f), HitStart = new Color(.85f, 1f, 1f, .85f);
    // Hazır Hasat Ritmi hakkı: rüzgar izleri altın rengine döner (küçük vurgu; ek saldırı üretmez).
    static readonly Color ChargedStart = new Color(1f, .86f, .28f, .9f);
    bool charged;
    public bool Charged => charged;
    public void SetCharged(bool value) => charged = value;
    void Awake()
    {
        if (scythe != null)
        {
            var points = new System.Collections.Generic.List<Vector3>();
            foreach (var filter in scythe.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh != null)
                    foreach (var vertex in filter.sharedMesh.vertices)
                        points.Add(scythe.InverseTransformPoint(filter.transform.TransformPoint(vertex)));
            if (points.Count > 0)
            {
                var bounds = new Bounds(points[0], Vector3.zero);
                foreach (var point in points) bounds.Encapsulate(point);
                int axis = bounds.size.x > bounds.size.y ? 0 : 1;
                if (bounds.size.z > bounds.size[axis]) axis = 2;
                int normal = bounds.size.x < bounds.size.y ? 0 : 1;
                if (bounds.size.z < bounds.size[normal]) normal = 2;
                modelLength = Mathf.Max(.01f, bounds.size[axis]);
                Vector3 low = Vector3.zero, high = Vector3.zero; int lows = 0, highs = 0;
                Bounds lowBounds = default, highBounds = default;
                foreach (var point in points)
                {
                    if (point[axis] < bounds.min[axis] + modelLength * .06f)
                    { if (lows++ == 0) lowBounds = new Bounds(point, Vector3.zero); lowBounds.Encapsulate(point); low += point; }
                    if (point[axis] > bounds.max[axis] - modelLength * .06f)
                    { if (highs++ == 0) highBounds = new Bounds(point, Vector3.zero); highBounds.Encapsulate(point); high += point; }
                }
                // The grip end is narrower than the blade/head end.
                bool lowEnd = lowBounds.size.sqrMagnitude < highBounds.size.sqrMagnitude;
                grip = lowEnd ? low / lows : high / highs;
                grip[axis] = lowEnd ? bounds.min[axis] : bounds.max[axis];
                Vector3 planeNormal = Vector3.zero; planeNormal[normal] = 1;
                flatRotation = Quaternion.FromToRotation(planeNormal, Vector3.up);
            }
        }
        for (int j = 0; j < wind.Length; j++)
        {
            var go = new GameObject("Cursor wind " + j); go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>(); wind[j] = line;
            line.sharedMaterial = overlayMaterial; line.positionCount = 18; line.useWorldSpace = false;
            line.startWidth = .055f; line.endWidth = .005f;
            line.startColor = WindStart; line.endColor = WindEnd;
        }
        // İmleç hareket izi: hızlı savururken arkasında kısa bir kuyruk bırakır.
        var trailObject = new GameObject("Cursor trail"); trailObject.transform.SetParent(transform, false);
        trail = trailObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = overlayMaterial; trail.time = .14f; trail.minVertexDistance = .05f;
        trail.widthMultiplier = .22f; trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f); trail.numCapVertices = 2;
        trail.startColor = new Color(.35f, 1f, .9f, .45f); trail.endColor = new Color(.6f, 1f, 1f, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trail.receiveShadows = false;
    }
    // Round dışında kapalı kalır; tekrar açılınca eski konumdan çizgi çekmesin.
    void OnEnable() { if (trail != null) trail.Clear(); }
    public void Pulse(bool crit) { pulse = 1f; pulseCrit = crit; }
    public void Show(Vector3 center, float radius)
    {
        transform.position = center + Vector3.up * .12f;
        pulse = Mathf.Max(0f, pulse - Time.deltaTime / .18f);
        float kick = pulse * pulse;
        angle -= Time.deltaTime * 540f * (1f + 2.2f * kick);
        if (scythe != null)
        {
            // Resolve the imported mesh plane and grip once; keep that grip at the cursor.
            float size = Mathf.Max(.6f, radius / modelLength) * (1f + .18f * kick);
            scythe.localScale = Vector3.one * size;
            scythe.localRotation = Quaternion.Euler(0, -angle, 0) * flatRotation;
            scythe.localPosition = -(scythe.localRotation * (grip * size));
        }
        Color start = Color.Lerp(charged ? ChargedStart : WindStart, pulseCrit ? CritStart : HitStart, kick);
        for (int j = 0; j < wind.Length; j++)
        {
            wind[j].startColor = start;
            wind[j].startWidth = .055f * (1f + 1.6f * kick);
        }
        for (int j = 0; j < wind.Length; j++) for (int i = 0; i < 18; i++)
        {
            float t = i / 17f, a = (angle + j * 120f - t * 65f) * Mathf.Deg2Rad;
            wind[j].SetPosition(i, new Vector3(Mathf.Cos(a), .025f, Mathf.Sin(a)) * (radius * (.85f + j * .04f)));
        }
    }
}
