using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MechanicsTestLab;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Sınama ödülü kartlarının yazıları sığıyor mu (26 Kasım 2026: AI kokan yazılar sade cümlelere çevrildi, kural ve notlar uzadı).
// Gerçek GameScene + Mechanics Test Lab oturumu: oyunun kendi akışıyla ilk Sınama turu biter, ödül ekranı açılır; sonra teklif
// sırayla projedeki bütün ödüllerle (3'erli) doldurulur. Her kartın başlık, etki, not, alt satır ve aşama yazısı kutusuna sığmalı
// (Türkçe ve İngilizce). Görüntüler: Logs/RewardFit/<dil>_<grup>.png, sonuç Logs/RewardTextFitVerification.txt.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method RewardTextFitVerification.RunBatch -Full
[InitializeOnLoad]
public static class RewardTextFitVerification
{
    const string Key = "RewardTextFitVerification";
    const int W = 1920, H = 1080;
    static readonly string[] Languages = { "tr", "en" };
    static readonly List<string> lines = new();
    static readonly List<BossRewardSO> rewards = new();
    static int phase, group, languageIndex;
    static double next, started;
    static RenderTexture target;
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static RewardTextFitVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        SessionState.SetBool(Key, true);
        Scenario s = ScenarioPresets.New("Ödül yazıları", "", "", "", 5);
        s.autoAttack = false;
        ScenarioPresets.Planter(s, ScenarioPresets.Small(), 5, 5);
        Result r = TestSession.StartPlay(s);
        if (!r.Ok) throw new Exception(r.Message);
    }

    static BossRewardManager Boss => BossRewardManager.Instance;
    static BossRewardPanelUI Panel => UnityEngine.Object.FindAnyObjectByType<BossRewardPanelUI>(FindObjectsInactive.Include);

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        double now = EditorApplication.timeSinceStartup;
        if (started == 0) { started = now; next = now + 2; return; }
        if (now - started > 300) { lines.Add("FAIL timeout at phase " + phase); Finish(); return; }
        if (now < next || !TestSession.Ready) return;
        try
        {
            Camera cam = Camera.main;
            switch (phase)
            {
                case 0:
                {
                    target = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32); target.Create();
                    cam.targetTexture = target;
                    foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay))
                    { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = cam.nearClipPlane + .05f; c.sortingOrder += 1000; }
                    Type.GetType("FeelBootstrap, Assembly-CSharp").GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    int bossRound = TestRewards.NextBossRound(RoundManager.Instance, 1);
                    Result open = TestRewards.OpenNormalOffer(bossRound);
                    lines.Add((open.Ok ? "INFO " : "FAIL ") + $"R{bossRound} Sınama turu oyunun akışıyla bitti: {open.Message}");
                    if (!open.Ok) { Finish(); return; }
                    // Projedeki bütün ödüller (eski profillerinkiler dahil), ad başına bir tane.
                    foreach (string guid in AssetDatabase.FindAssets("t:BossRewardSO", new[] { "Assets/ScriptableObjects" }))
                    {
                        var reward = AssetDatabase.LoadAssetAtPath<BossRewardSO>(AssetDatabase.GUIDToAssetPath(guid));
                        if (reward != null && rewards.All(r => r.name != reward.name)) rewards.Add(reward);
                    }
                    phase = 1; next = now + 2.5; return;
                }
                case 1:
                {
                    // Round sonunda seviye atlandıysa önce kart seçimi gelir; seçimler ödülsüz geçilir, sonra ödül ekranı açılır.
                    if (GameManager.Instance.CurrentState == GameStates.CardSelection)
                    {
                        while (RoundManager.Instance.OnCardSelectionComplete()) { }
                        next = now + 1.5; return;
                    }
                    bool shown =Boss != null && Boss.IsPending && Panel != null && Panel.gameObject.activeInHierarchy;
                    Check(shown, "Sınama ödülü ekranı açık", $"{rewards.Count} ödül sırayla gösterilecek");
                    if (!shown) { Finish(); return; }
                    GameText.Language = Languages[languageIndex];
                    group = 0;
                    phase = 2; next = now + .4; return;
                }
                case 2:
                {
                    // Teklifi bu grubun ödülleriyle değiştir; panel sürüm değişince kendini yeniden doldurur.
                    var offer = (List<BossRewardSO>)typeof(BossRewardManager).GetField("offer", Any).GetValue(Boss);
                    offer.Clear();
                    offer.AddRange(rewards.Skip(group * 3).Take(3));
                    typeof(BossRewardManager).GetField("offerPrepared", Any).SetValue(Boss, true);
                    PropertyInfo version = typeof(BossRewardManager).GetProperty("Version");
                    version.GetSetMethod(true).Invoke(Boss, new object[] { Boss.Version + 1 });
                    phase = 3; next = now + .5; return;
                }
                case 3:
                {
                    string language = Languages[languageIndex];
                    FitCards(language);
                    Capture($"{language}_{group + 1:00}.png");
                    group++;
                    if (group * 3 < rewards.Count) { phase = 2; next = now + .1; return; }
                    languageIndex++;
                    if (languageIndex < Languages.Length) { GameText.Language = Languages[languageIndex]; group = 0; phase = 2; next = now + .6; return; }
                    GameText.Language = "tr";
                    Finish(); return;
                }
            }
        }
        catch (Exception e) { lines.Add("FAIL phase " + phase + ": " + e); Finish(); }
    }

    // Kartın her görünen yazısı kutusuna sığar (taşan satır, kesilen yazı yok).
    static void FitCards(string language)
    {
        Canvas.ForceUpdateCanvases();
        var cards = (IList)typeof(BossRewardPanelUI).GetField("cards", Any).GetValue(Panel);
        var bad = new List<string>();
        var names = new List<string>();
        float smallest = float.MaxValue;
        foreach (object card in cards)
        {
            var root = (RectTransform)card.GetType().GetField("root").GetValue(card);
            if (!root.gameObject.activeInHierarchy) continue;
            var reward = (BossRewardSO)card.GetType().GetField("reward").GetValue(card);
            names.Add(reward.DisplayName);
            foreach (string field in new[] { "title", "effect", "note", "stack", "stage" })
            {
                var text = (TMP_Text)card.GetType().GetField(field).GetValue(card);
                if (text == null || !text.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(text.text)) continue;
                text.ForceMeshUpdate();
                Rect r = text.rectTransform.rect;
                Vector4 m = text.margin;
                Bounds b = text.textBounds;
                bool fits = b.min.x >= r.xMin + m.x - 2f && b.max.x <= r.xMax - m.z + 2f && b.min.y >= r.yMin + m.w - 2f && b.max.y <= r.yMax - m.y + 2f;
                if (!fits || text.isTextTruncated)
                    bad.Add($"{reward.name}/{field} '{Short(text.text)}' {b.size.x:0}×{b.size.y:0} / {r.width - m.x - m.z:0}×{r.height - m.y - m.w:0}, {text.fontSize:0.#} pt");
                if (text.enableAutoSizing && text.fontSize < smallest) smallest = text.fontSize;
            }
        }
        Check(bad.Count == 0, $"[{language}] grup {group + 1}: kart yazıları sığıyor ({string.Join(", ", names)})",
            bad.Count == 0 ? $"en küçük otomatik boy {(smallest == float.MaxValue ? 0 : smallest):0.#} pt" : string.Join(" | ", bad));
    }

    static string Short(string s)
    {
        s = System.Text.RegularExpressions.Regex.Replace(s ?? "", "<.*?>", "").Replace('\n', '/');
        return s.Length > 60 ? s.Substring(0, 60) + "…" : s;
    }

    static void Check(bool ok, string what, string detail = null) => lines.Add($"{(ok ? "PASS" : "FAIL")} {what}{(string.IsNullOrEmpty(detail) ? "" : " — " + detail)}");

    static void Capture(string name)
    {
        Camera.main.Render();
        Directory.CreateDirectory("Logs/RewardFit");
        RenderTexture old = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(W, H, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, W, H), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/RewardFit/" + name, image.EncodeToPNG());
        RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image);
    }

    static void Finish()
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        int pass = lines.Count(l => l.StartsWith("PASS")), fail = lines.Count(l => l.StartsWith("FAIL"));
        File.WriteAllLines("Logs/RewardTextFitVerification.txt", new[] { $"{(fail > 0 ? "FAIL" : "DONE")} {pass}/{pass + fail}" }.Concat(lines));
        if (Camera.main != null) Camera.main.targetTexture = null;
        if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        EditorApplication.Exit(fail > 0 ? 1 : 0);
    }
}
