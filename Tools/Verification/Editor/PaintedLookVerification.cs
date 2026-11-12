using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MechanicsTestLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Boyalı görünüm doğrulaması (11 Kasım 2026; PaintedLookInstaller). Yalnız izole kopyada.
//  RunBatch      : kurulumu kopyada İKİ kez çalıştırır (çoğaltmamalı), sonra denetler.
//  RunAsIsBatch  : ana projenin durumunu olduğu gibi denetler (kurulum çalıştırılmaz).
// Denetimler: varlıklar (doku boyu, mesh ölçü / UV), prefablar, GameScene karo çeşitleri, renderer geçişi, imleç, menü;
// oyunda (Mechanics Test Lab oturumu): açık / kapalı karo malzemesi, değiştirici rengi, kilit açma, saksı mesh'i, imleç halkası
// yarıçapı, imleçte kare başına bellek ayırma yok, doku belleği, hata yok.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method PaintedLookVerification.RunBatch -Full
[InitializeOnLoad]
public static class PaintedLookVerification
{
    const string Key = "PaintedLookVerification";
    static readonly List<string> lines = new();
    static int passed, failed, phase;
    static double started, next;
    static readonly List<string> errors = new();
    static PaintedLookVerification() { EditorApplication.update += Tick; }

    static void Check(bool ok, string text) { lines.Add((ok ? "PASS " : "FAIL ") + text); if (ok) passed++; else failed++; Debug.Log((ok ? "PASS " : "FAIL ") + text); }
    static void Info(string text) { lines.Add("INFO " + text); Debug.Log("INFO " + text); }

    // Oyun kipine geçişte betikler yeniden yüklenir (statikler sıfırlanır): düzenleme kipi sonuçları SessionState'te taşınır.
    static void SaveState()
    {
        SessionState.SetString(Key + ".lines", string.Join("\n", lines));
        SessionState.SetInt(Key + ".passed", passed); SessionState.SetInt(Key + ".failed", failed);
    }

    static void LoadState()
    {
        if (lines.Count > 0) return;
        string saved = SessionState.GetString(Key + ".lines", "");
        if (saved.Length > 0) lines.AddRange(saved.Split('\n'));
        passed = SessionState.GetInt(Key + ".passed", 0); failed = SessionState.GetInt(Key + ".failed", 0);
    }

    public static void RunBatch() => Begin(true);
    public static void RunAsIsBatch() => Begin(false);

