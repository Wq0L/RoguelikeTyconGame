using System.Collections.Generic;
using UnityEngine;

// Hasat Yankısı kuzey ışığı meteoru (23 Ekim 2026). Ağlar Blender'da tasarlandı (AuroraMeteorArtSO, Tools/Art/AuroraMeteor);
// hareket ve zamanlama burada, his ayarı için kodda tutulur.
// - HarvestEchoPrepared: meteor alanın çapraz yukarısında belirir, mekaniğin gecikmesi (HarvestEchoEffectSO.delay) içinde alan
//   merkezine ivmelenerek düşer. Boyu kesilen hücre sayısıyla yavaş büyür (6 hücrede 1,5 kat); krater alanla ölçeklenir.
// - HarvestEcho: vuruş sonucu. Her zaman çarpma: alev tacı, toz bulutları, fırlayan taş parçaları, krater (kalır, sonra batar)
//   ve ses. Hedef hücreler boşsa da çarpar; yalnız hiçbir şey ölmez (kullanıcı kararı, 23 Ekim). Yerde halka yok.
//   Sonuç hiç gelmezse (ör. etki kaldırıldı) meteor yerde söner.
// - Kartlar (iz, taç, bulut) her karede kameraya çevrilir; kamera açısından bağımsız doğru görünür.
// - Hasar mekanikte vuruş anında uygulanır; görsel hiçbir şeyi geciktirmez. Pause'da durur, round dışında ve restart'ta temizlenir.
// - Havuzlu, aynı anda en çok MeteorCapacity. Ağlar asset'tir (yok edilmez); kare başına bellek ayrılmaz.
public sealed partial class LateGamePresenter
{
    private const int MeteorCapacity = 3, MeteorDebris = 6, MeteorClouds = 4;
    private const float MeteorFizzleSeconds = .12f, MeteorWaitSeconds = .1f;
    // Çarpma sonrası zaman çizelgesi (sn): taç, bulut, enkaz, krater.
    private const float CrownPop = .06f, CrownEnd = .45f, CloudEnd = 1f, DebrisLand = .45f, DebrisEnd = .9f, CraterHold = 1.1f, CraterSink = .3f;
    private const float MeteorImpactSeconds = CraterHold + CraterSink;
    private static readonly Vector3 MeteorFall = new Vector3(.45f, -1f, .25f).normalized;
    private static readonly Vector2[] CloudSlots = { new(-.9f, .15f), new(.95f, -.1f), new(-.35f, -.75f), new(.5f, .7f) };

    // His katmanları (24 Ekim 2026, "açıklanamayan eksiklik" incelemesi): Geç Oyun Testi penceresinden aç/kapa, kullanıcı karşılaştırır.
    // Hareket: taş yuvarlanır, alev 12 kare/sn kaynar ve dalgalanır. Darbe karesi: düşerken uzama, çarpınca basılma, kısa parlama.
    // Tarla tepkisi: yakındaki bitkiler uzaklığa göre gecikmeli esner (hasar yok, halka yok).
    public static bool MeteorMotion = true, MeteorImpactFrame = true, MeteorFieldReaction = true;
    private const float MeteorFlashSeconds = .05f, ShockCellDelay = .035f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetMeteorFeel() => MeteorMotion = MeteorImpactFrame = MeteorFieldReaction = true;

    private enum MeteorPhase { Falling, Impact, Fizzle }

    private sealed class Meteor
    {
        public GameObject root;
        public Transform rockPivot, rock, trail, crown, crater, craterGround, flash;
        public Transform[] rims;
        public readonly Transform[] clouds = new Transform[MeteorClouds];
        public readonly Transform[] debris = new Transform[MeteorDebris];
        public Vector3 start, end, ground;
        public float age, phaseAge, fall, size, area, cell, scale;
        public MeteorPhase phase;
        public bool resolved;
    }

