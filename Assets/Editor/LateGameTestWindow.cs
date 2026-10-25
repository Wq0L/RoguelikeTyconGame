using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Bölüm 3.8 Play kontrol penceresi (yalnız Editor, yalnız Play modunda): Tools > Geç Oyun Testi.
// - Dört tarla olayını o anki round'da tek tek başlatır (oyunun kendi yürütücüsüyle: FieldEventDirector.StartNow).
// - Bütün boss ödüllerini o anki run'a verir (oyunun kendi denetimi ve tek işlemli uygulamasıyla: BossRewardManager.EditorGrant;
//   ön koşul, koşul ve stack sınırı atlanmaz — reddin nedeni yazılır).
// - Aktif etkileri, bekleme sürelerini, bütçeleri ve saldırı duraklamasını gösterir; tarla olayının fazını ve hedeflerini gösterir.
// - Hepsini oyunun tek temizlik yollarıyla kaldırır.
// Hiçbir şey kalıcı kayda (MetaSave) ya da profil seçimine yazılmaz; verilen ödül ve başlatılan olay yalnız bu Play oturumundaki
// run'a aittir. Bu dosya Editor derlemesindedir: oyuncu build'ine girmez. Bu pencere tam oyunda (GameScene) çekim ve hızlı deneme
// panelidir; kapsamlı mekanik testi ayrı test sahnesindedir (Tools > Mechanics Test Lab). Ödül kataloğunun gezilmesi iki araçta
// ortaktır (MechanicsTestLab.LabCatalog).
public sealed class LateGameTestWindow : EditorWindow
{
    private const string DefaultPoolPath = "Assets/ScriptableObjects/BossRewards/GecOyunV1/BossRewardPool_GecOyunV1.asset";
    private BossRewardPoolSO loadedPool;
    private string rewardSearch = "";
    private const string PlanPath = "Assets/ScriptableObjects/FieldEvents/GecOyunV1/FieldEventPlan_GecOyunV1.asset";
    private static readonly string[] EventNames = { "Diken Kuşatması", "İstilacı Filiz", "Aşılı Kabuk", "Gezgin Bereket" };

    private readonly List<BossRewardSO> rewards = new();
    private readonly List<int> placements = new();
    private readonly StringBuilder text = new();
    private Vector2 scroll;
    private string lastMessage = "";
    private float eventDelay = 1f;
    private int eventSeed = 1;
    // 25 Ekim 2026 (kullanıcı: "hep aynı yerde çıkıyor"): sabit seed her basışta aynı bölgeyi seçiyordu. Açıkken her basış yeni seed
    // üretir ve kutuya yazar (aynı yeri tekrar görmek için kapatıp o seed'le başlat). Oyunun kendi akışı zaten run seed'iyle rastgele.
    private bool eventRandomSeed = true;
    private int captureRound=1, captureAmount=10000;
    private ResourceType captureResource=ResourceType.Gold;

    [MenuItem("Tools/Geç Oyun Testi (Bölüm 3.8 · yalnız Play)", priority = 110)]
    private static void Open() => GetWindow<LateGameTestWindow>("Geç Oyun Testi");

    private void OnEnable() => LoadRewards();
    private void OnInspectorUpdate() { if (EditorApplication.isPlaying) Repaint(); }

    private void LoadRewards()
    {
        rewards.Clear();
        loadedPool = BossRewardManager.Instance != null ? BossRewardManager.Instance.Pool : null;
        BossRewardPoolSO pool = loadedPool != null ? loadedPool : AssetDatabase.LoadAssetAtPath<BossRewardPoolSO>(DefaultPoolPath);
        if (pool == null) return;
        // Gerçek havuzun asset referanslarını koru: eski profil kopyaları ayrı stack yaratmasın (ortak katalog okuması).
        MechanicsTestLab.LabCatalog.Rewards(pool, rewards);
        // Ön koşulu olmayanlar önce (aynı ailenin sırası: anten → hat → devre).
        rewards.Sort((a, b) => Depth(a) != Depth(b) ? Depth(a).CompareTo(Depth(b)) : string.CompareOrdinal(a.id, b.id));
    }

