using System.Collections.Generic;
using UnityEngine;

// EMEKLİ DENEY: kullanıcı kararıyla yük elektriği devre dışı. Geçmiş ölçüm kodunun derlenebilmesi için korunur.
// RoundManager bu bileşeni kurmaz; eski bir sahnede bulunursa da Mode daima KillChance döner.
// - Elektrik şansı > 0 olan her saksı aktif hasat süresinde (round açık, oyun durumu Round) yük doldurur; en fazla bir hazır yük.
//   Dolum süresi profilin ElectricChargeTuning eşlemesinden gelir (şans → süre); tempo dolumu da aynı oranda hızlandırır.
// - Oyuncunun o saksıdaki yaşayan bitkiye doğrudan vuruşu hazır yükü boşaltır (öldürmek şart değil). Aynı saldırı aynı saksıda
//   en fazla bir yük tüketir; farklı saksılar ayrı değerlendirilir. Davranış hasarı yük boşaltmaz. Boşalma mevcut elektrik yolundan
//   geçer: hedef geometrisi, hasar, rezonans/uzmanlaşma katsayısı, XP ve efekt havuzu kuralları aynıdır.
// - Round başında ve sonunda, yeni run'da, ana menüde, saksı kaldırılınca ve saksının elektrik şansı 0'a inince yük silinir.
//   Don Cephesi yalnız üretimi etkiler; yüke dokunmaz.
[DisallowMultipleComponent]
public sealed class ElectricChargeManager : MonoBehaviour
{
    public static ElectricChargeManager Instance { get; private set; }
    public static bool ChargeMode => Instance != null && Instance.Mode == ElectricTriggerMode.Charge;

    private sealed class Charge
    {
        public float progress;
        public bool ready;
        public MeshRenderer ring;
    }

    private const float RescanInterval = 0.5f;
    private readonly Dictionary<PlanterBrain, Charge> charges = new();
    private readonly List<PlanterBrain> planters = new(), scratch = new();
    private readonly Stack<MeshRenderer> freeRings = new();
    private RoundManager rounds;
    private GameManager game;
    private float rescanTimer;
    private Material ringMaterial;
    private Mesh quad;
    private MaterialPropertyBlock block;
    private bool ringsUnavailable;

    public ElectricTriggerMode Mode => ElectricTriggerMode.KillChance;
    public int Discharges { get; private set; }
    public int ChargingPlanters => charges.Count;
    public bool IsReady(PlanterBrain planter) => planter != null && charges.TryGetValue(planter, out Charge c) && c.ready;
    public float Progress(PlanterBrain planter) => planter != null && charges.TryGetValue(planter, out Charge c) ? c.progress : 0f;

    private void Awake()
    {
        Instance = this;
        rounds = GetComponent<RoundManager>();
    }

    private void OnEnable()
    {
        if (rounds == null) return;
        rounds.OnRunStarted += ClearAll;
        rounds.OnRoundChanged += HandleRoundStarted;
        rounds.OnRoundEnded += ClearAll;
    }

    private void OnDisable()
    {
        if (rounds != null)
        {
            rounds.OnRunStarted -= ClearAll;
            rounds.OnRoundChanged -= HandleRoundStarted;
            rounds.OnRoundEnded -= ClearAll;
        }
        if (game != null) game.OnGameStateChanged -= HandleStateChanged;
        game = null;
        ClearAll();
    }

    private void OnDestroy()
    {
        ClearAll();
        if (ringMaterial != null) Destroy(ringMaterial);
        if (quad != null) Destroy(quad);
        if (Instance == this) Instance = null;
    }

    private void HandleRoundStarted(int round) => ClearAll();

    private void HandleStateChanged(GameStates state)
    {
        if (state == GameStates.MainMenu || state == GameStates.RunSetup || state == GameStates.RunComplete) ClearAll();
    }

    private void Update()
    {
        if (game == null && GameManager.Instance != null)
        {
            game = GameManager.Instance;
            game.OnGameStateChanged += HandleStateChanged;
        }
        if (!ChargeMode) { if (charges.Count > 0) ClearAll(); return; }
        bool harvesting = rounds.IsRoundActive && game != null && game.CurrentState == GameStates.Round;
        if (harvesting)
        {
            rescanTimer -= Time.deltaTime;
            if (rescanTimer <= 0f) { rescanTimer = RescanInterval; Rescan(); }
            ElectricChargeTuning tuning = rounds.Profile.electricCharge;
            // Tempo (60 sn üstü süre) saldırı ve üretim gibi dolumu da hızlandırır; sabit süre koşulunda tempo 1'dir.
            float dt = Time.deltaTime * rounds.TempoMultiplier;
            for (int i = 0; i < planters.Count; i++)
            {
                PlanterBrain planter = planters[i];
                float chance = IsPlaced(planter) ? planter.GetFinalStat(StatType.ElectricChance) : 0f;
                if (chance <= 0f) { Remove(planter); continue; }
                if (!charges.TryGetValue(planter, out Charge charge)) charges[planter] = charge = new Charge();
                if (charge.ready) continue;
                charge.progress += dt / tuning.Seconds(chance);
                if (charge.progress >= 1f) { charge.progress = 1f; charge.ready = true; }
            }
        }
        DrawRings();
    }

