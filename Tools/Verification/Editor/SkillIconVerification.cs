using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MechanicsTestLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Yetenek ikonları doğrulaması (12 Kasım 2026). Kurulumu kopyada iki kez çalıştırır (çoğaltmamalı), sonra:
//  - 19 doku Sprite olarak, en çok 256 px; set 21 kimlik, 19'u çizili; dosya adları kimliklerle eşleşir.
//  - Oyunda (Mechanics Test Lab): ağaçtaki her görünür düğüm kendi kimliğinin çizilmiş ikonunu gösterir (eski simge kapalı);
//    ipucu penceresi aynı ikonu portrede gösterir; ikon güncellemesi bellek ayırmaz; hata yok.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method SkillIconVerification.RunBatch
[InitializeOnLoad]
public static class SkillIconVerification
{
    const string Key = "SkillIconVerification";
    static readonly List<string> lines = new();
    static int passed, failed, phase;
    static double next, started;
    static readonly List<string> errors = new();
    static SkillIconVerification() { EditorApplication.update += Tick; }

    static void Check(bool ok, string text) { lines.Add((ok ? "PASS " : "FAIL ") + text); if (ok) passed++; else failed++; }
    static void Save() { SessionState.SetString(Key + ".lines", string.Join("\n", lines)); SessionState.SetInt(Key + ".p", passed); SessionState.SetInt(Key + ".f", failed); }
    static void Load()
    {
        if (lines.Count > 0) return;
        string s = SessionState.GetString(Key + ".lines", ""); if (s.Length > 0) lines.AddRange(s.Split('\n'));
        passed = SessionState.GetInt(Key + ".p", 0); failed = SessionState.GetInt(Key + ".f", 0);
    }

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        try
        {
            lines.Add("INFO " + SkillIconInstaller.Install());
            string second = SkillIconInstaller.Install();
            lines.Add("INFO second run: " + second);
            Check(second.Contains("0 doku ayarlandı") && second.Contains("0 kimlik eklendi") && second.Contains("0 sprite bağlandı")
                  && second.Contains("temada 0 sprite, sahnelerde 0 görsel"), "installer is idempotent (icons and resource swaps)");
            var theme = AssetDatabase.LoadAssetAtPath<ComicUITheme>(SkillIconInstaller.ThemePath);
            Check(theme.coinSprite != null && theme.coinSprite.name == "gold" && theme.ironSprite.name == "iron" && theme.stoneSprite.name == "stone",
                  "UI theme resource icons (HUD, planter shop, round summary) use the new gold / iron / stone drawings");
            var oldSprites = SkillIconInstaller.OldResourceSprites.Select(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>().FirstOrDefault()).Where(x => x != null).ToList();
            int leftovers = 0;
            foreach (string path in SkillIconInstaller.ResourceScenes)
            {
                if (!File.Exists(path)) continue;
                var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                leftovers += sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Image>(true)).Count(i => oldSprites.Contains(i.sprite));
            }
            Check(leftovers == 0, $"no scene image shows the old resource sprites any more ({leftovers})");
            var importers = Directory.GetFiles(SkillIconInstaller.Folder, "*.png").Select(p => AssetImporter.GetAtPath(p.Replace('\\', '/')) as TextureImporter).ToList();
            Check(importers.Count == 19 && importers.All(i => i != null && i.textureType == TextureImporterType.Sprite && i.maxTextureSize <= 256 && i.alphaIsTransparency && i.mipmapEnabled),
                  $"19 icons imported as sprites, at most 256 px, alpha, mipmapped ({importers.Count})");
            var set = AssetDatabase.LoadAssetAtPath<SkillIconSetSO>(SkillIconInstaller.SetPath);
            Check(set != null && set.entries.Count == 21 && set.entries.Count(e => e.sprite != null) == 19 && set.entries.Where(e => e.sprite != null).All(e => e.sprite.name == e.id),
                  $"icon set: 21 ids, 19 drawn, file names match ids ({set?.entries.Count(e => e.sprite != null)}/{set?.entries.Count})");
        }
        catch (Exception e) { Check(false, "edit checks threw: " + e); Finish(); return; }
        Save();
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        EditorSceneManager.OpenScene(Lab.GameScenePath);
        SessionState.SetBool(Key, true);
        Scenario s = ScenarioPresets.New("Yetenek ikonları", "", "", "", 3);
        s.autoAttack = false;
        Result r = TestSession.StartPlay(s);
        if (!r.Ok) { Check(false, "lab: " + r.Message); Finish(); }
    }

    static void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (stack != null && stack.Contains("UnityEditor.Search")) return;
        errors.Add(message);
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorApplication.QueuePlayerLoopUpdate();
        double now = EditorApplication.timeSinceStartup;
        if (started == 0) { Load(); Application.logMessageReceived += OnLog; started = now; next = now + 3; return; }
        if (now - started > 120) { Check(false, "timeout at phase " + phase); Finish(); return; }
        if (now < next || !TestSession.Ready) return;
        try
        {
            var nodes = UnityEngine.Object.FindObjectsByType<SkillNodeUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(n => n.Node != null).ToList();
            var refresh = typeof(SkillNodeUI).GetMethod("RefreshIcon", BindingFlags.Instance | BindingFlags.NonPublic);
            var iconField = typeof(SkillNodeUI).GetField("iconImage", BindingFlags.Instance | BindingFlags.NonPublic);
            var badgeField = typeof(SkillNodeUI).GetField("skillBadge", BindingFlags.Instance | BindingFlags.NonPublic);
            if (phase == 0)
            {
                int ok = 0, wrong = 0, fallback = 0; var used = new HashSet<string>(); var fallbackIds = new HashSet<string>();
                foreach (var n in nodes)
                {
                    refresh.Invoke(n, null);
                    string recipe = SkillIconCatalog.For(n.Node);
                    Sprite expected = SkillIconSetSO.For(recipe);
                    var image = (Image)iconField.GetValue(n);
                    var badge = (Component)badgeField.GetValue(n);
                    if (expected != null)
                    {
                        bool good = image.enabled && image.sprite == expected && (badge == null || !badge.gameObject.activeSelf);
                        if (good) { ok++; used.Add(recipe); } else wrong++;
                    }
                    else
                    {
                        bool good = !image.enabled && badge != null && badge.gameObject.activeSelf;   // çizimi yok: eski simge
                        if (good) { fallback++; fallbackIds.Add(recipe + (n.isActiveAndEnabled ? "" : " (gizli yuva)")); } else wrong++;
                    }
                }
                lines.Add($"INFO nodes without a drawing keep the old badge: {fallback} ({string.Join(", ", fallbackIds)})");
                Check(nodes.Count >= 100 && wrong == 0, $"every tree node shows its drawn icon ({ok} nodes), ids without a drawing keep the old badge ({fallback}); wrong {wrong}");
                Check(used.Count == 19, $"all 19 drawn icons appear in the tree ({used.Count})");
                // ipucu: aynı ikon portrede
                var view = ComicPopupView.Attach(new GameObject("Tooltip probe", typeof(RectTransform)));
                view.Begin("probe", "", null, "", "skill-attack");
                var portrait = (Image)typeof(ComicPopupView).GetField("portrait", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                var skillPortrait = (Component)typeof(ComicPopupView).GetField("skillPortrait", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                Check(portrait.enabled && portrait.sprite == SkillIconSetSO.For("skill-attack") && !skillPortrait.gameObject.activeSelf, "tooltip header shows the same drawn icon");
                view.Begin("probe", "", null, "", "skill-time");   // çizilmemiş kimlik: eski simge
                Check(!portrait.enabled && skillPortrait.gameObject.activeSelf, "an id without a drawing falls back to the old drawn badge");
                // bellek: ikon araması kare başına ayırmaz
                SkillIconSetSO.For("skill-gold");
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 500; i++) SkillIconSetSO.For("skill-gold");
                Check(GC.GetAllocatedBytesForCurrentThread() - before == 0, "icon lookup allocates nothing");
                // HUD kaynak ikonları (ComicHud temadan kurar)
                var hudIcons = UnityEngine.Object.FindObjectsByType<GoldUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Select(g => g.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name == "Icon"))
                    .Where(i => i != null).Select(i => i.sprite != null ? i.sprite.name : "null").ToList();
                lines.Add("INFO HUD resource icons: " + string.Join(", ", hudIcons));
                Check(hudIcons.Count == 3 && hudIcons.OrderBy(x => x).SequenceEqual(new[] { "gold", "iron", "stone" }), $"HUD resource cards show the new drawings ({string.Join(", ", hudIcons)})");
                phase = 1; next = now + 1; return;
            }
            Check(errors.Count == 0, $"no errors in play ({errors.Count}){(errors.Count > 0 ? ": " + errors[0] : "")}");
            Finish();
        }
        catch (Exception e) { Check(false, $"play phase {phase} threw: {e}"); Finish(); }
    }

    static void Finish()
    {
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= OnLog;
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/SkillIconVerification.txt", new[] { $"{(failed == 0 ? "PASS" : "FAIL")} {passed} passed, {failed} failed" }.Concat(lines));
        EditorApplication.Exit(failed == 0 ? 0 : 1);
    }
}
