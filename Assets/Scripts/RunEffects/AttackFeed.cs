using System;
using System.Collections.Generic;
using UnityEngine;

// Doğrudan saldırının ve normal davranış tetiğinin bildirimi (Bölüm 3.8). PlayerController ve PlanterBrain yalnız buraya bildirir;
// kimin dinlediğini bilmezler. Dinleyenler: boss ödülü etkileri (RunEffects) ve tarla olayları (FieldEventDirector).
// - Dinleyici yokken hiçbir şey kaydedilmez ve saldırının çarpanı 1 kalır: eski profillerde saldırı yolu aynen eskisi gibidir.
// - Dinleyiciler saldırının SONUNDA çalışır (End): bütün doğrudan vuruşlar uygulanmış, zincir kökü ve Hasat Ritmi kapanmıştır.
//   Böylece bir ödül etkisinin vuruşu aynı saldırının sıradaki doğrudan vuruşunu değiştiremez.
// - Bunlar oynanış dinleyicileridir (gözlem değil): hataları yutulmaz.
public static class AttackFeed
{
    private static DirectAttackListener[] listeners = Array.Empty<DirectAttackListener>();
    private static readonly DirectAttackReport report = new();

    public static bool Listening => listeners.Length > 0;
    // Bir doğrudan saldırının vuruşları uygulanıyor (Begin ile End arası). Normal davranış tetikleri bu aralıkta doğar.
    public static bool InAttack { get; private set; }
    // Son başlayan saldırının sıra numarası (dinleyici varken artar): "aynı kök saldırı" kimliği, zincir ödülünden bağımsız.
    public static int Serial => report.Serial;

    // Şansı tutan ve gerçekten başlayan NORMAL davranış tetiği (nesil 0): saksı, davranış, kaynak hücre, bitkinin konumu.
    // Zincirden, artçıdan ya da ödül etkisinden doğan davranışlar bildirilmez.
    public static event Action<PlanterBrain, DamageType, GridObject, Vector3> BehaviorTriggered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        listeners = Array.Empty<DirectAttackListener>();
        BehaviorTriggered = null;
        InAttack = false;
        report.Reset();
        DirectAttackPause.Clear();
        DirectAttackPause.ResetCounters();
    }

    public static void Add(DirectAttackListener listener)
    {
        if (listener == null || Array.IndexOf(listeners, listener) >= 0) return;
        var next = new DirectAttackListener[listeners.Length + 1];
        Array.Copy(listeners, next, listeners.Length);
        next[listeners.Length] = listener;
        listeners = next;
    }

    public static void Remove(DirectAttackListener listener)
    {
        int index = Array.IndexOf(listeners, listener);
        if (index < 0) return;
        var next = new DirectAttackListener[listeners.Length - 1];
        Array.Copy(listeners, 0, next, 0, index);
        Array.Copy(listeners, index + 1, next, index, listeners.Length - index - 1);
        listeners = next;
    }

    // Hedef hücreler toplandıktan sonra, hasardan önce. Dönen: yalnız bu saldırının doğrudan hasar çarpanı (dinleyici yoksa 1).
    public static float Begin(Vector3 center, float radius, List<GridObject> cells)
    {
        if (listeners.Length == 0) return 1f;
        report.Start(center, radius, cells);
        InAttack = true;
        float multiplier = 1f;
        DirectAttackListener[] current = listeners;
        for (int i = 0; i < current.Length; i++) current[i].Begin(report, ref multiplier);
        report.Multiplier = multiplier;
        return multiplier;
    }

    // Her doğrudan vuruştan sonra (hasar uygulanmış): damage, kritik dahil hesaplanmış vuruş hasarıdır (saksı bonusundan önce).
    public static void Hit(GridObject cell, PlantHealth plant, int damage, bool crit, bool damaged = true, Vector3? point = null)
    {
        if (InAttack) report.Record(cell, plant, damage, crit, damaged, point);
    }

    public static void End(int directHarvests, bool touchedLivingPlant)
    {
        if (!InAttack) return;
        InAttack = false;
        report.DirectHarvests = directHarvests;
        report.TouchedLivingPlant = touchedLivingPlant;
        DirectAttackListener[] current = listeners;
        for (int i = 0; i < current.Length; i++) current[i].End(report);
    }

    public static void NotifyBehavior(PlanterBrain planter, DamageType type, GridObject cell, Vector3 position) =>
        BehaviorTriggered?.Invoke(planter, type, cell, position);
}

public abstract class DirectAttackListener
{
    // multiplier: yalnız bu saldırının doğrudan hasarına, diğer katsayılarla aynı çarpımda bir kez.
    public virtual void Begin(DirectAttackReport attack, ref float multiplier) { }
    public virtual void End(DirectAttackReport attack) { }
}

// Bir doğrudan vuruşun sonucu. Bitki referansı yalnız aynı karede geçerlidir (ölen bitki LateUpdate'te havuza döner); Life o
// yaşamın kimliğidir.
public readonly struct DirectHit
{
    public readonly Vector3 Point; // Snapshot before damage; never follow a pooled plant.
    public readonly GridObject Cell;
    public readonly PlantHealth Plant;
    public readonly uint Life;
    public readonly PlanterBrain Owner;
    public readonly PlantRarity Rarity;
    public readonly int Damage, Overkill;
    public readonly bool Crit, Killed, Damaged;

    public DirectHit(GridObject cell, PlantHealth plant, int damage, bool crit, bool damaged = true, Vector3? point = null)
    {
        Point = point ?? plant.transform.position;
        Cell = cell;
        Plant = plant;
        Life = plant.LifetimeVersion;
        Owner = plant.Owner;
        Rarity = plant.Data != null ? plant.Data.rarity : PlantRarity.Common;
        Damage = damage;
        Damaged = damaged;
        Crit = crit;
        Killed = plant.IsDead;
        Overkill = plant.IsDead ? plant.Overkill : 0;
    }
}

