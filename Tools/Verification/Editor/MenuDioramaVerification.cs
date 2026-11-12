using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Ana menü canlı sahnesi (MenuDioramaBuilder) doğrulaması, yalnız izole kopyada (11 Kasım 2026).
// Edit Mode: araç iki kez çalışır; prefab ve sahne öğeleri çoğalmaz (1 sahne örneği, 1 kamera; 49 karo, 9 saksı, 9 bitki);
// eski bahçe resmi kapalı; butonlar sahne yerleşiminde (sceneLayout). Play Mode: menü açılır, uzay arka planı ve sahne
// görünür, hareket var (iki çekim farklı), hata yok. Görüntüler: Logs/MenuDiorama/*.png.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method MenuDioramaVerification.RunBatch -Full
[InitializeOnLoad]
public static class MenuDioramaVerification
{
    const string Key = "MenuDioramaVerification";
    static double next, started;
    static int phase, errors;
    static bool listening;
    static readonly List<string> notes = new();
    static Texture2D first;
    static float playY, playScale;
    static MenuDioramaVerification() { EditorApplication.update += Tick; }

    static void Check(bool value, string text) { if (!value) throw new Exception(text); notes.Add("PASS " + text); }

    public static void RunBatch() => Run(false);
    // Ana projenin bugünkü hâli (kopyada hiçbir şey yeniden kurulmaz): sahne, prefab ve Play davranışı olduğu gibi denetlenir.
    public static void RunAsIsBatch() => Run(true);