    static void Begin(bool install)
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        try
        {
            if (install)
            {
                Info(PaintedLookInstaller.Install().Replace("\n", " | "));
                string second = PaintedLookInstaller.Install();
                Info("second run: " + second.Replace("\n", " | "));
                Check(second.Contains("GameScene: 0 karoya") && second.Contains("menü dioraması: 0 karo, 0 saksı") && second.Contains("zaten renderer'da")
                      && second.Contains("1x1: 0") && second.Contains("2x3: 0") && second.Contains("0 öğe eklendi"), "installer is idempotent (second run changes nothing)");
            }
            else Info("as-is: main project's state, installer not run");
            StaticChecks();
        }
        catch (Exception e) { Check(false, "static checks threw: " + e); Finish(); return; }
        SaveState();
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        EditorSceneManager.OpenScene(Lab.GameScenePath);
        SessionState.SetBool(Key, true);
        Scenario s = ScenarioPresets.New("Boyalı görünüm doğrulaması", "", "", "", 5);
        s.aimFixed = true; s.aimX = 5; s.aimZ = 5; s.autoAttack = true;
        ScenarioPresets.Planter(s, ScenarioPresets.LargePlanter(2, 3), 3, 5);
        ScenarioPresets.Planter(s, ScenarioPresets.LargePlanter(1, 2), 3, 3);
        ScenarioPresets.Planter(s, ScenarioPresets.Small(), 5, 5);
        ScenarioPresets.Tile(s, 6, 3, TileModifierType.Fertile, TileRarity.Common);
        ScenarioPresets.Tile(s, 7, 4, TileModifierType.Electric, TileRarity.Epic);
        Result r = TestSession.StartPlay(s);
        if (!r.Ok) { Check(false, "lab session: " + r.Message); Finish(); }
    }

    static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        // Unity'nin kendi arama dizini toplu kipte başlangıçta bu hatayı atıyor (UnityEditor.Search, oyunla ilgisi yok): sayılmaz, yazılır.
        if (stack != null && stack.Contains("UnityEditor.Search")) { lines.Add("INFO ignored editor search indexing error: " + message); return; }
        errors.Add(message);
    }

    // ---------------------------------------------------------------- düzenleme kipi denetimleri
    static Material[] Set(Func<int, string> path) => Enumerable.Range(0, PaintedLookInstaller.Variants).Select(v => AssetDatabase.LoadAssetAtPath<Material>(path(v))).ToArray();

    static void StaticChecks()
    {
        // dokular
        var importers = AssetDatabase.FindAssets("t:Texture2D", new[] { PaintedLookInstaller.TextureFolder })
            .Select(g => AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(g)) as TextureImporter).Where(i => i != null).ToList();
        Check(importers.Count == 11, $"11 painted textures (6 floor, 5 planter): {importers.Count}");
        Check(importers.All(i => i.maxTextureSize <= PaintedLookInstaller.MaxTextureSize && i.mipmapEnabled && i.textureCompression != TextureImporterCompression.Uncompressed),
              $"textures capped at {PaintedLookInstaller.MaxTextureSize}, mipmapped, compressed");

        // karo mesh'i
        Mesh original = AssetDatabase.LoadAllAssetsAtPath(PaintedLookInstaller.SourceFloorFbx).OfType<Mesh>().First();
        Mesh floor = AssetDatabase.LoadAssetAtPath<Mesh>(PaintedLookInstaller.FloorMeshPath);
        Check(floor != null && floor.vertexCount == original.vertexCount && floor.subMeshCount == original.subMeshCount && (floor.bounds.size - original.bounds.size).magnitude < 1e-4f,
              $"floor mesh keeps the original shape ({floor?.vertexCount} vertices, {floor?.subMeshCount} submeshes)");
        if (floor != null)
        {
            Vector2[] uv = floor.uv; Vector3[] n = floor.normals;
            Check(uv.All(u => u.x >= 0f && u.x <= 1f && u.y >= 0f && u.y <= 1f), "floor UVs inside 0..1");
            Check(Enumerable.Range(0, uv.Length).Where(i => n[i].y > .9f).All(i => uv[i].x <= .75f && uv[i].y >= .25f), "floor top faces map to the painted top square");
        }

        // malzemeler
        var open = Set(PaintedLookInstaller.OpenMaterialPath); var locked = Set(PaintedLookInstaller.LockedMaterialPath);
        Check(open.Concat(locked).All(m => m != null && m.GetTexture("_MainTex") != null && m.GetColor("_Color") == Color.white), "6 floor materials carry their texture, colour multiplier white");

        // saksılar
        foreach (string size in PaintedLookInstaller.PlanterSizes)
        {
            Mesh painted = AssetDatabase.LoadAllAssetsAtPath(PaintedLookInstaller.PlanterMeshPath(size)).OfType<Mesh>().FirstOrDefault();
            Mesh source = AssetDatabase.LoadAllAssetsAtPath($"Assets/3D Assets/AI/Models/Saksı {size}.fbx").OfType<Mesh>().FirstOrDefault();
            Check(painted != null && source != null && (painted.bounds.center - source.bounds.center).magnitude < 2e-3f && (painted.bounds.size - source.bounds.size).magnitude < 2e-3f,
                  $"planter {size} painted mesh matches the original bounds ({painted?.bounds.size} vs {source?.bounds.size})");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PaintedLookInstaller.PlanterPrefabPath(size));
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            var visible = renderers.Where(r => r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("Saksi_Boyali")).ToList();
            var ghosts = renderers.Where(r => r.sharedMaterial != null && !r.sharedMaterial.name.StartsWith("Saksi_Boyali") && !r.sharedMaterial.name.StartsWith("Planter_")).ToList();
            Check(visible.Count >= 1 && visible.All(r => r.GetComponent<MeshFilter>().sharedMesh == painted)
                  && renderers.All(r => r.sharedMaterial == null || !r.sharedMaterial.name.StartsWith("Planter_")),
                  $"planter {size} prefab shows the painted mesh + material ({visible.Count} visible part)");
            Check(ghosts.All(r => r.GetComponent<MeshFilter>()?.sharedMesh != painted), $"planter {size} ghost (placement) part untouched ({ghosts.Count})");
        }

        // Ground.prefab
        var ground = AssetDatabase.LoadAssetAtPath<GameObject>(PaintedLookInstaller.GroundPrefabPath).GetComponent<GroundCell>();
        var gso = new SerializedObject(ground);
        var gRenderer = gso.FindProperty("groundRenderer").objectReferenceValue as Renderer;
        var unlocked = gso.FindProperty("unlockedMaterials");
        Check(gRenderer.GetComponent<MeshFilter>().sharedMesh == floor && gso.FindProperty("lockedMaterial").objectReferenceValue == locked[0]
              && unlocked.arraySize == floor.subMeshCount && Enumerable.Range(0, unlocked.arraySize).All(i => unlocked.GetArrayElementAtIndex(i).objectReferenceValue == open[0]),
              "Ground.prefab: painted mesh, variant 1, unlocked list as long as the submeshes");

        // GameScene karoları
        var scene = EditorSceneManager.OpenScene(PaintedLookInstaller.GameScenePath, OpenSceneMode.Single);
        var cells = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GroundCell>(true)).ToList();
        var used = new int[PaintedLookInstaller.Variants]; int wrong = 0;
        foreach (var cell in cells)
        {
            var so = new SerializedObject(cell);
            int v = PaintedLookInstaller.VariantFor(cell.transform.position, PaintedLookInstaller.Variants);
            var ul = so.FindProperty("unlockedMaterials");
            var rr = so.FindProperty("groundRenderer").objectReferenceValue as Renderer;
            bool ok = so.FindProperty("lockedMaterial").objectReferenceValue == locked[v] && ul.arraySize == floor.subMeshCount
                      && Enumerable.Range(0, ul.arraySize).All(i => ul.GetArrayElementAtIndex(i).objectReferenceValue == open[v])
                      && rr != null && rr.sharedMaterials.All(m => m == locked[v]) && rr.GetComponent<MeshFilter>().sharedMesh == floor;
            if (ok) used[v]++; else wrong++;
        }
        Check(cells.Count == 121 && wrong == 0, $"GameScene: all {cells.Count} tiles carry their own variant (wrong {wrong})");
        Check(used.All(u => u > 20), $"all three variants used [{string.Join(", ", used)}]");

        // renderer geçişi
        var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(PaintedLookInstaller.RendererPath);
        var features = data.rendererFeatures.Where(f => f != null && f.name == PaintedLookInstaller.HadesFeatureName).ToList();
        var fp = features.FirstOrDefault() as FullScreenPassRendererFeature;
        Check(features.Count == 1 && fp != null && fp.isActive && fp.passMaterial != null && fp.passMaterial.shader.name == "Hidden/HadesStylize"
              && fp.injectionPoint == FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents, "Hades pass on the toon renderer once, active, before transparents (UI / particles untouched)");
        if (fp != null && fp.passMaterial != null)
            Check(Mathf.Approximately(fp.passMaterial.GetFloat("_Thickness"), PaintedLookInstaller.HadesThickness) && Mathf.Approximately(fp.passMaterial.GetFloat("_ShadowStrength"), PaintedLookInstaller.HadesShadow),
                  "pass uses the chosen light (hafif) values");
        var map = new SerializedObject(data).FindProperty("m_RendererFeatureMap");
        Check(map.arraySize == data.rendererFeatures.Count, $"renderer feature map consistent ({map.arraySize} / {data.rendererFeatures.Count})");

        // imleç
        var cursor = AssetDatabase.LoadAssetAtPath<GameObject>(HarvestCursorReadability.PrefabPath).GetComponent<HarvestCursorVisual>();
        var cso = new SerializedObject(cursor);
        Check(cso.FindProperty("inkRing").objectReferenceValue != null && cso.FindProperty("groundFill").objectReferenceValue != null, "harvest cursor has ink ring + ground fill wired");

        // menü
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(MenuDioramaBuilder.PrefabPath);
        if (menu != null)
        {
            var filters = menu.GetComponentsInChildren<MeshFilter>(true);
            Check(!filters.Any(f => f.sharedMesh != null && AssetDatabase.GetAssetPath(f.sharedMesh) == PaintedLookInstaller.SourceFloorFbx), "menu diorama: no unpainted tile left");
            var tileMats = filters.Where(f => f.sharedMesh == floor).SelectMany(f => f.GetComponent<MeshRenderer>().sharedMaterials).Distinct().ToList();
            Check(tileMats.Count > 0 && tileMats.All(m => m.name.EndsWith("_Menu") && m.parent != null && m.parent.name.StartsWith("Floor_Boyali")), $"menu tiles use painted menu variants ({tileMats.Count})");
        }
    }

    // ---------------------------------------------------------------- oyun kipi
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        double now = EditorApplication.timeSinceStartup;
        if (started == 0) { LoadState(); Application.logMessageReceived += OnLog; started = now; next = now + 6; }
        if (now - started > 120) { Check(false, "play timeout at phase " + phase); Finish(); return; }
        if (now < next) return;
        try
        {
            var open = Set(PaintedLookInstaller.OpenMaterialPath); var locked = Set(PaintedLookInstaller.LockedMaterialPath);
            var cells = UnityEngine.Object.FindObjectsByType<GroundCell>(FindObjectsSortMode.None);
            if (phase == 0)
            {
                int badOpen = 0, badLocked = 0, opened = 0;
                foreach (var c in cells)
                {
                    int v = PaintedLookInstaller.VariantFor(c.transform.position, PaintedLookInstaller.Variants);
                    var mats = c.GroundRenderer.sharedMaterials;
                    if (c.IsLocked) { if (!mats.All(m => m == locked[v])) badLocked++; }
                    else { opened++; if (!mats.All(m => m == open[v])) badOpen++; }
                }
                Check(opened == 25 && badOpen == 0 && badLocked == 0, $"play: open tiles ({opened}) and locked tiles show their painted variant (wrong {badOpen} / {badLocked})");
                var modified = cells.Where(c => c.CurrentModifier != null).ToList();
                var block = new MaterialPropertyBlock();
                bool tinted = modified.Count == 2 && modified.All(c =>
                {
                    c.GroundRenderer.GetPropertyBlock(block);
                    return block.GetColor("_BaseColor") == c.CurrentModifier.tileColor && c.GroundRenderer.sharedMaterials.All(m => open.Contains(m));
                });
                Check(tinted, $"play: tile modifiers still tint the painted tile ({modified.Count} modified)");
                var lockedCell = cells.First(c => c.IsLocked);
                lockedCell.Unlock();
                int lv = PaintedLookInstaller.VariantFor(lockedCell.transform.position, PaintedLookInstaller.Variants);
                Check(lockedCell.GroundRenderer.sharedMaterials.All(m => m == open[lv]), "play: unlocking a tile switches to its painted open variant");
                var planterMeshes = UnityEngine.Object.FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None)
                    .SelectMany(p => p.GetComponentsInChildren<MeshRenderer>(false)).Where(r => r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("Saksi_Boyali")).ToList();
                Check(planterMeshes.Count >= 3, $"play: placed planters render painted ({planterMeshes.Count} painted parts)");
            }
            else if (phase == 1)
            {
                var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                var visual = player != null ? player.GetComponentInChildren<HarvestCursorVisual>(true) : null;
                var so = visual != null ? new SerializedObject(visual) : null;
                var ink = so?.FindProperty("inkRing").objectReferenceValue as LineRenderer;
                var fill = so?.FindProperty("groundFill").objectReferenceValue as Transform;
                bool ringOk = ink != null && visual.gameObject.activeInHierarchy && Mathf.Abs(ink.GetPosition(0).magnitude - player.CursorRadius) < 1e-3f
                              && fill != null && Mathf.Abs(fill.localScale.x - player.CursorRadius * 2f) < 1e-3f;
                Check(ringOk, $"play: cursor ink ring and fill sit on the attack radius ({player?.CursorRadius:0.000})");
                if (visual != null)
                {
                    Vector3 c = player.CursorWorldPosition; float rad = player.CursorRadius;
                    visual.Show(c, rad);
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    for (int i = 0; i < 200; i++) visual.Show(c, rad);
                    long delta = GC.GetAllocatedBytesForCurrentThread() - before;
                    Check(delta == 0, $"perf: cursor update allocates nothing per frame ({delta} bytes over 200 calls)");
                }
                long bytes = 0; int count = 0;
                foreach (string g in AssetDatabase.FindAssets("t:Texture2D", new[] { PaintedLookInstaller.TextureFolder }))
                {
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g));
                    if (t == null) continue;
                    bytes += Profiler.GetRuntimeMemorySizeLong(t); count++;
                }
                Info($"perf: {count} painted textures use {bytes / 1048576f:0.0} MB (with mipmaps, as imported)");
                Check(bytes < 24L * 1048576, "perf: painted textures under 24 MB in memory");
                var materials = new HashSet<Material>();
                foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    foreach (var m in r.sharedMaterials) if (m != null) materials.Add(m);
                Info($"perf: {materials.Count} distinct materials on scene renderers (painted floor {materials.Count(m => m.name.StartsWith("Floor_Boyali"))}, painted planter {materials.Count(m => m.name.StartsWith("Saksi_Boyali"))})");
            }
            else
            {
                Check(errors.Count == 0, $"no errors in play ({errors.Count}){(errors.Count > 0 ? ": " + errors[0] : "")}");
                Finish(); return;
            }
            phase++;
            next = now + 1.5;
        }
        catch (Exception e) { Check(false, $"play phase {phase} threw: {e}"); Finish(); }
    }

    static void Finish()
    {
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= OnLog;
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/PaintedLookVerification.txt", new[] { $"{(failed == 0 ? "PASS" : "FAIL")} {passed} passed, {failed} failed" }.Concat(lines));
        EditorApplication.Exit(failed == 0 ? 0 : 1);
    }
}