    private static int Depth(BossRewardSO reward) => MechanicsTestLab.LabCatalog.Depth(reward);

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Play modunda kullanılır. GameScene'i aç, Tools > Run Profili'nden bir profil seç (öneri: Run50 Geç Oyun V1) ve Play'e bas.\n" +
                                    "Bu pencere kayda ve profil seçimine yazmaz.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }
        RoundManager rounds = RoundManager.Instance;
        bool inRound = rounds != null && rounds.IsRoundActive && GameManager.Instance != null && GameManager.Instance.CurrentState == GameStates.Round;
        EditorGUILayout.LabelField("Profil", rounds != null && rounds.Profile != null ? rounds.Profile.displayName : "yok");
        EditorGUILayout.LabelField("Durum", rounds == null ? "RoundManager yok" : $"Round {rounds.CurrentRound} · {(inRound ? "aktif" : "round dışında")}" + (DemoSceneSettings.IsDemo ? " · DEMO (olay ve yeni ödül yok)" : ""));
        if (!string.IsNullOrEmpty(lastMessage)) EditorGUILayout.HelpBox(lastMessage, MessageType.None);

        DrawCaptureTools(rounds,inRound);
        EditorGUILayout.Space(8);
        DrawMeteorFeel();
        EditorGUILayout.Space(8);
        DrawEvents(inRound);
        EditorGUILayout.Space(8);
        DrawRewards();
        EditorGUILayout.Space(8);
        DrawEffects();
        EditorGUILayout.Space(8);
        using (new EditorGUI.DisabledScope(BossRewardManager.Instance == null))
            if (GUILayout.Button("Bütün test etkilerini temizle (olay + bu run'ın BÜTÜN boss ödülleri ve etkileri)"))
            {
                if (FieldEventDirector.Instance != null) FieldEventDirector.Instance.End();
                BossRewardManager.Instance.ClearAll();
                LateGamePresenter.DebugOutlines = false;
                lastMessage = "Temizlendi: tarla olayı, boss ödülleri, etkiler, geçici stat'lar ve saldırı duraklaması (oyunun kendi temizlik yolları).";
            }
        EditorGUILayout.EndScrollView();
    }

    // Hasat Yankısı meteoru his katmanları (24 Ekim 2026): oynarken tek tek kapatıp eksik hissi bulmak için. Kayda yazmaz.
    private void DrawMeteorFeel()
    {
        EditorGUILayout.LabelField("Meteor hissi (Hasat Yankısı)", EditorStyles.boldLabel);
        LateGamePresenter.MeteorMotion = EditorGUILayout.Toggle("2 · Hareket (yuvarlanma, alev kaynaması)", LateGamePresenter.MeteorMotion);
        LateGamePresenter.MeteorImpactFrame = EditorGUILayout.Toggle("3 · Darbe karesi (uzama/basılma, parlama)", LateGamePresenter.MeteorImpactFrame);
        LateGamePresenter.MeteorFieldReaction = EditorGUILayout.Toggle("4 · Tarla tepkisi (yakın bitkiler esner)", LateGamePresenter.MeteorFieldReaction);
        EditorGUILayout.LabelField("Birini kapatınca \"boş\" geliyorsa o katman şart; fark yoksa ya da kalabalıksa çıkarılır.", EditorStyles.miniLabel);
    }

    private void DrawCaptureTools(RoundManager rounds,bool inRound)
    {
        EditorGUILayout.LabelField("Çekim / test araçları",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Round geçişi mevcut tarla, seviye ve alınan güçleri korur; atlanan round ödüllerini vermez. Bu araçlar kullanıldığında Play oturumu boyunca kalıcı kayıt yazılması kapatılır.",MessageType.Info);
        using(new EditorGUI.DisabledScope(rounds==null || DemoSceneSettings.IsDemo))
        {
            captureRound=EditorGUILayout.IntField("Hedef round",captureRound);
            captureRound=Mathf.Clamp(captureRound,1,rounds!=null?rounds.MaxRounds:50);
            if(GUILayout.Button("Seçilen round'a git / yeniden başlat"))
                lastMessage=rounds.EditorJumpToRound(captureRound)?$"Round {rounds.CurrentRound} başladı. Mevcut tarla ve güçler korundu.":"Round geçişi yapılamadı; profil ve oyun durumunu kontrol et.";
            using(new EditorGUI.DisabledScope(!inRound))
                if(GUILayout.Button("Round süresine +60 saniye")){rounds.EditorAddRoundTime(60);lastMessage="Çekim için 60 saniye eklendi.";}
        }
        captureResource=(ResourceType)EditorGUILayout.EnumPopup("Kaynak",captureResource);
        captureAmount=Mathf.Max(1,EditorGUILayout.IntField("Eklenecek miktar",captureAmount));
        using(new EditorGUI.DisabledScope(ResourceManager.Instance==null || DemoSceneSettings.IsDemo))
            if(GUILayout.Button("Seçilen kaynağı ekle"))
            {
                MetaSave.EditorProtectTestSession();
                ResourceManager.Instance.AddResource(captureResource,captureAmount);
                lastMessage=$"{captureResource} eklendi. Yeni bakiye: {ResourceManager.Instance.GetResourceAmount(captureResource):N0}";
            }
        if(MetaSave.EditorSessionMemoryOnly)EditorGUILayout.LabelField("Kayıt: yalnız bu Play oturumunun belleğinde",EditorStyles.miniLabel);
    }

    private static int NewEventSeed() => (System.Environment.TickCount ^ System.DateTime.Now.Millisecond * 7919) & 0x7FFFFFFF | 1;

    private void DrawEvents(bool inRound)
    {
        EditorGUILayout.LabelField("Tarla olayları", EditorStyles.boldLabel);
        FieldEventDirector director = FieldEventDirector.Instance;
        if (director == null) { EditorGUILayout.LabelField("FieldEventDirector yok."); return; }
        eventDelay = EditorGUILayout.Slider("Başlangıç gecikmesi (sn)", eventDelay, 0f, 6f);
        eventRandomSeed = EditorGUILayout.Toggle("Rastgele yer (her basışta yeni seed)", eventRandomSeed);
        using (new EditorGUI.DisabledScope(eventRandomSeed))
            eventSeed = EditorGUILayout.IntField(eventRandomSeed ? "Son kullanılan seed" : "Bölge seed'i", eventSeed);
        LateGamePresenter.DebugOutlines = EditorGUILayout.Toggle("Kesin alan çizgisi (test görünümü)", LateGamePresenter.DebugOutlines);
        using (new EditorGUI.DisabledScope(!inRound || DemoSceneSettings.IsDemo))
        {
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < EventNames.Length; i++)
                if (GUILayout.Button(EventNames[i]))
                {
                    // Profilin planı yoksa (ör. Alpha profili seçili) Geç Oyun V1 planı yalnız bu çağrı için kullanılır.
                    FieldEventPlanSO plan = director.Plan != null ? director.Plan : AssetDatabase.LoadAssetAtPath<FieldEventPlanSO>(PlanPath);
                    if (eventRandomSeed) eventSeed = NewEventSeed();
                    bool started = director.StartNow((FieldEventKind)i, eventDelay, eventSeed, plan);
                    lastMessage = started ? $"{EventNames[i]} kuruldu (seed {eventSeed}; hazırlık {eventDelay:0.#} sn sonra başlar)." : $"{EventNames[i]} kurulamadı: uygun bölge yok ya da plan bulunamadı.";
                }
            EditorGUILayout.EndHorizontal();
        }
        if (!inRound) EditorGUILayout.LabelField("Olay yalnız aktif round'da başlatılır.", EditorStyles.miniLabel);
        FieldEvent current = director.Current;
        if (current == null) { EditorGUILayout.LabelField("Aktif olay", "yok"); return; }
        text.Clear();
        text.Append(current.Title).Append(" · faz ").Append(current.Phase).Append(" · ").Append(current.PhaseTime.ToString("0.0")).Append(" sn\n").Append(current.Status).Append('\n');
        for (int z = 0; z < current.ZoneCount; z++)
            text.Append("bölge ").Append(z).Append(": ").Append(current.Zone(z).Count).Append(" hücre").Append(current.ZoneClosed(z) ? " · KAPALI" : "").Append('\n');
        for (int i = 0; i < current.Targets.Count; i++)
        {
            InterventionTarget target = current.Targets[i];
            text.Append("hedef ").Append(i).Append(" (").Append(target.Cell.x).Append(", ").Append(target.Cell.z).Append("): ").Append(target.Remaining).Append('/').Append(target.Durability)
                .Append(target.Broken ? " · kırıldı" : target.Hittable ? " · vurulabilir" : " · kapalı").Append('\n');
        }
        text.Append("üretimi kapalı hücre ").Append(director.Zones.BlockedCells).Append(" · yavaşlamış hücre ").Append(director.Zones.SlowedCells);
        EditorGUILayout.HelpBox(text.ToString(), MessageType.None);
        if (GUILayout.Button("Olayı temizle")) { director.End(); lastMessage = "Tarla olayı temizlendi."; }
    }

    private void DrawRewards()
    {
        EditorGUILayout.LabelField("Bütün boss buff’ları (bu run’a ver)", EditorStyles.boldLabel);
        BossRewardManager boss = BossRewardManager.Instance;
        if (boss == null) { EditorGUILayout.LabelField("BossRewardManager yok."); return; }
        if (loadedPool != boss.Pool) LoadRewards();
        EditorGUILayout.BeginHorizontal();
        rewardSearch = EditorGUILayout.TextField("Buff ara", rewardSearch);
        if (GUILayout.Button("Yenile", GUILayout.Width(60f))) LoadRewards();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField($"{rewards.Count} boss buff’ı · tüm aşamalar · " + (loadedPool != null ? loadedPool.name : "Geç Oyun V1"), EditorStyles.miniLabel);
        if (rewards.Count == 0) { EditorGUILayout.LabelField("Bu profilin boss ödül havuzu boş."); return; }
        foreach (BossRewardSO reward in rewards)
        {
            if (!MatchesSearch(reward, rewardSearch)) continue;
            EditorGUILayout.BeginHorizontal();
            int stacks = boss.Stacks(reward);
            BossRewardBlock block = boss.BlockOf(reward, true);
            string state = $"{stacks}/{reward.maxStacks} · " + (block == BossRewardBlock.None ? "alınabilir" : "alınamaz: " + BlockText(reward, block));
            EditorGUILayout.LabelField(new GUIContent(reward.displayName, boss.NoteFor(reward)), new GUIContent(state));
            using (new EditorGUI.DisabledScope(DemoSceneSettings.IsDemo || block != BossRewardBlock.None || boss.IsPending))
                if (GUILayout.Button("Ver", GUILayout.Width(60f)))
                {
                    bool granted = GrantReward(boss, reward, out string refusal, out int placement);
                    lastMessage = granted
                        ? $"{reward.displayName} verildi" + (placement != RunEffectSO.NoPlacement ? $" (konum: {AntennaSlots.Name(placement)})." : ".")
                        : $"{reward.displayName} verilmedi ({refusal}).";
                }
            EditorGUILayout.EndHorizontal();
        }
        if (boss.IsPending) EditorGUILayout.LabelField("Bekleyen bir boss teklifi varken ödül verilmez.", EditorStyles.miniLabel);
    }

    private static bool MatchesSearch(BossRewardSO reward, string query) =>
        string.IsNullOrWhiteSpace(query) ||
        (reward.displayName ?? "").IndexOf(query.Trim(), System.StringComparison.CurrentCultureIgnoreCase) >= 0 ||
        (reward.id ?? "").IndexOf(query.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0;

    private bool GrantReward(BossRewardManager boss, BossRewardSO reward, out string refusal, out int placement)
    {
        placement = RunEffectSO.NoPlacement;
        if (!EditorApplication.isPlaying || DemoSceneSettings.IsDemo) { refusal = "yalnız tam sürüm Play"; return false; }
        if (reward.effect != null && reward.effect.NeedsPlacement && RunEffects.Instance != null)
        {
            placements.Clear();
            reward.effect.CollectPlacements(RunEffects.Instance, placements);
            if (placements.Count > 0) placement = placements[placements.Count / 2];
        }
        MetaSave.EditorProtectTestSession();
        return boss.EditorGrant(reward, out refusal, placement);
    }

    private static string BlockText(BossRewardSO reward, BossRewardBlock block) => block switch
    {
        BossRewardBlock.Prerequisite => "önce " + (reward.requires != null ? reward.requires.displayName : "ön koşul"),
        BossRewardBlock.Condition => "koşul tutmuyor (davranışlı saksı / kritik şansı / konum yok)",
        BossRewardBlock.StackLimit => "stack sınırı",
        BossRewardBlock.Exclusive => "dışlayan ödül alınmış",
        _ => block.ToString(),
    };

    private void DrawEffects()
    {
        EditorGUILayout.LabelField("Aktif etkiler · bekleme · bütçe", EditorStyles.boldLabel);
        RunEffects effects = RunEffects.Instance;
        if (effects == null || effects.Active.Count == 0) { EditorGUILayout.LabelField("Aktif etki yok."); return; }
        text.Clear();
        text.Append("round saati ").Append(effects.Clock.ToString("0.0")).Append(" sn · bekleyen iş ").Append(effects.PendingTimers).Append('/').Append(RunEffects.TimerCapacity)
            .Append(" · düşen iş ").Append(effects.DroppedTimers).Append('\n');
        text.Append("saldırı duraklaması: ").Append(DirectAttackPause.Active ? $"AKTİF · kalan {DirectAttackPause.Remaining:0.00} sn" : "yok")
            .Append(" · istek ").Append(DirectAttackPause.Requests).Append(" · toplam ").Append(DirectAttackPause.PausedSeconds.ToString("0.00")).Append(" sn (sert sınır ")
            .Append(DirectAttackPause.MaxSeconds.ToString("0.0")).Append(" sn)\n");
        foreach (RunEffect effect in effects.Active)
        {
            text.Append("— ").Append(effect.Data != null ? effect.Data.name : effect.GetType().Name).Append('\n');
            effect.Describe(text);
        }
        EditorGUILayout.HelpBox(text.ToString().TrimEnd('\n'), MessageType.None);
    }
}