    static void Run(bool asIs)
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        try
        {
            // Kopyada sıfırdan kurulum: prefab, mesh ve malzemeler (kopya eski dosyaları tutuyor; araç var olana dokunmaz).
            if (!asIs)
            {
                if (AssetDatabase.IsValidFolder("Assets/Prefabs/Menu")) AssetDatabase.DeleteAsset("Assets/Prefabs/Menu");
                notes.Add("INFO " + MenuDioramaBuilder.Build());
                notes.Add("INFO " + MenuDioramaBuilder.Build());   // ikinci kez: hiçbir şey çoğalmamalı
                notes.Add("INFO " + MenuDioramaBuilder.ApplyPolish());   // kullanıcının istediği büyük butonlar / tabela / lo-fi değerleri
                notes.Add("INFO " + MenuDioramaBuilder.ResetCamera());
            }
            else notes.Add("INFO as-is: main project's MenuScene and prefab, nothing rebuilt");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MenuDioramaBuilder.PrefabPath);
            Check(prefab != null, "diorama prefab exists");
            int tiles = prefab.transform.Find("Platform/Tiles").childCount, planters = prefab.transform.Find("Platform/Planters").childCount;
            var motion = prefab.GetComponent<MenuDioramaMotion>();
            Check(tiles == 49 && planters == 9, $"prefab: {tiles} tiles, {planters} planters");
            Check(motion != null && motion.PlantCount == 9 && motion.Platform != null, $"motion component wired ({motion?.PlantCount} plants)");
            var platform = prefab.transform.Find("Platform");
            int undersides = platform.Cast<Transform>().Count(t => t.name == MenuDioramaBuilder.UndersideName);
            Check(undersides == 1, $"island underside (soil + rock) added once after two runs ({undersides})");
            Check(motion.FloaterCount == 5, $"floating rocks wired to motion ({motion.FloaterCount})");
            Check(prefab.transform.Cast<Transform>().Count(t => t.name == MenuDioramaBuilder.IsletsName) == 1 && motion.IsletCount == 2, $"two far islets added once and wired ({motion.IsletCount})");
            Check(prefab.transform.Cast<Transform>().Count(t => t.name == MenuDioramaBuilder.PollenName) == 1, "pollen particles added once");
            var body = platform.Find(MenuDioramaBuilder.UndersideName + "/Island Body").GetComponent<MeshFilter>().sharedMesh;
            Check(body != null && AssetDatabase.Contains(body) && body.subMeshCount == 3, "underside mesh saved as an asset (3 layers)");
            Check(body.vertexCount == MenuDioramaBuilder.BodyVertexCount, $"underside uses the current shape ({body.vertexCount} vertices)");
            // Ters yüz yok: her üçgenin sarım yönü kendi normaliyle aynı tarafa bakar ve normal kütlenin içinden dışarı bakar.
            Vector3[] bv = body.vertices, bn = body.normals; Vector3 inside = body.bounds.center + Vector3.up * (body.bounds.extents.y * .3f);
            int bad = 0, total = 0;
            for (int sub = 0; sub < body.subMeshCount; sub++)
            {
                int[] tri = body.GetTriangles(sub);
                for (int i = 0; i < tri.Length; i += 3, total++)
                {
                    Vector3 a = bv[tri[i]], b = bv[tri[i + 1]], c = bv[tri[i + 2]], wind = Vector3.Cross(b - a, c - a), n = bn[tri[i]];
                    if (Vector3.Dot(wind, n) <= 0f) bad++;
                    else if (sub > 0 && Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f) bad++;
                }
            }
            Check(bad == 0, $"underside has no flipped faces ({bad} of {total} triangles)");
            var soilMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/Menu/MenuSoil.mat");
            Check(soilMat != null && soilMat.GetFloat("_OtlWorldWidth") > 0f, "soil / rock carry the game's ink outline");
            var gameLook = prefab.transform.Find("Platform/Planters").GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null && m.HasProperty("_OtlWorldWidth") && m.GetFloat("_OtlWorldWidth") > 0f).ToList();
            Check(gameLook.Count > 0 && gameLook.All(m => m.parent != null && m.GetFloat("_OtlWorldWidth") >= m.parent.GetFloat("_OtlWorldWidth") * 1.5f),
                $"planters / plants use menu outline variants of the game materials ({gameLook.Count} slots; game materials untouched)");
            var tileMats = prefab.transform.Find("Platform/Tiles").GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToList();
            Check(tileMats.Count > 0 && tileMats.All(m => m.parent != null && m.GetFloat("_OtlWorldWidth") > 0f && m.GetFloat("_Steps") <= 2.01f),
                $"tiles (where planters sit) carry menu ink outlines and 2-tone cel shading ({tileMats.Count} materials)");
            var menuLight = EditorSceneManager.OpenScene(MenuDioramaBuilder.MenuScenePath).GetRootGameObjects().FirstOrDefault(g => g.name == MenuDioramaBuilder.LightName)?.GetComponent<Light>();
            Check(menuLight != null && menuLight.shadows != LightShadows.Soft, $"menu light casts hard (toon) shadows ({menuLight?.shadows})");
            var pollenMat = prefab.transform.Find(MenuDioramaBuilder.PollenName).GetComponent<ParticleSystemRenderer>().sharedMaterial;
            Check(pollenMat.GetTexture("_BaseMap") != null && pollenMat.GetTexture("_BaseMap").name == "MenuSparkle", "pollen is a hard-edged comic sparkle");
            float depth = body.bounds.size.y, width = Mathf.Min(body.bounds.size.x, body.bounds.size.z);
            Check(depth > width * .3f && depth < width * .6f, $"underside proportion: depth {depth:0.0} vs width {width:0.0}");
            Check(!prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(m => !(m is MenuDioramaMotion)), "prefab holds only visuals (no game scripts)");
            Check(!prefab.GetComponentsInChildren<Collider>(true).Any(), "prefab has no colliders (menu clicks unaffected)");
            var scene = EditorSceneManager.OpenScene(MenuDioramaBuilder.MenuScenePath);
            var roots = scene.GetRootGameObjects();
            Check(roots.Count(g => g.name == MenuDioramaBuilder.RootName) == 1, "one diorama instance in MenuScene after two runs");
            Check(roots.Count(g => g.name == MenuDioramaBuilder.CameraName) == 1, "one menu camera after two runs");
            Check(roots.Count(g => g.name == MenuDioramaBuilder.LightName) == 1, "one menu light (copied from the game) after two runs");
            var cam = roots.First(g => g.name == MenuDioramaBuilder.CameraName).GetComponent<Camera>();
            Check(cam.GetComponent<SpaceBackground>() != null, "menu camera carries the game's SpaceBackground");
            Check(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Image>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(i => i.sprite != null && i.sprite.name.StartsWith("MainMenu_Garden")).All(i => !i.gameObject.activeInHierarchy), "old garden backdrop is off");
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
            Check(menu != null && menu.SceneLayout, "MainMenuUI uses the scene layout");
            var menuSo = new SerializedObject(menu);
            var panel = (GameObject)menuSo.FindProperty("mainMenuPanel").objectReferenceValue;
            int signs = panel.transform.Cast<Transform>().Count(t => t.name == MenuDioramaBuilder.SignName);
            Check(signs == 1, $"one title sign on the menu panel ({signs})");
            var signTitle = panel.transform.Find(MenuDioramaBuilder.SignName + "/Title").GetComponent<TMPro.TMP_Text>();
            var theme = Resources.Load<ComicUITheme>("ComicUITheme");
            Check(signTitle.font == theme.displayFont, "title is in the kit's display font (Bungee), saved in the scene");
            Check(new SerializedObject(signTitle.GetComponent<LocalizedText>()).FindProperty("key").stringValue == MenuDioramaBuilder.TitleKey, "title text comes from the translation table");
            Check(Mathf.Abs(Mathf.DeltaAngle(0f, panel.transform.Find(MenuDioramaBuilder.SignName).localEulerAngles.z)) > 1f, "sign is tilted");
            var uiMotion = panel.GetComponent<MenuUiMotion>();
            Check(uiMotion != null && uiMotion.Sign != null && uiMotion.ButtonCount == 3, "menu UI motion wired (sign + 3 buttons)");
            var hovers = new[] { "playButton", "optionsButton", "quitButton" }.Select(f => ((UnityEngine.UI.Button)menuSo.FindProperty(f).objectReferenceValue).GetComponent<ComicHoverMotion>()).ToList();
            Check(hovers.All(h => h != null && h.breathe > 0f && Mathf.Abs(h.tilt) > 1f) && hovers[0].tilt >= hovers.Skip(1).Max(h => Mathf.Abs(h.tilt)), "buttons tilt on hover (Play most) and breathe, saved in the scene");
            Capture("e0_edit_mode.png", cam);   // Edit Mode görünümü (çalışma anı hareketi ve uzay dörtgeni yok)
        }
        catch (Exception e) { Finish(e); return; }
        // Play Mode'a girerken alan yeniden yüklenir (statikler sıfırlanır): Edit Mode sonuçları dosyada bekler.
        Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/MenuDioramaVerification_edit.txt", notes);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        double now = EditorApplication.timeSinceStartup;
        if (started == 0) { started = now; next = now + 2; }
        if (now - started > 60) { Finish(new Exception("timeout at phase " + phase)); return; }
        if (now < next) return;
        try
        {
            Camera cam = Camera.main;
            if (phase == 0)
            {
                if (!listening) { listening = true; Application.logMessageReceived += (msg, stack, type) => { if (type == LogType.Exception || type == LogType.Error) { errors++; notes.Add("ERROR " + msg); } }; }
                Check(cam != null && cam.name == MenuDioramaBuilder.CameraName, "play: menu camera is the main camera");
                // Toplu kipte ayar kapalı gelebilir: uzay arka planı yalnız bellekte açılır.
                GameSettings.BackgroundEffects = true; var space = cam.GetComponent<SpaceBackground>(); space.enabled = false; space.enabled = true;
                cam.aspect = 1920f / 1080f;
            }
            else if (phase == 1)
            {
                first = Capture("p1_menu.png", cam, true);
                var play = PlayButton();
                playY = play.anchoredPosition.y; playScale = play.localScale.x;
                var t = UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == "Title" && x.transform.parent.name == MenuDioramaBuilder.SignName);
                Check(t != null && t.text == GameText.Get(MenuDioramaBuilder.TitleKey), $"play: sign shows the localized title ('{t?.text}')");
                t.ForceMeshUpdate();
                Check(t.textBounds.size.x <= t.rectTransform.rect.width + 2f, $"play: title fits the sign ({t.textBounds.size.x:0} / {t.rectTransform.rect.width:0})");
            }
            else if (phase == 2)
            {
                Texture2D second = Capture("p2_menu_sonra.png", cam, true);
                Color32[] a = first.GetPixels32(), b = second.GetPixels32();
                int diff = 0; for (int i = 0; i < a.Length; i += 7) if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 24) diff++;
                Check(diff > 50, $"scene moves between two shots ({diff} sampled pixels changed)");
                var play2 = PlayButton();
                Check(Mathf.Abs(play2.anchoredPosition.y - playY) > .3f, $"play: buttons drift while idle ({playY:0.0} -> {play2.anchoredPosition.y:0.0})");
                Check(Mathf.Abs(play2.localScale.x - playScale) > .001f, $"play: Play button breathes ({playScale:0.000} -> {play2.localScale.x:0.000})");
                // Üstüne gelince eğilir: olay doğrudan verilir, kısa geçişten sonra ölçülür.
                play2.GetComponent<ComicHoverMotion>().OnPointerEnter(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));
                phase = 3; next = now + .35; return;
            }
            else if (phase == 3)
            {
                var play3 = PlayButton();
                float angle = Mathf.DeltaAngle(0f, play3.localEulerAngles.z);
                Check(Mathf.Abs(angle) > 2f, $"play: hovering Play tilts it ({angle:0.0} deg)");
                Capture("p3_menu_hover.png", Camera.main, true);
                Check(errors == 0, $"no errors in play mode ({errors})");
                Finish(null); return;
            }
            phase++;
            next = now + (phase == 2 ? 1.6 : .6);
        }
        catch (Exception e) { Finish(e); }
    }

    static RectTransform PlayButton() =>
        (RectTransform)((UnityEngine.UI.Button)new SerializedObject(UnityEngine.Object.FindAnyObjectByType<MainMenuUI>()).FindProperty("playButton").objectReferenceValue).transform;

    static void Finish(Exception e)
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        var all = new List<string>();
        if (File.Exists("Logs/MenuDioramaVerification_edit.txt") && !notes.Any(n => n.StartsWith("INFO Menü"))) all.AddRange(File.ReadAllLines("Logs/MenuDioramaVerification_edit.txt"));
        all.AddRange(notes);
        File.WriteAllLines("Logs/MenuDioramaVerification.txt", new[] { e == null ? "PASS " + all.Count(n => n.StartsWith("PASS")) + " checks" : "FAIL " + e }.Concat(all));
        EditorApplication.Exit(e == null ? 0 : 1);
    }

    // withUi: ekran üstü arayüz de çekime girer (kameraya geçici bağlanır).
    static Texture2D Capture(string name, Camera camera, bool withUi = false)
    {
        Directory.CreateDirectory("Logs/MenuDiorama");
        Canvas[] canvases = withUi ? UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray() : new Canvas[0];
        foreach (Canvas c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = camera.nearClipPlane + .05f; }
        var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); target.Create();
        RenderTexture old = RenderTexture.active;
        camera.targetTexture = target; Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/MenuDiorama/" + name, image.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = old; target.Release(); UnityEngine.Object.DestroyImmediate(target);
        foreach (Canvas c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
        return image;
    }
}