    // Doğrudan isabet: saksının hazır yükü varsa boşaltır ve mevcut elektrik yolunu çalıştırır. Yük yoksa hiçbir şey olmaz.
    public static bool TryDischarge(PlanterBrain planter, int damage)
    {
        ElectricChargeManager manager = Instance;
        if (!ChargeMode || planter == null || !manager.charges.TryGetValue(planter, out Charge charge) || !charge.ready) return false;
        if (!IsPlaced(planter) || planter.GetFinalStat(StatType.ElectricChance) <= 0f) { manager.Remove(planter); return false; }
        charge.ready = false;
        charge.progress = 0f;
        manager.Discharges++;
        bool fired = HarvestBehaviorManager.Instance != null && HarvestBehaviorManager.Instance.TryElectric(planter, damage);
        HarvestBehaviorStats.Record(DamageType.Electric, fired);
        return true;
    }

    public void ClearAll()
    {
        foreach (Charge charge in charges.Values) ReleaseRing(charge);
        charges.Clear();
        planters.Clear();
        rescanTimer = 0f;
    }

    private static bool IsPlaced(PlanterBrain planter) =>
        planter != null && planter.isActiveAndEnabled && planter.OccupiedGrids.Count > 0;

    private void Rescan()
    {
        planters.Clear();
        foreach (PlanterBrain planter in FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None))
            if (IsPlaced(planter)) planters.Add(planter);
        scratch.Clear();
        foreach (PlanterBrain planter in charges.Keys) if (!planters.Contains(planter)) scratch.Add(planter);
        foreach (PlanterBrain planter in scratch) Remove(planter);
    }

    private void Remove(PlanterBrain planter)
    {
        if (planter is null || !charges.TryGetValue(planter, out Charge charge)) return;
        ReleaseRing(charge);
        charges.Remove(planter);
    }

    // ---------------- gösterim: saksı merkezinde küçük dolum halkası (hazırken nabız) ----------------
    private void DrawRings()
    {
        if (charges.Count == 0 || !EnsureRingResources()) return;
        block ??= new MaterialPropertyBlock();
        float cell = GridManager.Instance != null ? GridManager.Instance.GetCellSize() : 2f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
        scratch.Clear();
        foreach (KeyValuePair<PlanterBrain, Charge> entry in charges)
        {
            if (!IsPlaced(entry.Key)) { scratch.Add(entry.Key); continue; }
            Charge charge = entry.Value;
            if (charge.ring == null) charge.ring = TakeRing();
            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (GridObject grid in entry.Key.OccupiedGrids)
            {
                GroundCell ground = grid != null ? grid.GetGroundCellCached() : null;
                if (ground == null) continue;
                center += ground.transform.position;
                count++;
            }
            if (count == 0) { scratch.Add(entry.Key); continue; }
            Transform t = charge.ring.transform;
            t.position = center / count + Vector3.up * 0.08f;
            t.localScale = Vector3.one * cell * 0.9f;
            block.SetFloat("_Fill", charge.progress);
            block.SetFloat("_Ready", charge.ready ? 1f : 0f);
            block.SetFloat("_Pulse", pulse);
            charge.ring.SetPropertyBlock(block);
        }
        foreach (PlanterBrain planter in scratch) Remove(planter);
    }

    private bool EnsureRingResources()
    {
        if (ringMaterial != null) return true;
        if (ringsUnavailable) return false;
        Shader shader = Resources.Load<Shader>("ElectricChargeRing");
        if (shader == null || !shader.isSupported)
        {
            ringsUnavailable = true;
            Debug.LogWarning("ElectricChargeRing shader bulunamadı veya desteklenmiyor; elektrik yükü dünyada gösterilmeyecek.", this);
            return false;
        }
        ringMaterial = new Material(shader) { name = "Electric Charge Ring (runtime)" };
        quad = new Mesh { name = "Electric Charge Quad" };
        quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quad.RecalculateBounds();
        return true;
    }

    private MeshRenderer TakeRing()
    {
        MeshRenderer ring = freeRings.Count > 0 ? freeRings.Pop() : null;
        if (ring == null)
        {
            var go = new GameObject("Electric Charge Ring", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.GetComponent<MeshFilter>().sharedMesh = quad;
            ring = go.GetComponent<MeshRenderer>();
            ring.sharedMaterial = ringMaterial;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            ring.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
        ring.gameObject.SetActive(true);
        return ring;
    }

    private void ReleaseRing(Charge charge)
    {
        if (charge.ring == null) return;
        charge.ring.gameObject.SetActive(false);
        freeRings.Push(charge.ring);
        charge.ring = null;
    }
}