    private readonly List<Meteor> liveMeteors = new(), spareMeteors = new();
    private AuroraMeteorArtSO meteorArt;
    private bool meteorArtLoaded;
    private Mesh meteorFlashMesh;

    public int MeteorsLive => liveMeteors.Count;
    public int MeteorsSkipped { get; private set; }
    public int MeteorImpacts { get; private set; }
    public int MeteorFizzles { get; private set; }

    private AuroraMeteorArtSO MeteorArt
    {
        get
        {
            if (!meteorArtLoaded)
            {
                meteorArt = Resources.Load<AuroraMeteorArtSO>(AuroraMeteorArtSO.ResourcePath);
                meteorArtLoaded = true;
                if (meteorArt == null || !meteorArt.Valid) Debug.LogWarning("AuroraMeteorArt eksik; Hasat Yankısı meteoru çizilmeyecek (oynanış etkilenmez).");
            }
            return meteorArt != null && meteorArt.Valid ? meteorArt : null;
        }
    }

    private void ShowMeteor(RewardCue cue)
    {
        Camera camera = Camera.main;
        AuroraMeteorArtSO art = MeteorArt;
        if (camera == null || art == null || !EnsureArcAssets()) return;
        if (liveMeteors.Count >= MeteorCapacity) { MeteorsSkipped++; return; }
        Meteor m;
        if (spareMeteors.Count > 0) { m = spareMeteors[^1]; spareMeteors.RemoveAt(spareMeteors.Count - 1); }
        else m = BuildMeteor(art);

        m.cell = MeteorCellSize();
        m.scale = m.cell / art.cell;
        m.size = m.scale * (1f + .1f * (Mathf.Max(1, cue.Value) - 1));
        m.area = Mathf.Max(cue.Radius, .5f * m.cell);
        m.ground = cue.To + Vector3.up * .3f;
        m.end = m.ground + Vector3.up * (art.rockRadius * m.size * .6f);
        m.start = m.ground - MeteorFall * (6f + 2.2f * art.rockRadius * m.size);   // daha yüksekten: düşüş görülsün
        m.fall = Mathf.Max(.05f, MeteorDelay());
        m.age = m.phaseAge = 0f;
        m.phase = MeteorPhase.Falling;
        m.resolved = false;
        m.root.SetActive(true);
        PoseMeteor(m, camera);
        liveMeteors.Add(m);
    }

    // Vuruş sonucu: alan merkezine en yakın, henüz sonuçlanmamış düşen meteor. İsabet olsun olmasın çarpar (boş hücrede hasar yok).
    private void ResolveMeteor(RewardCue cue)
    {
        Meteor best = null;
        float bestDistance = 1f;
        for (int i = 0; i < liveMeteors.Count; i++)
        {
            Meteor m = liveMeteors[i];
            if (m.resolved || m.phase != MeteorPhase.Falling) continue;
            float distance = Vector3.Distance(m.ground - Vector3.up * .3f, cue.From);
            if (distance < bestDistance) { best = m; bestDistance = distance; }
        }
        if (best == null) return;
        best.resolved = true;
        best.phaseAge = 0f;
        best.phase = MeteorPhase.Impact;
        MeteorImpacts++;
        Play(LateGameSound.HarvestEcho, LateGameVariant.HarvestEcho);
        if (MeteorFieldReaction) ShockField(best);
        Camera camera = Camera.main;
        if (camera != null) PoseMeteor(best, camera);
    }

    public int FieldShocks { get; private set; }