// Tek bir oyuncu saldırısının kaydı: saldırı başına yeniden kullanılır (ayırma yok). Dinleyiciler saklamaz, End içinde okur.
public sealed class DirectAttackReport
{
    private readonly List<DirectHit> hits = new(32);
    private int living = -1;

    public int Serial { get; private set; }
    public Vector3 Center { get; private set; }
    // Bu saldırının gerçek temas yarıçapı (Hasat Ritmi güçlendirmesi dahil).
    public float Radius { get; private set; }
    public IReadOnlyList<GridObject> Cells { get; private set; }
    public IReadOnlyList<DirectHit> Hits => hits;
    public float Multiplier { get; internal set; } = 1f;
    public int DirectHarvests { get; internal set; }
    public bool TouchedLivingPlant { get; internal set; }

    public int Crits
    {
        get { int count = 0; for (int i = 0; i < hits.Count; i++) if (hits[i].Crit) count++; return count; }
    }

    // Saldırı başlarken alandaki canlı bitki sayısı (ilk soruda sayılır; yalnız Begin sırasında anlamlıdır).
    public int LivingTargets
    {
        get
        {
            if (living >= 0) return living;
            living = 0;
            for (int i = 0; i < Cells.Count; i++)
            {
                GameObject plant = Cells[i].GetPlantObject();
                if (plant != null && plant.TryGetComponent(out PlantHealth health) && !health.IsDead) living++;
            }
            return living;
        }
    }

    public bool Covers(GridObject cell)
    {
        for (int i = 0; i < Cells.Count; i++) if (Cells[i] == cell) return true;
        return false;
    }

    internal void Start(Vector3 center, float radius, List<GridObject> cells)
    {
        Serial++;
        Center = center;
        Radius = radius;
        Cells = cells;
        hits.Clear();
        living = -1;
        Multiplier = 1f;
        DirectHarvests = 0;
        TouchedLivingPlant = false;
    }

    internal void Record(GridObject cell, PlantHealth plant, int damage, bool crit, bool damaged, Vector3? point = null) => hits.Add(new DirectHit(cell, plant, damage, crit, damaged, point));

    internal void Reset()
    {
        Serial = 0;
        hits.Clear();
        Cells = null;
        living = -1;
    }
}

// Doğrudan saldırı duraklaması (Bölüm 3.8, Dolunay'ın bedeli). Yalnız oyuncunun saldırı zamanlayıcısını bekletir: input, imleç,
// davranışlar, üretim ve round süresi etkilenmez; görselin ya da parçacığın bitmesi beklenmez.
// - Süre oyun zamanıyla azalır (duraklatılmış oyunda azalmaz; saldırı zamanlayıcısı da o sırada durur: kilitlenme olmaz).
// - Her kaynağın kendi süresi vardır: bir kaynak yalnız kendi kilidini kurar, uzatır ya da kaldırır; başkasınınkine dokunamaz.
// - Sert üst sınır: bir kaynağın kalan süresi MaxSeconds'ı aşamaz (istek kırpılır).
// - Round sonu, yeni run ve menüde temizlenir (RunEffects).
public static class DirectAttackPause
{
    public const float MaxSeconds = 0.6f;
    private const int Capacity = 4;
    private static readonly int[] sources = new int[Capacity];
    private static readonly float[] remaining = new float[Capacity];
    private static int count;

    public static bool Active => count > 0;
    public static int Sources => count;
    // Ölçüm ve test: verilen toplam duraklama isteği ve duraklamada geçen oyun süresi.
    public static int Requests { get; private set; }
    public static float PausedSeconds { get; private set; }

    public static float Remaining
    {
        get { float max = 0f; for (int i = 0; i < count; i++) if (remaining[i] > max) max = remaining[i]; return max; }
    }

    public static float RemainingFor(int source)
    {
        for (int i = 0; i < count; i++) if (sources[i] == source) return remaining[i];
        return 0f;
    }

    public static void Request(int source, float seconds)
    {
        if (source == 0 || !(seconds > 0f)) return;   // NaN da reddedilir
        seconds = Mathf.Min(seconds, MaxSeconds);
        Requests++;
        for (int i = 0; i < count; i++)
            if (sources[i] == source) { if (seconds > remaining[i]) remaining[i] = seconds; return; }
        if (count >= Capacity) return;   // kapasite dolu: yeni kilit kurulmaz (oyun kilitlenmez)
        sources[count] = source;
        remaining[count] = seconds;
        count++;
    }

    public static void Release(int source)
    {
        for (int i = 0; i < count; i++)
        {
            if (sources[i] != source) continue;
            count--;
            sources[i] = sources[count];
            remaining[i] = remaining[count];
            return;
        }
    }

    // PlayerController her karede çağırır. true: bu karede saldırı zamanlayıcısı ilerlemez.
    public static bool Tick(float deltaTime)
    {
        if (count == 0) return false;
        PausedSeconds += deltaTime;
        for (int i = count - 1; i >= 0; i--)
        {
            remaining[i] -= deltaTime;
            if (remaining[i] > 0f) continue;
            count--;
            sources[i] = sources[count];
            remaining[i] = remaining[count];
        }
        return count > 0;
    }

    // Bütün kilitleri kaldırır (round sonu, menü, yeni run). Sayaçlar yalnız yeni run'da sıfırlanır.
    public static void Clear() => count = 0;

    public static void ResetCounters()
    {
        Requests = 0;
        PausedSeconds = 0f;
    }
}
