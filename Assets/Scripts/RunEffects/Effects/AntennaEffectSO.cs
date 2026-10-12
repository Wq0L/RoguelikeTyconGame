using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Elektrik Anteni (Bölüm 3.8, erken aşama). Oyuncu ödülü alırken tarlanın kenarında bir konum seçer; anten açık alanın hemen
// DIŞINDA durur ve içeri bakar. Bir tarla hücresini işgal etmez: saksı yerleşimine, tile modifier'ına ve rezonansa dokunmaz.
// - Konum = kenar (batı / doğu / güney / kuzey) + şerit (o kenardaki satır ya da sütun). Tarla büyürse anten aynı şeritte, yeni
//   kenarın dışına taşınmış sayılır (konum her vuruşta o anki açık alandan çözülür).
// - Her 'interval' saniyede bir, baktığı yöndeki ilk 'reach' açık hücredeki canlı bitkilere vurur. Hasar: hasat hasarı stat'ı ×
//   'damage'. Vuruş doğrudan vuruş değildir (DamageType.Reward): kritik atmaz, doğrudan çarpanları ve saksı bonusunu almaz,
//   davranış tetiklemez, doğrudan hasat sayaçlarını doldurmaz, zincire katılmaz.
// - Sayaç yalnız aktif round'da ilerler; round başında sıfırlanır.
// Sayılar ilk prototip değeridir (Tools/Balance/GecOyunV1).
[CreateAssetMenu(menuName = "ClickerGame/Run Effects/Elektrik Anteni", fileName = "AntennaEffect")]
public sealed class AntennaEffectSO : RunEffectSO
{
    [Tooltip("Antenin baktığı yönde vurduğu hücre sayısı.")]
    [Range(1, 6)] public int reach = 3;
    [Tooltip("İki vuruş arası (sn, oyun zamanı).")]
    [Min(0.2f)] public float interval = 2.5f;
    [Tooltip("Vuruş hasarı: hasat hasarı stat'ına oran.")]
    [Min(0f)] public float damage = 0.6f;

    public override bool NeedsPlacement => true;
    public override string PlacementPrompt => "ANTENİ YERLEŞTİR\n<size=70%>Tarlanın kenarında bir konum seç · anten içeri bakar</size>";
    public override string Summary =>
        $"kenardaki anten, baktığı yöndeki ilk {reach} hücreye {Number(interval)} sn'de bir vurur · hasat hasarının %{Percent(damage)}'i";

    public override RunEffect Create() => new AntennaEffect();

    public override bool CanTake(RunEffects host) => host.TryBounds(out _);

    public override void CollectPlacements(RunEffects host, List<int> into)
    {
        into.Clear();
        if (!host.TryBounds(out FieldBounds bounds)) return;
        AntennaSlots.All(bounds, into);
    }

    public override bool IsValidPlacement(RunEffects host, int placement) =>
        host.TryBounds(out FieldBounds bounds) && AntennaSlots.IsValid(bounds, placement);

    public override Vector3 PlacementPosition(RunEffects host, int placement) =>
        host.TryBounds(out FieldBounds bounds) ? AntennaSlots.World(host.Grid, bounds, placement) : Vector3.zero;

    public override void PlacementCells(RunEffects host, int placement, List<GridPosition> into)
    {
        into.Clear();
        if (host.TryBounds(out FieldBounds bounds)) AntennaSlots.Reach(bounds, placement, reach, into);
    }
}

// Anten ailesinin durumu: yerleşmiş antenler ve vuruş sayaçları. İletken Hat ikinci anteni buraya ekler.
public sealed class AntennaEffect : RunEffect
{
    public const int MaxAntennas = 2;

    public struct Antenna
    {
        public int slot;
        public float timer;
        public bool pulses;
    }

    private readonly List<Antenna> antennas = new(MaxAntennas);
    private readonly List<GridPosition> cells = new();
    private AntennaEffectSO Config => (AntennaEffectSO)Data;

    public int Count => antennas.Count;
    public int Slot(int index) => antennas[index].slot;
    public int Pulses { get; private set; }
    // Read-only presentation countdown; the attack clock remains authoritative.
    public float SecondsUntilPulse(int index) => antennas[index].pulses ? Mathf.Max(0f, Config.interval-antennas[index].timer) : float.PositiveInfinity;