    // Tarla tepkisi: alanın bir hücre ötesine kadar yaşayan bitkiler, uzaklığa göre gecikmeli ve azalan bir şokla esner.
    // Yalnız görsel model ölçeği (PlantJuice); hasar, collider, oynanış değişmez. Çarpma başına bir tarama (en çok 6 sn'de bir).
    private void ShockField(Meteor m)
    {
        GridSystem grid = RunEffects.Instance != null ? RunEffects.Instance.Grid : null;
        if (grid == null) return;
        Vector3 center = m.ground - Vector3.up * .3f;
        float reach = m.area + m.cell;
        GridPosition hit = grid.GetGridPosition(center);
        int cells = Mathf.CeilToInt(reach / m.cell);
        // Çevredeki hücreler doğrudan taranır (üretim noktası açık/kapalı olmasından bağımsız; bellek ayırmaz).
        for (int x = hit.x - cells; x <= hit.x + cells; x++)
            for (int z = hit.z - cells; z <= hit.z + cells; z++)
            {
                var cell = new GridPosition(x, z);
                if (!grid.IsValidGridPosition(cell)) continue;
                PlantHealth plant = FieldCells.LivingPlant(grid, cell);
                if (plant == null || !plant.TryGetComponent(out PlantJuice juice)) continue;
                Vector3 offset = plant.transform.position - center;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance > reach) continue;
                juice.Shock(Mathf.Lerp(1.3f, .35f, distance / reach), distance / m.cell * ShockCellDelay);
                FieldShocks++;
            }
    }

    private void SyncMeteors()
    {
        if (liveMeteors.Count == 0) return;
        if (RoundManager.Instance == null || !RoundManager.Instance.IsRoundActive) { ClearMeteors(); return; }
        bool running = Time.timeScale > 0f && GameManager.Instance != null && GameManager.Instance.CurrentState == GameStates.Round;
        float delta = running ? Time.deltaTime : 0f;
        Camera camera = Camera.main;
        for (int i = liveMeteors.Count - 1; i >= 0; i--)
        {
            Meteor m = liveMeteors[i];
            m.age += delta;
            if (m.phase == MeteorPhase.Falling)
            {
                // Sonuç gelmediyse (ör. etki kaldırıldı) meteor yerde söner.
                if (m.age >= m.fall + MeteorWaitSeconds) { m.phase = MeteorPhase.Fizzle; m.phaseAge = 0f; m.resolved = true; MeteorFizzles++; }
            }
            else m.phaseAge += delta;
            if ((m.phase == MeteorPhase.Impact && m.phaseAge >= MeteorImpactSeconds) || (m.phase == MeteorPhase.Fizzle && m.phaseAge >= MeteorFizzleSeconds))
            {
                StoreMeteor(m);
                liveMeteors.RemoveAt(i);
                continue;
            }
            if (camera != null) PoseMeteor(m, camera);
        }
    }

    private void PoseMeteor(Meteor m, Camera camera)
    {
        Vector3 forward = camera.transform.forward;
        bool falling = m.phase == MeteorPhase.Falling, impact = m.phase == MeteorPhase.Impact;

        // Taş + iz. Pivotun z ekseni düşüş yönüdür (uzama/basılma ve yuvarlanma bu eksende); taş ağı pivotun içinde özgün yönünde
        // durur, kızgın yüz hep önde kalır.
        Vector3 rockAt;
        float rockScale, along = 1f, across = 1f, spin = 0f;
        if (falling)
        {
            float t = Mathf.Clamp01(m.age / m.fall);
            rockAt = Vector3.LerpUnclamped(m.start, m.end, t * t);   // ivmelenen düşüş
            rockScale = m.size;
            if (MeteorImpactFrame) { along = 1f + .3f * t; across = 1f - .12f * t; }   // hızlandıkça uzar
            if (MeteorMotion) spin = m.age * 540f;
        }
        else
        {
            float sink = Mathf.Clamp01(m.phaseAge / (impact ? .1f : MeteorFizzleSeconds));
            rockAt = m.end - Vector3.up * (m.size * .3f * sink);
            rockScale = m.size * (1f - sink);
            if (impact && MeteorImpactFrame) { along = Mathf.Lerp(.55f, .3f, sink); across = Mathf.Lerp(1.35f, 1.1f, sink); }   // çarpınca basılır
            if (MeteorMotion) spin = (m.fall + m.phaseAge) * 540f;
        }
        SetActive(m.rockPivot, rockScale > .001f);
        if (rockScale > .001f)
        {
            m.rockPivot.SetPositionAndRotation(rockAt, Quaternion.LookRotation(MeteorFall) * Quaternion.AngleAxis(spin, Vector3.forward));
            m.rockPivot.localScale = new Vector3(rockScale * across, rockScale * across, rockScale * along);
        }
        SetActive(m.trail, falling);
        if (falling)
        {
            Quaternion card = Card(forward, -MeteorFall);
            Vector3 trailScale = Vector3.one * (m.size * Mathf.Lerp(.75f, 1f, Mathf.Clamp01(m.age / m.fall)));
            if (MeteorMotion)
            {
                // Toon alev: 12 kare/sn ayna "kaynama" + yumuşak dalgalanma ve hafif salınım (Random yok).
                float a = m.age;
                float boil = Mathf.Floor(a * 12f) % 2f == 0f ? 1f : -1f;
                trailScale.x *= boil * (1f + .1f * Mathf.Sin(a * 37f));
                trailScale.y *= 1f + .12f * Mathf.Sin(a * 29f + 1f);
                card *= Quaternion.AngleAxis(4f * Mathf.Sin(a * 17f), Vector3.forward);
            }
            m.trail.SetPositionAndRotation(rockAt, card);
            m.trail.localScale = trailScale;
        }

        // Çarpma
        SetActive(m.crown, impact && m.phaseAge < CrownEnd);
        SetActive(m.crater, impact);
        for (int i = 0; i < MeteorClouds; i++) SetActive(m.clouds[i], impact && m.phaseAge < CloudEnd);
        for (int i = 0; i < MeteorDebris; i++) SetActive(m.debris[i], impact && m.phaseAge < DebrisEnd);
        if (!impact) return;
        float k = m.phaseAge;
        float areaScale = m.scale * Mathf.Max(1f, m.area / (.5f * m.cell));
        Quaternion up = Card(forward, Vector3.up);

        if (k < CrownEnd)
        {
            float crown = k < CrownPop ? Ease(k / CrownPop) : Mathf.Lerp(1f, 0f, (k - CrownPop) / (CrownEnd - CrownPop));
            Quaternion sway = MeteorMotion ? Quaternion.AngleAxis(5f * Mathf.Sin(k * 30f), Vector3.forward) : Quaternion.identity;
            m.crown.SetPositionAndRotation(m.ground, up * sway);
            m.crown.localScale = Vector3.one * (m.size * Mathf.Max(.001f, crown) * (1f + .15f * (areaScale / m.scale - 1f)));
        }

        // Darbe karesi: çarpmanın ilk anında kısa krem-beyaz patlama yıldızı (yerel; zaman durmaz).
        bool flash = MeteorImpactFrame && k < MeteorFlashSeconds;
        SetActive(m.flash, flash);
        if (flash)
        {
            m.flash.SetPositionAndRotation(m.ground + Vector3.up * (.35f * m.scale), up);
            m.flash.localScale = Vector3.one * (m.scale * (.9f + .25f * (areaScale / m.scale - 1f)) * (1f + .3f * k / MeteorFlashSeconds));
        }

        // Krater: zemin (yanık leke, çatlak, kristal) alanla ölçülü büyür; kenar blokları neredeyse aynı boyda kalır, genişleyen çevreye
        // dağılır (dev kaya olmasın). Çarpmada küçükten açılır, sonunda yere batar.
        float areaFactor = areaScale / m.scale;
        float open = Ease(Mathf.Clamp01(k / .12f)), sinkK = Mathf.Clamp01((k - CraterHold) / CraterSink);
        float ground = m.scale * (1f + .45f * (areaFactor - 1f)) * Mathf.Lerp(.6f, 1f, open);
        float rim = m.scale * (1f + .12f * (areaFactor - 1f)) * Mathf.Lerp(.5f, 1f, open) * (1f - .6f * sinkK);
        m.crater.position = m.ground - Vector3.up * (.05f * sinkK);
        m.craterGround.localScale = new Vector3(ground * (1f - .3f * sinkK), ground * (1f - sinkK) + .001f, ground * (1f - .3f * sinkK));
        AuroraMeteorArtSO art = meteorArt;
        for (int i = 0; i < m.rims.Length; i++)
        {
            Vector3 at = art.rimPositions[i];
            m.rims[i].localPosition = new Vector3(at.x * ground, at.y * m.scale * (1f - sinkK), at.z * ground);
            m.rims[i].localScale = Vector3.one * (rim + .001f);
        }

        if (k < CloudEnd)
        {
            float grow = Ease(Mathf.Clamp01(k / .5f)), fade = 1f - Mathf.Clamp01((k - .55f) / (CloudEnd - .55f));
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized, depth = Vector3.Cross(right, Vector3.up);
            for (int i = 0; i < MeteorClouds; i++)
            {
                Vector2 slot = CloudSlots[i];
                Vector3 at = m.ground + (right * slot.x + depth * slot.y) * (m.area * Mathf.Lerp(.45f, .8f, grow)) + Vector3.up * Mathf.Lerp(.1f, .5f, grow) * m.scale;
                m.clouds[i].SetPositionAndRotation(at, up);
                m.clouds[i].localScale = Vector3.one * (m.scale * Mathf.Lerp(.6f, 1.35f, grow) * fade * (1f + .1f * (areaScale / m.scale - 1f)) + .001f);
            }
        }

        if (k < DebrisEnd)
        {
            float fly = Mathf.Clamp01(k / DebrisLand), shrink = 1f - Mathf.Clamp01((k - DebrisLand) / (DebrisEnd - DebrisLand));
            for (int i = 0; i < MeteorDebris; i++)
            {
                float angle = i * 2f * Mathf.PI / MeteorDebris + .5f * (i % 2);
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float reach = m.area * Mathf.Lerp(.3f, 1.1f + .15f * (i % 3), fly);
                float height = (.25f + 1.6f * fly - 1.85f * fly * fly) * m.scale;
                m.debris[i].position = m.ground + dir * reach + Vector3.up * Mathf.Max(0f, height);
                m.debris[i].rotation = Quaternion.Euler(k * 520f + i * 40f, k * 310f + i * 70f, 0f);
                m.debris[i].localScale = Vector3.one * (m.scale * shrink + .001f);
            }
        }
    }

    // Kart uzayı → dünya: z kameranın bakış yönü, y ekranda verilen yön (kameraya dik düzleme izdüşüm).
    private static Quaternion Card(Vector3 forward, Vector3 direction)
    {
        Vector3 y = direction - forward * Vector3.Dot(direction, forward);
        return y.sqrMagnitude < 1e-6f ? Quaternion.LookRotation(forward) : Quaternion.LookRotation(forward, y);
    }

    private static float Ease(float t) => 1f - (1f - t) * (1f - t);

    private static void SetActive(Transform part, bool on)
    {
        if (part.gameObject.activeSelf != on) part.gameObject.SetActive(on);
    }

    private void StoreMeteor(Meteor m)
    {
        if (m.root != null) m.root.SetActive(false);
        spareMeteors.Add(m);
    }

    private void ClearMeteors()
    {
        foreach (Meteor m in liveMeteors) StoreMeteor(m);
        liveMeteors.Clear();
    }

    // Ağlar ve malzemeler asset'tir; çalışma anında üretilen bir şey yok (yöntem bağlantı noktası olarak kalır).
    private void DestroyMeteorAssets()
    {
        if (meteorFlashMesh != null) Destroy(meteorFlashMesh);
    }

    // Darbe karesi yıldızı (çalışma anında bir kez): mürekkep, krem, beyaz merkez. Kart uzayında (z kameradan uzak).
    private static Mesh BuildFlashMesh()
    {
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var triangles = new List<int>();
        void Layer(float outer, float inner, float depth, Color color)
        {
            int center = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, depth));
            colors.Add(color);
            for (int i = 0; i < 18; i++)
            {
                float radius = i % 2 == 0 ? outer * (i % 4 == 0 ? 1f : .8f) : inner, angle = i * Mathf.PI / 9f + .15f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, depth));
                colors.Add(color);
                triangles.Add(center); triangles.Add(center + 1 + i); triangles.Add(center + 1 + (i + 1) % 18);
            }
        }
        Layer(1f, .48f, .03f, new Color(.006f, .005f, .016f));   // mürekkep (#1A1324, doğrusal)
        Layer(.86f, .4f, 0f, new Color(1f, .93f, .78f));
        Layer(.36f, .36f, -.03f, Color.white);
        var mesh = new Mesh { name = "Meteor Impact Flash (runtime)" };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static float MeteorDelay()
    {
        RunEffects effects = RunEffects.Instance;
        HarvestEchoEffect echo = effects != null ? effects.Find<HarvestEchoEffect>() : null;
        return echo != null && echo.Data is HarvestEchoEffectSO data ? data.delay : .2f;
    }

    private static float MeteorCellSize()
    {
        GridSystem grid = RunEffects.Instance != null ? RunEffects.Instance.Grid : null;
        return grid != null ? Vector3.Distance(FieldCells.World(grid, new GridPosition(0, 0)), FieldCells.World(grid, new GridPosition(1, 0))) : 2f;
    }

    // ---- kurulum (havuz başına bir kez) ----
    private Meteor BuildMeteor(AuroraMeteorArtSO art)
    {
        var root = new GameObject("Aurora Meteor");
        root.transform.SetParent(transform, false);
        var m = new Meteor { root = root };
        m.rockPivot = new GameObject("Rock Pivot").transform;
        m.rockPivot.SetParent(root.transform, false);
        m.rock = LitPart(m.rockPivot, "Rock", art.rock, art);
        m.rock.localRotation = Quaternion.Inverse(Quaternion.LookRotation(MeteorFall));   // ağ özgün yönünde: kızgın yüz önde
        UnlitPart(m.rock, "Cracks", art.rockCracks);
        if (meteorFlashMesh == null) meteorFlashMesh = BuildFlashMesh();
        m.flash = UnlitPart(root.transform, "Impact Flash", meteorFlashMesh);
        m.trail = UnlitPart(root.transform, "Trail", art.trail);
        m.crown = UnlitPart(root.transform, "Crown", art.crown);
        for (int i = 0; i < MeteorClouds; i++) m.clouds[i] = UnlitPart(root.transform, "Cloud " + i, art.clouds[i % art.clouds.Length]);
        for (int i = 0; i < MeteorDebris; i++) m.debris[i] = LitPart(root.transform, "Debris " + i, art.stones[i % art.stones.Length], art);
        m.crater = new GameObject("Crater").transform;
        m.crater.SetParent(root.transform, false);
        m.craterGround = UnlitPart(m.crater, "Ground", art.craterGround);
        m.rims = new Transform[art.rims.Length];
        for (int i = 0; i < art.rims.Length; i++) m.rims[i] = LitPart(m.crater, "Rim " + i, art.rims[i], art);
        return m;
    }

    private Transform LitPart(Transform parent, string name, Mesh mesh, AuroraMeteorArtSO art)
    {
        Transform body = MeshPart(parent, name, mesh, art.lit);
        MeshPart(body, "Ink", mesh, art.ink);
        return body;
    }

    private Transform UnlitPart(Transform parent, string name, Mesh mesh) => MeshPart(parent, name, mesh, arcMaterial);

    private static Transform MeshPart(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go.transform;
    }
}
