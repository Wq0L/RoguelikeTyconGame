using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Boss ödülü tekliflerini sabit koşullarda üretmek için ortak yardımcılar (test ve teklif dağılımı ölçümü kullanır).
// Sahne Play'de olmalı (GameScene, RunSetup). Tarla düzeni elle kurulur, oyuncu vuruşu yoktur: teklif yalnız uygunluk
// durumuna ve seed'e bağlıdır. Teklif, oyunun kendi BuildOffer yoluyla üretilir; burada teklif kuralı yeniden yazılmaz.
public static class RewardOfferLab
{
    public enum Layout { Empty, Direct, Explosive, Electric, Multi }

    public static string Name(Layout layout) => layout switch
    {
        Layout.Empty => "boş tarla",
        Layout.Direct => "doğrudan vuruş (davranışsız saksılar)",
        Layout.Explosive => "patlamalı",
        Layout.Electric => "elektrikli",
        _ => "çok davranışlı (patlama + elektrik + kasırga + bumerang)",
    };

    static BossRewardManager Boss => BossRewardManager.Instance;
    static T F<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void SetF(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, v);
    static void SetP(object o, string n, object v) => o.GetType().GetProperty(n).GetSetMethod(true).Invoke(o, new[] { v });

    public static TileModifierSO Tile(string type, string rarity = "Common")
    {
        var tile = AssetDatabase.LoadAssetAtPath<TileModifierSO>($"Assets/ScriptableObjects/GridModifiers/{type}/{type}-{rarity}.asset");
        if (tile == null) throw new Exception($"tile asset missing: {type}-{rarity}");
        return tile;
    }

    public static List<GroundCell> OpenCells()
    {
        var grid = GridManager.Instance.GetGridSystem(); var list = new List<GroundCell>();
        for (int x = 0; x < GridManager.Instance.GetWidth(); x++) for (int z = 0; z < GridManager.Instance.GetHeight(); z++)
        {
            var ground = grid.GetGridObject(new GridPosition(x, z))?.GetGroundCellCached();
            if (ground != null && !ground.IsLocked) list.Add(ground);
        }
        return list;
    }

    public static void ClearField()
    {
        foreach (var brain in Object.FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None).ToList()) brain.RemoveSelf();
        foreach (var ground in OpenCells()) ground.ApplyModifier(null);
    }

    public static PlanterBrain Place(GroundCell ground, TileModifierSO tile = null, float value = 1f)
    {
        var so = AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 1x1.asset");
        var cell = GridManager.Instance.GetGridSystem().GetGridObject(ground.GetGridPosition());
        if (tile != null)
            ground.ApplyModifier(tile, tile.modifierRanges.Select(r => new StatModifier { statType = r.statType, target = r.target, operation = r.operation, value = value }).ToList());
        var planter = Object.Instantiate(so.prefab);
        planter.transform.position = ground.transform.position;
        cell.SetPlanterObject(planter);
        var brain = planter.GetComponent<PlanterBrain>(); brain.Initialize(so, new List<GridObject> { cell });
        cell.SetPlanterBrain(brain);
        return brain;
    }

    // Açık hücrelerin hepsine 1×1 saksı; düzene göre dört hücrenin altına davranış tile'ı (sabit şans değerleriyle).
    public static void Build(Layout layout)
    {
        ClearField();
        if (layout == Layout.Empty) return;
        var cells = OpenCells().OrderBy(c => c.GetGridPosition().x * 100 + c.GetGridPosition().z).ToList();
        for (int i = 0; i < cells.Count; i++)
        {
            TileModifierSO tile = null; float value = 1f;
            bool slot = i % 2 == 0 && i < 8;   // 0, 2, 4, 6
            if (slot && layout == Layout.Explosive) { tile = Tile("Explosive"); value = .30f; }
            if (slot && layout == Layout.Electric) { tile = Tile("Electric"); value = .40f; }
            if (slot && layout == Layout.Multi)
            {
                (string type, float v) = i == 0 ? ("Explosive", .30f) : i == 2 ? ("Electric", .40f) : i == 4 ? ("Tornado", .22f) : ("Boomerang", .18f);
                tile = Tile(type); value = v;
            }
            Place(cells[i], tile, value);
        }
    }

    // Son round'un ortalama tarla doluluğu ("tarla boşalıyor" koşulu bunu okur).
    public static void SetOccupancy(float value) => SetP(Boss, "LastRoundOccupancy", value);

    // Ödülü oyunun kendi Choose yoluyla verir (teklif enjekte edilir). Sınırdaysa false.
    public static bool Grant(BossRewardSO reward)
    {
        var offer = F<List<BossRewardSO>>(Boss, "offer");
        offer.Clear(); offer.Add(reward);
        SetF(Boss, "offerPrepared", true);
        SetP(Boss, "IsPending", true);
        bool taken = Boss.Choose(reward);
        if (!taken) { SetP(Boss, "IsPending", false); offer.Clear(); SetF(Boss, "offerPrepared", false); }
        return taken;
    }

    // Teklif üretimi sırasında oyunun rastgelelik akışının (UnityEngine.Random) değişmediği kaç kez denetlendi.
    public static int RandomChecks { get; private set; }

    // O boss round'u ve run seed'i için teklif: oyunun BuildOffer'ı, o anki uygunluk durumuyla. Yöneticinin o anki teklifini,
    // teklif round'unu ve run seed'ini olduğu gibi geri bırakır (run sırasında da çağrılabilir).
    // Her çağrıda oyunun rastgelelik akışı önce ve sonra karşılaştırılır: teklif onu tüketirse hata verir.
    public static List<BossRewardSO> Offer(BossRewardPoolSO pool, int bossRound, int seed)
    {
        UnityEngine.Random.State gameplay = UnityEngine.Random.state;
        var events = SegmentEventDirector.Instance; int oldSeed = events.RunSeed;
        var list = F<List<BossRewardSO>>(Boss, "offer");
        var kept = list.ToList(); int keptRound = F<int>(Boss, "offerRound");
        SetP(events, "RunSeed", seed);
        SetF(Boss, "offerRound", bossRound);
        typeof(BossRewardManager).GetMethod("BuildOffer", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(Boss, new object[] { pool, RoundManager.Instance.Calendar.PeriodOf(bossRound) });
        var result = list.ToList();
        list.Clear(); list.AddRange(kept);
        SetF(Boss, "offerRound", keptRound);
        SetP(events, "RunSeed", oldSeed);
        if (!gameplay.Equals(UnityEngine.Random.state)) throw new Exception("building a reward offer consumed the gameplay random stream");
        RandomChecks++;
        return result;
    }

    // Havuzun bütün ödülleri: düz havuzda rewards + kırılma ödülleri, aşamalı havuzda aşama sırasıyla.
    public static List<BossRewardSO> All(BossRewardPoolSO pool) =>
        pool.IsStaged ? pool.stages.SelectMany(s => s.rewards.Select(e => e.reward)).ToList()
                      : pool.rewards.Concat(pool.breakthroughs ?? new List<BossRewardSO>()).ToList();

    // Havuzdaki sıraya göre harf: A, B, C …
    public static string Encode(BossRewardPoolSO pool, List<BossRewardSO> offer)
    {
        var all = All(pool);
        return offer.Count == 0 ? "-" : new string(offer.Select(r => (char)('A' + all.IndexOf(r))).ToArray());
    }
}