    public bool Occupies(int slot)
    {
        for (int i = 0; i < antennas.Count; i++) if (antennas[i].slot == slot) return true;
        return false;
    }

    public override bool OnAdded(int placement) => AddAntenna(placement, true);

    public override void OnRemoved()
    {
        for (int i = antennas.Count - 1; i >= 0; i--) RemoveAntenna(antennas[i].slot);
    }

    // pulses: anten kendi üç hücrelik vuruşunu yapar mı (İletken Hat'ın ikinci anteni veriye göre yalnız hattın ucu olabilir).
    public bool AddAntenna(int slot, bool pulses)
    {
        if (antennas.Count >= MaxAntennas || Occupies(slot)) return false;
        if (!Host.TryBounds(out FieldBounds bounds) || !AntennaSlots.IsValid(bounds, slot)) return false;
        antennas.Add(new Antenna { slot = slot, pulses = pulses });
        Vector3 at = AntennaSlots.World(Host.Grid, bounds, slot);
        Host.Emit(new RewardCue(RewardCueKind.AntennaPlaced, at, AntennaSlots.AnchorWorld(Host.Grid, bounds, slot), slot));
        return true;
    }

    public void RemoveAntenna(int slot)
    {
        for (int i = antennas.Count - 1; i >= 0; i--)
        {
            if (antennas[i].slot != slot) continue;
            antennas.RemoveAt(i);
            Host.Emit(new RewardCue(RewardCueKind.AntennaRemoved, Vector3.zero, slot));
        }
    }

    public override void OnRoundStarted()
    {
        for (int i = 0; i < antennas.Count; i++)
        {
            Antenna antenna = antennas[i];
            antenna.timer = 0f;
            antennas[i] = antenna;
        }
    }

    public override void Tick(float deltaTime)
    {
        float interval = Config.interval;
        for (int i = 0; i < antennas.Count; i++)
        {
            Antenna antenna = antennas[i];
            if (!antenna.pulses) continue;
            antenna.timer += deltaTime;
            bool due = antenna.timer >= interval;
            // Karede en çok bir vuruş; taşan süre en çok bir aralık kadar aktarılır (uzun karede telafi döngüsü yok).
            if (due) antenna.timer = Mathf.Min(antenna.timer - interval, interval);
            antennas[i] = antenna;
            if (due) Pulse(antenna.slot);
        }
    }

    private void Pulse(int slot)
    {
        // Şerit artık açık alanda değilse (olmaması gerekir: tarla yalnız büyür) anten pasif kalır.
        if (!Host.TryBounds(out FieldBounds bounds) || !AntennaSlots.IsValid(bounds, slot)) return;
        AntennaSlots.Reach(bounds, slot, Config.reach, cells);
        if (cells.Count == 0) return;
        int damage = Host.StatDamage(Config.damage);
        for (int i = 0; i < cells.Count; i++)
        {
            StrikeResult result=Host.Strike(cells[i], damage, RewardSource.Antenna);
            if(result.Hit)Host.Emit(new RewardCue(RewardCueKind.AntennaContact,result.Point));
        }
        Pulses++;
        GridSystem grid = Host.Grid;
        Host.Emit(new RewardCue(RewardCueKind.AntennaPulse, AntennaSlots.World(grid, bounds, slot), FieldCells.World(grid, cells[cells.Count - 1]), cells.Count));
    }

    public override void Describe(StringBuilder text)
    {
        for (int i = 0; i < antennas.Count; i++)
            text.Append("anten ").Append(AntennaSlots.Name(antennas[i].slot)).Append(antennas[i].pulses ? $" · sıradaki vuruş {Mathf.Max(0f, Config.interval - antennas[i].timer):0.0} sn" : " · yalnız hat ucu").Append('\n');
        text.Append("vuruş ").Append(Pulses).Append(" · isabet ").Append(Host.Hits(RewardSource.Antenna)).Append(" · hasat ").Append(Host.Kills(RewardSource.Antenna)).Append('\n');
    }
}

// Anten konumlarının geometrisi. Konum kimliği = kenar × 100 + şerit. Kenar: 0 batı (+x'e bakar), 1 doğu (−x), 2 güney (+z),
// 3 kuzey (−z). Şerit: batı / doğuda satır (z), güney / kuzeyde sütun (x), grid koordinatıyla.
public static class AntennaSlots
{
    public const int West = 0, East = 1, South = 2, North = 3;
    // Antenin açık alanın kenar hücresinden dışarı uzaklığı (hücre birimi).
    public const float Outset = 0.85f;

