using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MechanicsTestLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// "Hades geçişi" prototipi, yalnız izole kopyada (11 Kasım 2026, kullanıcı: "menüde test edelim; izole bakayım, onaylarsak
// oyuna geçer"). Shader kaynağı Tools/Art/HadesPrototype/HadesStylize.shader; çalıştırmadan önce kopyanın
// Assets/HadesPrototype klasörüne konur (oyuna / ana projeye hiçbir şey girmez). Menü sahnesi oynatılır, menü kamerasının URP
// renderer'ına bellekte geçici bir FullScreenPassRendererFeature eklenir (kaydedilmez), aynı karede önce / sonra çekilir:
// h0_once, h1_hades (orta), h2_hades_guclu, h3_hades_hafif. Görüntüler: Logs/HadesPrototype/.
// Boyama denemesi (kullanıcı "hafif iyi; o boyamayı da deneyelim"): karolara Tools/Art/HadesPrototype/paint_floor_tiles.py'nin
// dokuları geçici malzeme kopyalarıyla takılır: p1_boyali, p2_boyali_hafif, p3_boyali_orta. Sahne / malzeme dosyası değişmez.
// Saksılar (kullanıcı "saksı ve toprakta böyle boyayalım"): paint_planter.py'nin 5 boyu (Saksi_<boyut>_Boyali.fbx + doku) aynı adlı
// özgün mesh'in yerine takılır.
// Oyun çekimi (kullanıcı "oyunu aktif oynayan biri nasıl görecekse öyle görelim"): RunGameBatch gerçek GameScene'i Mechanics Test
// Lab oturumuyla açar (5 × 5 tarla, 5 saksı boyu, nişan ortada, otomatik saldırı açık), oyunun kendi kamerası + arayüzüyle
// aynı karede çeker: g0_once, g1_boyali, g2_boyali_hafif.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method HadesPrototypeCapture.RunBatch (menü) / RunGameBatch (oyun)
[InitializeOnLoad]
public static class HadesPrototypeCapture
{
    const string Key = "HadesPrototypeCapture", GameKey = "HadesPrototypeCapture.Game", PrefixKey = "HadesPrototypeCapture.Prefix";
    static double next, started;
    static int phase;
    static FullScreenPassRendererFeature feature;
    static ScriptableRendererData data;
    static Material mat;
    static readonly List<string> notes = new();
    const string PaintDir = "Assets/HadesPrototype/";
    static readonly List<(Renderer renderer, Material[] materials)> swapped = new();
    static readonly List<(MeshFilter filter, Mesh mesh)> swappedMeshes = new();
    // paint_floor_tiles.py ile aynı UV düzeni: üst yüz tepeden düz izdüşüm, dış duvarlar alt şeritte yan yana
    const float TopU0 = .004f, TopU1 = .746f, TopV0 = .254f, TopV1 = .996f, StripV0 = .02f, StripV1 = .22f;
    static HadesPrototypeCapture() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (Shader.Find("Hidden/HadesStylize") == null) throw new Exception("HadesStylize shader missing in the copy (Assets/HadesPrototype)");
        EditorSceneManager.OpenScene(MenuDioramaBuilder.MenuScenePath);
        SessionState.SetBool(Key, true); SessionState.SetBool(GameKey, false);
        EditorApplication.EnterPlaymode();
    }

    public static void RunGameBatch() => StartGame("g", GameScenario());

    // Kullanıcı: "boş beyaz, full açık grid, üstü boş, rastgele renklerde tile modifier dağıtılmış hâli nasıl?" — 11 × 11 tam açık,
    // saksı yok, karoların yaklaşık yarısında rastgele tür / nadirlikte değiştirici (sabit tohum: önce / sonra aynı dağılım).
    public static void RunGameTilesBatch()
    {
        Scenario s = ScenarioPresets.New("Hades oyun çekimi (tam açık, değiştiriciler)", "", "", "", 11);
        s.aimFixed = true; s.aimX = 5; s.aimZ = 5; s.autoAttack = true;
        var types = (TileModifierType[])Enum.GetValues(typeof(TileModifierType));
        var rng = new System.Random(1111);
        for (int x = 0; x < 11; x++)
            for (int z = 0; z < 11; z++)
            {
                if (rng.NextDouble() > .5) continue;
                double r = rng.NextDouble();
                TileRarity rarity = r < .5 ? TileRarity.Common : r < .78 ? TileRarity.Rare : r < .93 ? TileRarity.Epic : TileRarity.Legendary;
                ScenarioPresets.Tile(s, x, z, types[rng.Next(types.Length)], rarity);
            }
        StartGame("t", s);
    }

    // Aynı tam açık 11 × 11, hiç değiştirici yok (kullanıcı: "beyazı da görelim").
    public static void RunGameWhiteBatch()
    {
        Scenario s = ScenarioPresets.New("Hades oyun çekimi (tam açık, beyaz)", "", "", "", 11);
        s.aimFixed = true; s.aimX = 5; s.aimZ = 5; s.autoAttack = true;
        StartGame("w", s);
    }

    static Scenario GameScenario()
    {
        // 5 × 5 açık alan (hücre 3–7): her saksı boyundan biri + 1 × 1'ler, iki hücre boş (karo görünsün)
        Scenario s = ScenarioPresets.New("Hades oyun çekimi (5 saksı boyu)", "", "", "", 5);
        s.aimFixed = true; s.aimX = 5; s.aimZ = 5; s.autoAttack = true;
        ScenarioPresets.Planter(s, ScenarioPresets.LargePlanter(2, 3), 3, 5);
        ScenarioPresets.Planter(s, ScenarioPresets.LargePlanter(2, 2), 5, 6);
        ScenarioPresets.Planter(s, ScenarioPresets.LargePlanter(1, 3), 7, 5);
        ScenarioPresets.Planter(s, ScenarioPresets.LargePlanter(1, 2), 3, 3);
        foreach (var (x, z) in new[] { (4, 3), (4, 4), (5, 3), (5, 4), (5, 5), (6, 3), (6, 4), (7, 3) }) ScenarioPresets.Planter(s, ScenarioPresets.Small(), x, z);
        return s;
    }

    static void StartGame(string prefix, Scenario s)
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (Shader.Find("Hidden/HadesStylize") == null) throw new Exception("HadesStylize shader missing in the copy (Assets/HadesPrototype)");
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        EditorSceneManager.OpenScene(Lab.GameScenePath);
        SessionState.SetBool(Key, true); SessionState.SetBool(GameKey, true); SessionState.SetString(PrefixKey, prefix);
        Result r = TestSession.StartPlay(s);
        if (!r.Ok) throw new Exception(r.Message);
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        double now = EditorApplication.timeSinceStartup;
        bool game = SessionState.GetBool(GameKey, false);
        if (started == 0) { started = now; next = now + (game ? 14 : 3); }   // oyunda bitkiler büyüsün, saldırılar başlasın
        if (now - started > 150) { Finish(new Exception("timeout at phase " + phase)); return; }
        if (now < next) return;
        try
        {
            Camera cam = Camera.main;
            switch (phase)
            {
                case 0:
                {
                    GameSettings.BackgroundEffects = true;
                    var space = cam.GetComponent<SpaceBackground>(); if (space != null) { space.enabled = false; space.enabled = true; }
                    cam.aspect = 1920f / 1080f;
                    var camData = cam.GetUniversalAdditionalCameraData();
                    int index = new SerializedObject(camData).FindProperty("m_RendererIndex").intValue;
                    var asset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
                    var list = new SerializedObject(asset).FindProperty("m_RendererDataList");
                    if (index < 0) index = new SerializedObject(asset).FindProperty("m_DefaultRendererIndex").intValue;
                    data = list.GetArrayElementAtIndex(index).objectReferenceValue as ScriptableRendererData;
                    if (data == null) throw new Exception("renderer data not found");
                    mat = new Material(Shader.Find("Hidden/HadesStylize")) { name = "Hades Stylize (prototype)" };
                    var light = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
                    if (light != null) mat.SetVector("_LightDir", -light.transform.forward);
                    feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                    feature.name = "Hades Stylize (prototype)";
                    feature.passMaterial = mat;
                    feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;   // arayüz ve saydam parçacıklar etkilenmez
                    feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Color;
                    feature.fetchColorBuffer = true;
                    feature.SetActive(false);
                    data.rendererFeatures.Add(feature);
                    data.SetDirty();
                    notes.Add($"INFO renderer '{data.name}' (index {index}), light {(light != null ? light.name : "none")}");
                    if (game)
                    {
                        var feel = cam.GetComponent<CameraFeel>(); if (feel != null) feel.enabled = false;   // sarsıntı karşılaştırmayı bozmasın
                        // Oyuncunun varsayılanı: arka plan efektleri açık (GameSettings varsayılanı). Toplu kipte kopyanın ayarı kapalı
                        // olabilir: oyunun uzay arka planı bellekte açılır ve bileşen yeniden kurulur (MenuSceneCapture ile aynı).
                        if (space == null) space = cam.gameObject.AddComponent<SpaceBackground>();
                        space.enabled = false; space.enabled = true;
                        Time.timeScale = 0f;   // önce / sonra aynı kare
                        notes.Add($"INFO game camera ortho={cam.orthographic} size={cam.orthographicSize} rot={cam.transform.eulerAngles} far={cam.farClipPlane}; space bg children {space.transform.childCount}; planters {UnityEngine.Object.FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None).Length}");
                        phase = 8;   // oyun dizisi: bir kare arka plan otursun (9), sonra çekimler (10…)
                    }
                    break;
                }
                case 9: notes.Add($"INFO space bg after a frame: renderers under camera {cam.GetComponentsInChildren<Renderer>(true).Length}, any 'Space Background' {UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Any(r => r.name == "Space Background")}"); break;
                case 10: Capture(GamePrefix + "0_once.png", cam); ApplyPaint(); break;
                case 11: Capture(GamePrefix + "1_boyali.png", cam); feature.SetActive(true); data.SetDirty(); Set(1.6f, .5f, .15f, .35f); break;
                case 12: Capture(GamePrefix + "2_boyali_hafif.png", cam); RemovePaint(); ApplyPaint(variants: true); break;
                case 13: Capture(GamePrefix + "3_cesitli_hafif.png", cam); RemovePaint(); break;
                case 14: data.rendererFeatures.Remove(feature); data.SetDirty(); Finish(null); return;
                case 1: Capture("h0_once.png", cam); ApplyPaint(); break;
                case 2: Capture("p1_boyali.png", cam); feature.SetActive(true); data.SetDirty(); break;
                case 3:
                    Set(1.6f, .5f, .15f, .35f); Capture("p2_boyali_hafif.png", cam);
                    Set(2.2f, .75f, .35f, .55f); Capture("p3_boyali_orta.png", cam);
                    RemovePaint();
                    break;
                case 4:
                    Set(2.2f, .75f, .35f, .55f); Capture("h1_hades.png", cam);
                    Set(3.2f, .9f, .5f, .75f); Capture("h2_hades_guclu.png", cam);
                    Set(1.6f, .5f, .15f, .35f); Capture("h3_hades_hafif.png", cam);
                    break;
                default:
                    data.rendererFeatures.Remove(feature); data.SetDirty();   // geçici: kopyada bile kalıcı değil
                    Finish(null); return;
            }
            phase++;
            next = now + .6;
        }
        catch (Exception e) { Finish(e); }
    }

    // Karo malzemelerinin (açık: Ground_*, kapalı: Locked_*) geçici kopyalarına boyalı doku; renk çarpanı beyaz (renk dokudan gelir).
    // variants: karo başına çeşit (T_Floor_Boyali_*_V0..), hücrenin konumundan sabit bir karışımla seçilir (rastgele görünür ama
    // her açılışta aynı; oyunda da ızgara koordinatından seçilir). Işık yönü bütün çeşitlerde aynı, karo döndürülmez.
    static void ApplyPaint(bool variants = false)
    {
        var openSet = new List<Texture2D>(); var lockedSet = new List<Texture2D>();
        if (variants)
            for (int v = 0; ; v++)
            {
                var o = AssetDatabase.LoadAssetAtPath<Texture2D>(PaintDir + $"T_Floor_Boyali_Acik_V{v}.png");
                var l = AssetDatabase.LoadAssetAtPath<Texture2D>(PaintDir + $"T_Floor_Boyali_Kilitli_V{v}.png");
                if (o == null || l == null) break;
                openSet.Add(o); lockedSet.Add(l);
            }
        else { openSet.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(PaintDir + "T_Floor_Boyali_Acik.png")); lockedSet.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(PaintDir + "T_Floor_Boyali_Kilitli.png")); }
        if (openSet.Count == 0 || openSet[0] == null || lockedSet[0] == null) throw new Exception("painted floor textures missing in the copy (" + PaintDir + ")");
        var cache = new Dictionary<(Material, int), Material>();
        var used = new int[openSet.Count];
        foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            Material[] mats = r.sharedMaterials; bool any = false;
            Vector3 wp = r.transform.position;
            int variant = VariantFor(Mathf.RoundToInt(wp.x * 4f), Mathf.RoundToInt(wp.z * 4f), openSet.Count);
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i]; if (m == null) continue;
                bool isLocked = m.name.StartsWith("Locked"), isOpen = m.name.StartsWith("Ground_");
                if (!isLocked && !isOpen) continue;
                if (!cache.TryGetValue((m, variant), out Material painted))
                {
                    painted = new Material(m) { name = m.name + $" (boyalı V{variant})" };
                    painted.SetTexture("_MainTex", isLocked ? lockedSet[variant] : openSet[variant]);
                    painted.SetColor("_Color", Color.white);
                    cache[(m, variant)] = painted;
                }
                mats[i] = painted; any = true;
            }
            if (any) used[variant]++;
            if (any)
            {
                swapped.Add((r, r.sharedMaterials)); r.sharedMaterials = mats;
                var filter = r.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    if (!meshCache.TryGetValue(filter.sharedMesh, out Mesh paintedMesh)) meshCache[filter.sharedMesh] = paintedMesh = PaintedMesh(filter.sharedMesh);
                    swappedMeshes.Add((filter, filter.sharedMesh)); filter.sharedMesh = paintedMesh;
                }
            }
        }
        // Saksılar: Tools/Art/HadesPrototype/paint_planter.py'nin yeni UV'li modelleri + boyalı dokuları (boy başına)
        foreach (string size in new[] { "1x1", "1x2", "1x3", "2x2", "2x3" })
        {
            var saksiTex = AssetDatabase.LoadAssetAtPath<Texture2D>(PaintDir + $"T_Saksi_{size}_Boyali.png");
            Mesh saksiMesh = AssetDatabase.LoadAllAssetsAtPath(PaintDir + $"Saksi_{size}_Boyali.fbx").OfType<Mesh>().FirstOrDefault();
            if (saksiTex == null || saksiMesh == null) { notes.Add($"WARN painted planter {size} missing"); continue; }
            Material saksiMat = null; int planters = 0;
            foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || filter.sharedMesh.name != saksiMesh.name) continue;
                Material[] mats = r.sharedMaterials;
                if (mats.Length == 0 || mats[0] == null || !mats[0].name.StartsWith("Planter_")) continue;   // hayalet (yerleştirme) malzemesi değişmez
                if (saksiMat == null)
                {
                    saksiMat = new Material(mats[0]) { name = mats[0].name + $" (boyalı {size})" };
                    saksiMat.SetTexture("_MainTex", saksiTex); saksiMat.SetColor("_Color", Color.white);
                    notes.Add($"INFO planter {size} mesh bounds original {filter.sharedMesh.bounds.center} {filter.sharedMesh.bounds.size} -> painted {saksiMesh.bounds.center} {saksiMesh.bounds.size}");
                }
                swapped.Add((r, r.sharedMaterials));
                for (int i = 0; i < mats.Length; i++) mats[i] = saksiMat;
                r.sharedMaterials = mats;
                swappedMeshes.Add((filter, filter.sharedMesh)); filter.sharedMesh = saksiMesh;
                planters++;
            }
            notes.Add($"INFO painted {planters} planters of {size}");
        }
        notes.Add($"INFO painted {swapped.Count} renderers via {cache.Count} material copies; tile variants used [{string.Join(", ", used)}]");
        var sun = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
        if (swapped.Count > 0 && sun != null)
        {
            Transform tile = swapped[0].renderer.transform;
            Vector3 toLight = tile.InverseTransformDirection(-sun.transform.forward).normalized;
            Camera cam = Camera.main;
            Vector3 toCam = tile.InverseTransformDirection(-cam.transform.forward).normalized;
            notes.Add($"INFO tile '{tile.name}' rot {tile.rotation.eulerAngles} scale {tile.lossyScale}; toward light in tile mesh space {toLight.x:F3},{toLight.y:F3},{toLight.z:F3}; toward camera {toCam.x:F3},{toCam.y:F3},{toCam.z:F3}");
        }
        if (swapped.Count == 0) throw new Exception("no tile renderers found to paint");
    }

    // Sabit karışım (Random yok): komşu hücreler farklı çeşit alır, desen tekrar etmez.
    static int VariantFor(int x, int z, int count)
    {
        if (count <= 1) return 0;
        unchecked
        {
            uint h = (uint)(x * 73856093) ^ (uint)(z * 19349663);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (int)(h % (uint)count);
        }
    }

    static void RemovePaint()
    {
        foreach (var (r, mats) in swapped) if (r != null) r.sharedMaterials = mats;
        foreach (var (f, m) in swappedMeshes) if (f != null) f.sharedMesh = m;
        swapped.Clear(); swappedMeshes.Clear();
    }

    static readonly Dictionary<Mesh, Mesh> meshCache = new();

    // Karonun boyamaya uygun UV'li kopyası (bellekte; özgün mesh / FBX değişmez).
    static Mesh PaintedMesh(Mesh source)
    {
        Mesh m = UnityEngine.Object.Instantiate(source); m.name = source.name + " (boyama UV)";
        Vector3[] v = m.vertices, n = m.normals; Bounds b = source.bounds;
        var uv = new Vector2[v.Length];
        float eps = Mathf.Max(b.size.x, b.size.z) * .01f;
        int outerCount = 0;
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 p = v[i], q = n[i];
            bool outer = Mathf.Abs(q.y) < .5f && (Mathf.Abs(p.x - b.min.x) < eps || Mathf.Abs(p.x - b.max.x) < eps || Mathf.Abs(p.z - b.min.z) < eps || Mathf.Abs(p.z - b.max.z) < eps);
            if (outer)
            {
                int seg; float s;
                if (q.x > .5f) { seg = 0; s = (p.z - b.min.z) / b.size.z; }
                else if (q.x < -.5f) { seg = 1; s = (b.max.z - p.z) / b.size.z; }
                else if (q.z > .5f) { seg = 2; s = (b.max.x - p.x) / b.size.x; }
                else { seg = 3; s = (p.x - b.min.x) / b.size.x; }
                uv[i] = new Vector2((seg + .02f + .96f * s) / 4f, StripV0 + (StripV1 - StripV0) * (p.y - b.min.y) / b.size.y);
                outerCount++;
            }
            else uv[i] = new Vector2(TopU0 + (TopU1 - TopU0) * (p.x - b.min.x) / b.size.x, TopV0 + (TopV1 - TopV0) * (p.z - b.min.z) / b.size.z);
        }
        m.uv = uv;
        notes.Add($"INFO painted mesh '{source.name}': {v.Length} vertices ({outerCount} on outer walls), bounds {b.size}");
        return m;
    }

    static string GamePrefix => SessionState.GetString(PrefixKey, "g");

    static void Set(float thickness, float shadow, float hatch, float rim)
    {
        mat.SetFloat("_Thickness", thickness);
        mat.SetFloat("_ShadowStrength", shadow);
        mat.SetFloat("_HatchStrength", hatch);
        mat.SetFloat("_RimStrength", rim);
    }

    static void Finish(Exception e)
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/HadesPrototypeCapture.txt", new[] { e == null ? "DONE" : "FAIL " + e }.Concat(notes));
        EditorApplication.Exit(e == null ? 0 : 1);
    }

    static void Capture(string name, Camera camera)
    {
        Directory.CreateDirectory("Logs/HadesPrototype");
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        foreach (Canvas c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = camera.nearClipPlane + .05f; }
        var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); target.Create();
        RenderTexture old = RenderTexture.active;
        camera.targetTexture = target; Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/HadesPrototype/" + name, image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = old; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
        foreach (Canvas c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
    }
}