    public static int Id(int side, int lane) => side * 100 + lane;
    public static int Side(int id) => id / 100;
    public static int Lane(int id) => id % 100;

    public static bool IsValid(FieldBounds bounds, int id)
    {
        if (id < 0) return false;
        int side = Side(id), lane = Lane(id);
        if (side > North) return false;
        return side <= East ? lane >= bounds.MinZ && lane <= bounds.MaxZ : lane >= bounds.MinX && lane <= bounds.MaxX;
    }

    public static void All(FieldBounds bounds, List<int> into)
    {
        for (int z = bounds.MinZ; z <= bounds.MaxZ; z++) { into.Add(Id(West, z)); into.Add(Id(East, z)); }
        for (int x = bounds.MinX; x <= bounds.MaxX; x++) { into.Add(Id(South, x)); into.Add(Id(North, x)); }
    }

    // İçe bakan yön (grid adımı).
    public static Vector2Int Facing(int id) => Side(id) switch
    {
        West => new Vector2Int(1, 0),
        East => new Vector2Int(-1, 0),
        South => new Vector2Int(0, 1),
        _ => new Vector2Int(0, -1),
    };

    // Antenin bağlandığı kenar hücresi (açık alanın o şeritteki ilk hücresi).
    public static GridPosition Anchor(FieldBounds bounds, int id)
    {
        int lane = Lane(id);
        return Side(id) switch
        {
            West => new GridPosition(bounds.MinX, lane),
            East => new GridPosition(bounds.MaxX, lane),
            South => new GridPosition(lane, bounds.MinZ),
            _ => new GridPosition(lane, bounds.MaxZ),
        };
    }

    // Baktığı yöndeki ilk 'reach' hücre (açık alanın içinde kalanlar).
    public static void Reach(FieldBounds bounds, int id, int reach, List<GridPosition> into)
    {
        into.Clear();
        if (!IsValid(bounds, id)) return;
        GridPosition anchor = Anchor(bounds, id);
        Vector2Int facing = Facing(id);
        for (int step = 0; step < reach; step++)
        {
            var cell = new GridPosition(anchor.x + facing.x * step, anchor.z + facing.y * step);
            if (!bounds.Contains(cell)) break;
            into.Add(cell);
        }
    }

    public static Vector3 AnchorWorld(GridSystem grid, FieldBounds bounds, int id) => FieldCells.World(grid, Anchor(bounds, id));

    // Antenin dünyadaki yeri: kenar hücresinin dışında, içe bakan yönün tersinde.
    public static Vector3 World(GridSystem grid, FieldBounds bounds, int id)
    {
        Vector2Int facing = Facing(id);
        float distance = FieldCells.CellSize * Outset;
        return AnchorWorld(grid, bounds, id) - new Vector3(facing.x, 0f, facing.y) * distance;
    }

    // İki kenar hücresi arasındaki hat: tam sayı adımlı doğru (Bresenham), iki uç dahil. Hücre sayısı = max(|dx|, |dz|) + 1.
    public static void Line(GridPosition from, GridPosition to, List<GridPosition> into)
    {
        into.Clear();
        int dx = Mathf.Abs(to.x - from.x), dz = Mathf.Abs(to.z - from.z);
        int sx = from.x < to.x ? 1 : -1, sz = from.z < to.z ? 1 : -1;
        int error = dx - dz, x = from.x, z = from.z;
        // Adım sayısı sonludur: en çok dx + dz + 1.
        for (int guard = dx + dz + 1; guard > 0; guard--)
        {
            into.Add(new GridPosition(x, z));
            if (x == to.x && z == to.z) break;
            int twice = error * 2;
            if (twice > -dz) { error -= dz; x += sx; }
            if (twice < dx) { error += dx; z += sz; }
        }
    }

    private static readonly string[] SideNames = { "batı", "doğu", "güney", "kuzey" };
    public static string Name(int id) => IsNameable(id) ? $"{SideNames[Side(id)]} kenarı · şerit {Lane(id)}" : "?";
    private static bool IsNameable(int id) => id >= 0 && Side(id) <= North;
}
