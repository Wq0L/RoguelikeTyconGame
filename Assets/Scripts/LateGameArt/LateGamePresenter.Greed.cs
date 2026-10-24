using System.Collections.Generic;
using UnityEngine;

// Açgözlü Hasat "aç ağız halkası" (24 Ekim 2026; eski amber diş işaretinin ve kapanma nabzının yerine). Ağlar Blender'da tasarlandı
// (GreedJawArtSO, Tools/Art/GreedJaw); hareket ve zamanlama burada, Blender önizlemesindeki kare kare hesabın aynısı.
// - Giriş (24 Ekim, "birden belirip yapışıyor" geri bildirimi): ağız merkezden büyümez, halkadan uyanır. Mini dişler dışarıdan
//   girdap gibi girer; dudaklar (diş etleri) ağız köşelerinden başlayıp halka boyunca ortaya doğru uzar ve yükselir; dişler
//   aynı yönde dalga halinde eğik durumdan dikilir; sonunda esneyip bir kez "çat" der. Hap / kayıp giriş bitmeden gelirse
//   o anki hâlden devam eder. Girişte küçük toon duman pofları (24 Ekim, kullanıcı isteği): dudakların başladığı dört köşede
//   "puf", dudaklar ortada buluşunca iki küçük puf; yükselip söner (kayıp tozuyla aynı kart ağı). Uzama yalnız girişte köşe yazar (örnek başına kopya diş eti ağı; tampon paylaşılır, bellek ayrılmaz).
// - Hak hazırken: imleç halkasının içinde iki çene (diş eti + dişler) ve halkanın hemen dışında dönen 5 mini diş. Ağız halkaya
//   yapışıktır ve halkanın gerçek yarıçapıyla ölçeklenir (gecikme saldırı alanını yanlış gösterirdi). Hareket hissi dişlerden gelir:
//   hıza göre geriye yatma (az sönümlü yay, durunca sallanır), hızlıyken biraz daha açılma, beklemede ara ara "çat-çat";
//   mini dişler hızla hızlanır ve biraz geride kalır.
// - GreedSpent (2+ canlı bitki, ×1,5): ağız vuruş merkezinde kalır, açılıp "hap!" diye kapanır; dikenler ve kırıntılar saçılır,
//   mini dişler ağza emilir. GreedLost (tek hedef): yarım ısırık, dört diş kopup düşer (toz pofu), kalanlar sarkar, mini dişler
//   saçılır. Boş vuruşta hiçbir şey değişmez.
// - Kamera ortografik: ağız bakış ekseninde kameraya doğru kaydırılır. Ekrandaki yeri (halkanın üstü) değişmez, saksı ve bitki
//   arkasında kalmaz; parçaların kendi aralarındaki derinlik sırası korunur.
// - Havuzlu: en çok GreedJawCapacity örnek (biri imleçte, diğerleri çıkış animasyonunda); dolunca en eski çıkış kesilir ve sayılır.
//   Parçalar örnek başına bir kez kurulur (ilki ödül alınınca önceden); karede yalnız transform yazılır, bellek ayrılmaz.
//   Ağlar ve malzemeler asset'tir.
// - Saldırı ritmi (24 Ekim, kullanıcı isteği): belirme / hap / kayıp, bir saldırı aralığının %90'ına sığacak kadar hızlanır
//   (yavaş saldırıda tasarım hızı, en çok GreedMaxRate kat). Boş savuruşta "çat-çat", sıradaki saldırı yaklaşırken ağız biraz
//   açılır (hap vuruşla aynı anda gelir); mini dişler 4 saldırıda bir tur döner. Ritim PlayerController'dan salt okunur alınır.
// - Yaş yalnız Round'da ve timeScale > 0 iken ilerler (pause'da durur); round dışında, ödül kalkınca ve kapatılınca temizlenir.
public sealed partial class LateGamePresenter
{
    private const int GreedJawCapacity = 3, GreedMinis = 5, GreedSpikes = 5, GreedPuffs = 4, GreedEntryPuffs = 6;
    private const float GreedDepthBias = 4f, GreedRingLift = .05f, GreedChompSeconds = .55f, GreedLoseSeconds = .78f;
    private const float GreedAppearSeconds = .7f, GreedClackSeconds = .24f, GreedMaxRate = 4f;
    // Giriş, ısırıktan daha az hızlanır (göz takip edebilsin): en çok GreedAppearMaxRate kat, iki saldırı aralığına yayılır.
    private const float GreedAppearMaxRate = 2.5f, GreedTeethGrown = .5f;
    // Kayıpta kopan dişler (üst çene 0–6, alt çene 7–13); her birinin düştüğü yerde bir toz pofu.
    private static readonly int[] GreedFalling = { 1, 4, 8, 11 };
    private static readonly float[] GreedSpikeAngles = { 28f, 62f, 90f, 118f, 152f };
    private static readonly float[] GreedMiniAngles = { 0f, 1.15f, 2.45f, 3.55f, 4.95f };

    private enum GreedPhase { Free, Ready, Chomp, Lose }

    private sealed class GreedTooth
    {
        public Transform part;
        public Vector3 home;
        public Quaternion yaw;
        public float scale;
    }

    // Diş eti süpürmesi: kaynak ağın köşeleri ve köşe açıları (çene uzayında +Z'den, derece) bir kez okunur; iş tamponu paylaşılır.
    private sealed class GumSweep
    {
        public Vector3[] source, work;
        public float[] angle, side;   // side: üçgenin ağırlık merkezine göre (−1 / +1); her üçgenin kendi üç köşesi var
        public float max;
    }

    private sealed class GreedJaw
    {
        public GameObject root;
        public readonly Mesh[] gums = new Mesh[2];
        public readonly GumSweep[] sweeps = new GumSweep[2];
        public readonly float[] sweepShown = { 1f, 1f };
        public float sweepStart = 1f;
        public readonly Transform[] pivots = new Transform[2];   // 0 üst (ekranda uzak), 1 alt (yakın)
        public GreedTooth[] teeth;
        public readonly Transform[] minis = new Transform[GreedMinis];
        public readonly Transform[] spikes = new Transform[GreedSpikes];
        public readonly Transform[] puffs = new Transform[GreedPuffs];
        public readonly Transform[] entryPuffs = new Transform[GreedEntryPuffs];
        public float entryAge = 99f;   // giriş pofları: hap / kayıp giriş bitmeden geldiyse kaldığı yerden sürer
        public Transform[] chips;
        public GreedPhase phase;
        public float age, rate, idle, scale, spin, speed, clack, anticipation, grown;   // grown: hap/kayıp başında girişin bittiği oran   // idle: ölçeksiz oyun zamanı (sakin salınımlar)
        public int attacks;
        public Vector3 center, lastCursor, lean, leanVelocity, lag, lagVelocity;
    }

    private readonly List<GreedJaw> greedJaws = new(), spareGreedJaws = new();
    private readonly Dictionary<Mesh, GumSweep> gumSweeps = new();
    private readonly List<Mesh> greedRuntimeMeshes = new();
    private GreedJaw greedLive;
    private GreedyHarvestEffect greedOwner;
    private PlayerController greedCursor;
    private GreedJawArtSO greedArt;
    private bool greedArtLoaded;

    public int GreedJawsShown => greedJaws.Count;
    public int GreedChomps { get; private set; }
    public int GreedLosses { get; private set; }
    public int GreedJawsCut { get; private set; }

    private GreedJawArtSO GreedArt
    {
        get
        {
            if (!greedArtLoaded)
            {
                greedArt = Resources.Load<GreedJawArtSO>(GreedJawArtSO.ResourcePath);
                greedArtLoaded = true;
                if (greedArt == null || !greedArt.Valid) Debug.LogWarning("GreedJawArt eksik; Açgözlü Hasat ağzı çizilmeyecek (oynanış etkilenmez).");
            }
            return greedArt != null && greedArt.Valid ? greedArt : null;
        }
    }

    private void ClearGreed()
    {
        foreach (GreedJaw jaw in greedJaws) StoreGreedJaw(jaw);
        greedJaws.Clear();
        greedLive = null;
        greedOwner = null;
    }

    private void SyncGreed()
    {
        var source = RunEffects.Instance != null ? RunEffects.Instance.Find<GreedyHarvestEffect>() : null;
        if (source == null || RoundManager.Instance == null || !RoundManager.Instance.IsRoundActive) { ClearGreed(); return; }
        if (greedOwner != source) { ClearGreed(); greedOwner = source; PrewarmGreed(); }
        if (greedCursor == null) greedCursor = FindFirstObjectByType<PlayerController>();
        Camera camera = Camera.main;
        if (camera == null || greedCursor == null) return;
        bool running = Time.timeScale > 0f && GameManager.Instance != null && GameManager.Instance.CurrentState == GameStates.Round;
        float delta = running ? Time.deltaTime : 0f;
        if (source.Ready && greedLive == null) greedLive = StartGreedJaw(GreedPhase.Ready, greedCursor.CursorWorldPosition);
        else if (!source.Ready && greedLive != null) { StoreGreedJaw(greedLive); greedJaws.Remove(greedLive); greedLive = null; }   // hak sessizce gitti
        for (int i = greedJaws.Count - 1; i >= 0; i--)
        {
            GreedJaw jaw = greedJaws[i];
            if (jaw == greedLive) FollowCursor(jaw, delta);
            else jaw.spin += GreedOrbit() * delta;
            jaw.age += delta * jaw.rate;
            jaw.idle += delta;
            if ((jaw.phase == GreedPhase.Chomp && jaw.age >= GreedChompSeconds) || (jaw.phase == GreedPhase.Lose && jaw.age >= GreedLoseSeconds))
            {
                StoreGreedJaw(jaw);
                greedJaws.RemoveAt(i);
                continue;
            }
            PoseGreedJaw(jaw, camera);
        }
    }

    // Ödül alındıktan sonraki ilk aktif karede bir örnek önceden kurulur: ilk hak dolduğunda (oyunun ortasında) kurulum
    // takılması olmasın. Havuzda örnek varsa bir şey yapılmaz.
    private void PrewarmGreed()
    {
        GreedJawArtSO art = GreedArt;
        if (greedJaws.Count + spareGreedJaws.Count > 0 || art == null || !EnsureArcAssets()) return;
        spareGreedJaws.Add(BuildGreedJaw(art));
    }

    // Hak harcandı: ağız vuruş merkezinde "hap!" der.
    private void GreedUsed(RewardCue cue)
    {
        if (ReleaseGreedJaw(GreedPhase.Chomp, cue.From)) GreedChomps++;
        Play(LateGameSound.GreedSpent, LateGameVariant.Greed);
    }

    // Hak kayboldu (tek hedef): dişler düşer. Ses yok (önceki davranış).
    private void GreedLost(RewardCue cue)
    {
        if (ReleaseGreedJaw(GreedPhase.Lose, cue.From)) GreedLosses++;
    }

    private bool ReleaseGreedJaw(GreedPhase phase, Vector3 at)
    {
        Camera camera = Camera.main;
        GreedJaw jaw = greedLive ?? StartGreedJaw(phase, at);
        greedLive = null;
        if (jaw == null || camera == null) return false;
        jaw.grown = jaw.phase == GreedPhase.Ready ? Mathf.Clamp01(jaw.age / GreedTeethGrown) : 1f;
        jaw.sweepStart = jaw.phase == GreedPhase.Ready ? GreedLipSweep(jaw.age) : 1f;
        jaw.entryAge = jaw.phase == GreedPhase.Ready ? jaw.age : 99f;
        jaw.phase = phase;
        jaw.age = 0f;
        jaw.rate = GreedRate(phase == GreedPhase.Chomp ? GreedChompSeconds : GreedLoseSeconds);
        jaw.anticipation = 0f;
        jaw.center = at;
        if (greedCursor != null && greedCursor.CursorRadius > 0f) jaw.scale = greedCursor.CursorRadius / GreedArt.designRadius;
        PoseGreedJaw(jaw, camera);
        return true;
    }

    // Animasyon hızı: verilen uzunluk, sıradaki saldırıdan önce (aralığın %90'ı) bitsin. Yavaş saldırıda 1 (tasarım hızı).
    private float GreedRate(float length, float intervals = .9f, float max = GreedMaxRate)
    {
        float interval = greedCursor != null ? greedCursor.AttackIntervalNow : 0f;
        return interval > 0f ? Mathf.Clamp(length / (interval * intervals), 1f, max) : 1f;
    }

    // Mini diş turu: 4 saldırıda bir (rad/sn); ritim bilinmiyorsa sakin taban hız.
    private float GreedOrbit()
    {
        float interval = greedCursor != null ? greedCursor.AttackIntervalNow : 0f;
        return interval > 0f ? Mathf.Clamp(Mathf.PI / (2f * interval), 1.2f, 6f) : 1.5f;
    }

    private GreedJaw StartGreedJaw(GreedPhase phase, Vector3 at)
    {
        GreedJawArtSO art = GreedArt;
        if (art == null || !EnsureArcAssets()) return null;
        if (greedJaws.Count >= GreedJawCapacity)
        {
            // Çok hızlı saldırıda: imleçteki dışında en eski çıkış animasyonu kesilir.
            int oldest = -1;
            for (int i = 0; i < greedJaws.Count; i++)
                if (greedJaws[i] != greedLive && (oldest < 0 || greedJaws[i].age > greedJaws[oldest].age)) oldest = i;
            if (oldest < 0) return null;
            StoreGreedJaw(greedJaws[oldest]);
            greedJaws.RemoveAt(oldest);
            GreedJawsCut++;
        }
        GreedJaw jaw;
        if (spareGreedJaws.Count > 0) { jaw = spareGreedJaws[^1]; spareGreedJaws.RemoveAt(spareGreedJaws.Count - 1); }
        else jaw = BuildGreedJaw(art);
        jaw.phase = phase;
        jaw.age = jaw.idle = jaw.spin = jaw.speed = jaw.anticipation = 0f;
        jaw.rate = GreedRate(GreedAppearSeconds, 1.8f, GreedAppearMaxRate);
        jaw.grown = jaw.sweepStart = 1f;
        jaw.entryAge = phase == GreedPhase.Ready ? 0f : 99f;
        jaw.clack = GreedClackSeconds;   // sessiz: hakkı doğuran saldırı "çat" saymaz
        jaw.attacks = greedCursor != null ? greedCursor.AttacksPerformed : 0;
        jaw.center = jaw.lastCursor = jaw.lag = at;
        jaw.lean = jaw.leanVelocity = jaw.lagVelocity = Vector3.zero;
        float radius = greedCursor != null ? greedCursor.CursorRadius : art.designRadius;
        jaw.scale = Mathf.Max(0f, radius) / art.designRadius;
        jaw.root.SetActive(true);
        greedJaws.Add(jaw);
        return jaw;
    }

    private void StoreGreedJaw(GreedJaw jaw)
    {
        jaw.phase = GreedPhase.Free;
        if (jaw.root != null) jaw.root.SetActive(false);
        spareGreedJaws.Add(jaw);
    }

    // İmleçteki ağız: konum ve yarıçap her karede halkadan; yaylar (diş eğimi, mini diş gecikmesi) yalnız oyun zamanıyla ilerler.
    private void FollowCursor(GreedJaw jaw, float delta)
    {
        Vector3 cursor = greedCursor.CursorWorldPosition;
        jaw.center = cursor;
        jaw.scale = Mathf.Max(0f, greedCursor.CursorRadius) / GreedArt.designRadius;
        if (delta > 0f)
        {
            Vector3 velocity = (cursor - jaw.lastCursor) / delta;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 625f) velocity = velocity.normalized * 25f;   // ışınlanma / ekran dışı dönüş
            jaw.speed = velocity.magnitude;
            Vector3 target = Vector3.ClampMagnitude(-velocity * .045f, .55f);
            int steps = Mathf.Clamp(Mathf.CeilToInt(delta * 120f), 1, 8);
            float h = delta / steps;
            for (int i = 0; i < steps; i++)
            {
                jaw.leanVelocity += ((target - jaw.lean) * 140f - jaw.leanVelocity * 9.5f) * h;
                jaw.lean += jaw.leanVelocity * h;
                jaw.lagVelocity += ((cursor - jaw.lag) * 260f - jaw.lagVelocity * 22f) * h;
                jaw.lag += jaw.lagVelocity * h;
            }
            jaw.spin += (GreedOrbit() + .22f * Mathf.Min(jaw.speed, 12f)) * delta;
            // Boş savuruş (hak duruyor): her saldırıda kısa "çat-çat"; ritme sığacak hızda.
            if (greedCursor.AttacksPerformed != jaw.attacks) { jaw.attacks = greedCursor.AttacksPerformed; jaw.clack = 0f; }
            jaw.clack = Mathf.Min(GreedClackSeconds, jaw.clack + delta * GreedRate(GreedClackSeconds));
            // Beklenti: sıradaki saldırı yaklaşırken ağız biraz açılır; çok hızlı saldırıda (titreme olmasın) söner.
            float interval = greedCursor.AttackIntervalNow;
            jaw.anticipation = GreedSmooth((greedCursor.AttackProgress - .55f) / .45f) * GreedSmooth((interval - .2f) / .15f);
        }
        jaw.lastCursor = cursor;
    }

    private void PoseGreedJaw(GreedJaw jaw, Camera camera)
    {
        GreedJawArtSO art = GreedArt;
        if (art == null) return;
        Transform view = camera.transform;
        Vector3 forward = view.forward;
        Vector3 far = new Vector3(forward.x, 0f, forward.z);
        far = far.sqrMagnitude > 1e-6f ? far.normalized : Vector3.forward;
        float scale = Mathf.Max(jaw.scale, 1e-4f), t = jaw.age;
        Transform root = jaw.root.transform;
        root.SetPositionAndRotation(jaw.center + Vector3.up * GreedRingLift - forward * GreedDepthBias, Quaternion.identity);
        root.localScale = Vector3.one * scale;

        bool ready = jaw.phase == GreedPhase.Ready, chomp = jaw.phase == GreedPhase.Chomp, lose = jaw.phase == GreedPhase.Lose;
        float gape = ready ? Mathf.Min(1f, jaw.speed / 6f) : 0f;
        float settled = ready ? GreedSmooth((t - .5f) / .2f) : 0f;   // giriş bitti: beklenti ve çat-çat devreye girer
        float chatter = ready ? (GreedBump(jaw.clack, .09f) + GreedBump(jaw.clack - .13f, .09f)) * (1f - gape) * settled : 0f;
        // Hap / kayıp, giriş bitmeden geldiyse: o anki hâlden kısa sürede tam boya yetişir (sıçrama yok).
        float catchUp = ready ? 1f : Mathf.Lerp(jaw.grown, 1f, GreedSmooth(t / .06f));
        Quaternion lean = ready && jaw.lean.sqrMagnitude > 1e-8f
            ? Quaternion.AngleAxis(jaw.lean.magnitude * Mathf.Rad2Deg, Vector3.Cross(Vector3.up, jaw.lean.normalized)) : Quaternion.identity;

        // Çeneler: dış yön boyunca açılıp kapanır, basılır.
        float offset = 0f;
        Vector3 jawScale = Vector3.one;
        if (ready)
        {
            // Esneme (.36–.6) ve şapırtı (.6–.7): uyanan, acıkmış ağız.
            offset = -.05f + .07f * gape + .06f * jaw.anticipation * (1f - gape) * settled - .09f * chatter
                     + .1f * GreedBump(t - .36f, .24f) - .05f * GreedBump(t - .6f, .1f);
            jawScale = new Vector3(1f, GreedBack((t - .02f) / .18f, 1.4f), 1f);   // halkanın üstünde yerinde yükselir
        }
        else if (chomp)
        {
            if (t < .06f) offset = .02f + .12f * GreedSmooth(t / .06f);
            else if (t < .12f) { float u = GreedSmooth((t - .06f) / .06f); offset = .14f - .84f * u; jawScale = new Vector3(1f, 1f - .2f * u, 1f); }
            else if (t < .22f) { float u = GreedSmooth((t - .12f) / .1f); offset = -.7f + .06f * u; jawScale = new Vector3(1f, .8f + .12f * u, 1f); }
            else { float u = GreedSmooth((t - .22f) / .2f); offset = -.64f + .14f * u; jawScale = new Vector3(1f, .92f, 1f) * (1f - u); }
        }
        else if (lose)
        {
            if (t < .12f) offset = -.25f * GreedSmooth(t / .12f);
            else if (t < .3f) offset = -.25f + .07f * GreedSmooth((t - .12f) / .18f);
            else
            {
                float u = GreedSmooth((t - .3f) / .32f), fade = 1f - GreedSmooth((t - .5f) / .12f);
                offset = -.18f + .08f * u;
                jawScale = new Vector3(1f - .06f * u, 1f - .2f * u, 1f - .06f * u) * fade;
            }
        }
        jawScale.y *= catchUp;
        float sweep = ready ? GreedLipSweep(t) : Mathf.Lerp(jaw.sweepStart, 1f, GreedSmooth(t / .06f));
        for (int side = 0; side < 2; side++)
        {
            SweepGum(jaw, side, sweep);
            Transform pivot = jaw.pivots[side];
            Quaternion facing = Quaternion.LookRotation(side == 0 ? far : -far, Vector3.up);
            pivot.localRotation = facing;
            pivot.localPosition = facing * new Vector3(0f, 0f, offset);
            pivot.localScale = jawScale;
            SetActive(pivot, jawScale.x > .002f && jawScale.y > .002f);
        }

        // Dişler (çene uzayında; kökeni tabanı).
        for (int i = 0; i < jaw.teeth.Length; i++)
        {
            GreedTooth tooth = jaw.teeth[i];
            Transform part = tooth.part;
            float size = 1f;
            Vector3 position = tooth.home;
            Quaternion local = tooth.yaw;
            if (ready)
            {
                int order = 3 - Mathf.Abs(i % 7 - 3);   // 0 ağız köşesi … 3 orta
                float grow = (t - .1f - .05f * order) / .13f;   // dudak cephesini izler
                size = GreedBack(grow, 3f);
                float unfold = -60f * (1f - GreedBack(grow, 2f));   // eğik (uç aşağıda) → dik, hafif taşarak
                part.localPosition = position;
                part.rotation = lean * (part.parent.rotation * tooth.yaw * Quaternion.AngleAxis(unfold + .12f * chatter * Mathf.Rad2Deg, Vector3.right));
            }
            else if (lose)
            {
                int falling = System.Array.IndexOf(GreedFalling, i);
                if (falling >= 0 && t > .1f)
                {
                    float u = (t - .1f) / .35f, arc = Mathf.Min(u, 1f);
                    position += tooth.yaw * Vector3.forward * (.4f * u) + Vector3.up * (.5f * Mathf.Sin(Mathf.PI * arc * .8f) - .25f * u * u);
                    local = tooth.yaw * Quaternion.AngleAxis(200f * arc, Vector3.right);
                    size = .9f * (1f - GreedSmooth((t - .45f) / .08f));
                }
                else if (falling < 0)
                {
                    local = tooth.yaw * Quaternion.AngleAxis((-30f + (i % 3) * 6f) * GreedSmooth((t - .1f) / .15f), Vector3.right);
                    size = .95f * (1f - GreedSmooth((t - .5f) / .1f));
                }
                part.localPosition = position;
                part.localRotation = local;
            }
            else { part.localPosition = position; part.localRotation = local; }
            size *= catchUp;
            part.localScale = Vector3.one * (tooth.scale * Mathf.Max(0f, size));
            SetActive(part, size > .002f);
        }

        // Mini dişler: halkanın hemen dışında döner (kök uzayında).
        Vector3 trail = Vector3.ClampMagnitude(jaw.lag - jaw.center, .3f) / scale;
        trail.y = 0f;
        for (int k = 0; k < GreedMinis; k++)
        {
            Transform part = jaw.minis[k];
            float swirl = ready ? GreedEaseOut((t - .03f * k) / .3f) : 1f;   // girişte dışarıdan dönerek gelir
            float angle = GreedMiniAngles[k] + jaw.spin + 2.5f * (1f - swirl);
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Quaternion facing = Quaternion.Euler(0f, 90f - angle * Mathf.Rad2Deg, 0f);
            float height = .06f + .04f * Mathf.Sin(3f * jaw.idle + k * 1.3f), size = 1f;
            Vector3 position = direction * art.miniRadius + Vector3.up * height;
            Quaternion rotation = facing;
            if (ready)
            {
                position = direction * (art.miniRadius + .9f * (1f - swirl)) + trail + Vector3.up * height;
                size = GreedBack((t - .03f * k) / .2f, 2.2f);
                rotation = lean * facing * Quaternion.AngleAxis(.22f * Mathf.Sin(7f * jaw.idle + k * 1.7f) * Mathf.Rad2Deg, Vector3.right);
            }
            else if (chomp)
            {
                float u = GreedSmooth((t - .05f - .015f * k) / .14f);
                position = direction * (art.miniRadius * (1f - u) + .15f * u) + Vector3.up * (height + .15f * u);
                size = 1f - u;
            }
            else if (lose)
            {
                float u = (t - .08f) / .45f;
                if (u > 0f)
                {
                    float arc = Mathf.Min(u, 1f);
                    position = direction * (art.miniRadius + .6f * arc) + Vector3.up * (height + .35f * Mathf.Sin(Mathf.PI * arc * .7f) - .3f * u * u);
                    size = 1f - GreedSmooth((u - .7f) / .3f);
                    rotation = facing * Quaternion.AngleAxis(4f * arc * Mathf.Rad2Deg, Vector3.right);
                }
            }
            size *= catchUp;
            part.localPosition = position;
            part.localRotation = rotation;
            part.localScale = Vector3.one * Mathf.Max(0f, size);
            SetActive(part, size > .002f);
        }

        // Hap: dikenler (ekran düzleminde yelpaze) ve kırıntılar.
        Quaternion card = view.rotation;
        for (int k = 0; k < GreedSpikes; k++)
        {
            Transform part = jaw.spikes[k];
            float x = t - .06f - .012f * (k % 2), size = 0f;
            if (chomp && x >= 0f && x <= .3f) size = x < .07f ? GreedBack(x / .07f, 2.2f) : 1f - GreedSmooth((x - .18f) / .12f);
            bool on = size > .002f;
            SetActive(part, on);
            if (!on) continue;
            float a = GreedSpikeAngles[k] * Mathf.Deg2Rad;
            part.localPosition = Vector3.up * .4f + view.right * (1.25f * Mathf.Cos(a)) + view.up * (.32f + .55f * Mathf.Sin(a));
            part.rotation = card * Quaternion.AngleAxis(GreedSpikeAngles[k] - 90f, Vector3.forward);
            part.localScale = Vector3.one * (1.2f * size * (k == 0 || k == GreedSpikes - 1 ? .79f : 1f));
        }
        for (int k = 0; k < jaw.chips.Length; k++)
        {
            Transform part = jaw.chips[k];
            GreedJawArtSO.ChipSlot slot = art.chipSlots[k];
            float x = (t - .07f) / .45f, size = chomp && x >= 0f && x <= 1f ? 1f - GreedSmooth((x - .7f) / .3f) : 0f;
            bool on = size > .002f;
            SetActive(part, on);
            if (!on) continue;
            var direction = new Vector3(slot.direction.x, 0f, slot.direction.y);
            part.localPosition = direction * (slot.distance * GreedEaseOut(x) * 1.1f) +
                                 Vector3.up * (.4f + slot.rise * Mathf.Sin(Mathf.PI * Mathf.Min(1f, x * 1.2f)) * .9f);
            part.localRotation = Quaternion.Euler(x * 300f, k * 40f, 0f);
            part.localScale = Vector3.one * (slot.scale * size);
        }

        // Giriş dumanı: 0–3 dudak tohumları (ağız köşeleri, yay uçları ±68°), 4–5 dudakların ortada buluştuğu yer.
        float entry = ready ? t : jaw.entryAge + t;
        Vector3 across = Vector3.Cross(Vector3.up, far);
        for (int k = 0; k < GreedEntryPuffs; k++)
        {
            Transform part = jaw.entryPuffs[k];
            bool seed = k < 4;
            float start = seed ? .01f + .025f * (k % 2) : .24f, x = (entry - start) / (seed ? .38f : .3f), size = 0f;
            if (x >= 0f && x <= 1f) size = (x < .25f ? GreedBack(x / .25f, 2f) : 1f - GreedSmooth((x - .45f) / .55f)) * (seed ? 1.3f : .9f);
            bool on = size > .002f;
            SetActive(part, on);
            if (!on) continue;
            Vector3 lip = k < 2 || k == 4 ? far : -far, direction;
            if (seed)
            {
                float edge = 68f * Mathf.Deg2Rad, sign = k % 2 == 0 ? 1f : -1f;
                direction = lip * Mathf.Cos(edge) + across * (sign * Mathf.Sin(edge));
            }
            else direction = lip;
            part.localPosition = direction * (art.designRadius + .12f * x) + Vector3.up * (.08f + .3f * x);
            part.rotation = card * Quaternion.AngleAxis(k * 50f + 40f * x, Vector3.forward);
            part.localScale = Vector3.one * size;
        }

        // Kayıp: kopan dişin düştüğü yerde toz pofu (ağzın içinde değil; yüz gibi okunmasın).
        for (int k = 0; k < GreedPuffs; k++)
        {
            Transform part = jaw.puffs[k];
            float x = (t - .45f) / .3f, size = 0f;
            if (lose && x >= 0f && x <= 1f) size = x < .3f ? GreedBack(x / .3f, 2.2f) : 1f - GreedSmooth((x - .5f) / .5f);
            bool on = size > .002f;
            SetActive(part, on);
            if (!on) continue;
            GreedTooth tooth = jaw.teeth[GreedFalling[k]];
            Vector3 landing = tooth.home + tooth.yaw * Vector3.forward * .4f;
            landing.y = 0f;
            part.position = tooth.part.parent.TransformPoint(landing) + Vector3.up * (.2f * x * scale);
            part.rotation = card;
            part.localScale = Vector3.one * (1.1f * size);
        }
    }

    // Dudak uzaması (0 köşelerde tohum, 1 tam yay): girişin ilk ~.26 sn'si.
    private static float GreedLipSweep(float age) => GreedEaseOut((age - .04f) / .22f);

    // Dudak ağız köşelerinden ortaya doğru uzar: cephe açısının iç tarafındaki köşeler cepheye katlanır (yarıçap / yükseklik
    // korunur, halkanın üstünde kalır). Değer değişmediyse hiçbir şey yazılmaz; 1'de kaynak köşeler geri yazılır.
    private static void SweepGum(GreedJaw jaw, int side, float amount)
    {
        amount = Mathf.Clamp01(amount);
        if (Mathf.Abs(jaw.sweepShown[side] - amount) < 1e-4f) return;
        jaw.sweepShown[side] = amount;
        GumSweep sweep = jaw.sweeps[side];
        float front = sweep.max * (1f - amount);
        for (int i = 0; i < sweep.source.Length; i++)
        {
            Vector3 p = sweep.source[i];
            float angle = sweep.angle[i];
            if (Mathf.Abs(angle) >= front) { sweep.work[i] = p; continue; }
            float radius = Mathf.Sqrt(p.x * p.x + p.z * p.z), edge = sweep.side[i] * front * Mathf.Deg2Rad;
            sweep.work[i] = new Vector3(Mathf.Sin(edge) * radius, p.y, Mathf.Cos(edge) * radius);
        }
        jaw.gums[side].SetVertices(sweep.work);
    }

    private GumSweep SweepOf(Mesh source)
    {
        if (gumSweeps.TryGetValue(source, out GumSweep sweep)) return sweep;
        Vector3[] vertices = source.vertices;   // bir kez
        sweep = new GumSweep { source = vertices, work = new Vector3[vertices.Length], angle = new float[vertices.Length], side = new float[vertices.Length] };
        for (int i = 0; i < vertices.Length; i++)
        {
            sweep.angle[i] = Mathf.Atan2(vertices[i].x, vertices[i].z) * Mathf.Rad2Deg;
            sweep.max = Mathf.Max(sweep.max, Mathf.Abs(sweep.angle[i]));
        }
        // Ağ düz gölgeli dışa aktarıldı (GreedJawArtBuilder): köşeler üçlü sırayla üçgendir. Ortadan geçen üçgen tek yana katlanır,
        // yoksa iki köşe arasında halkanın içini kesen bir şerit olur.
        for (int i = 0; i + 2 < vertices.Length; i += 3)
        {
            Vector3 c = vertices[i] + vertices[i + 1] + vertices[i + 2];
            sweep.side[i] = sweep.side[i + 1] = sweep.side[i + 2] = c.x < 0f ? -1f : 1f;
        }
        gumSweeps[source] = sweep;
        return sweep;
    }

    // Örnek başına kopya diş eti ağları çalışma anında üretildi: presenter yok olurken silinir.
    private void DestroyGreedAssets()
    {
        foreach (Mesh mesh in greedRuntimeMeshes) if (mesh != null) Destroy(mesh);
        greedRuntimeMeshes.Clear();
        gumSweeps.Clear();
    }

    private static float GreedSmooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    private static float GreedEaseOut(float x) { x = Mathf.Clamp01(x); float y = 1f - x; return 1f - y * y * y; }
    private static float GreedBack(float x, float k) { x = Mathf.Clamp01(x) - 1f; return 1f + x * x * ((k + 1f) * x + k); }
    private static float GreedBump(float x, float width) => x > 0f && x < width ? Mathf.Sin(Mathf.PI * x / width) : 0f;

    // ---- kurulum (havuz örneği başına bir kez) ----
    private GreedJaw BuildGreedJaw(GreedJawArtSO art)
    {
        var root = new GameObject("Greed Jaw");
        root.transform.SetParent(transform, false);
        var jaw = new GreedJaw { root = root };
        var lit = new[] { art.lit, art.ink };
        var flat = new[] { arcMaterial };
        jaw.teeth = new GreedTooth[art.upper.Length + art.lower.Length];
        int n = 0;
        for (int side = 0; side < 2; side++)
        {
            Transform pivot = new GameObject(side == 0 ? "Upper Jaw" : "Lower Jaw").transform;
            pivot.SetParent(root.transform, false);
            jaw.pivots[side] = pivot;
            Mesh source = side == 0 ? art.gumUpper : art.gumLower;
            Mesh gum = Instantiate(source);
            gum.name = source.name + " (giriş)";
            greedRuntimeMeshes.Add(gum);
            jaw.gums[side] = gum;
            jaw.sweeps[side] = SweepOf(source);
            GreedPart(pivot, "Gum", gum, lit);
            GreedJawArtSO.ToothSlot[] slots = side == 0 ? art.upper : art.lower;
            Mesh[] meshes = side == 0 ? art.teethUpper : art.teethLower;
            foreach (GreedJawArtSO.ToothSlot slot in slots)
            {
                Transform part = GreedPart(pivot, "Tooth " + n, meshes[Mathf.Clamp(slot.kind, 0, meshes.Length - 1)], lit);
                jaw.teeth[n++] = new GreedTooth { part = part, home = slot.position, yaw = Quaternion.Euler(0f, slot.yaw, 0f), scale = slot.scale };
            }
        }
        for (int k = 0; k < GreedMinis; k++) jaw.minis[k] = GreedPart(root.transform, "Mini " + k, art.mini, lit);
        for (int k = 0; k < GreedSpikes; k++) jaw.spikes[k] = GreedPart(root.transform, "Spike " + k, art.spike, flat);
        jaw.chips = new Transform[art.chipSlots.Length];
        for (int k = 0; k < jaw.chips.Length; k++) jaw.chips[k] = GreedPart(root.transform, "Chip " + k, art.chips[art.chipSlots[k].color % art.chips.Length], lit);
        for (int k = 0; k < GreedPuffs; k++) jaw.puffs[k] = GreedPart(root.transform, "Puff " + k, art.puff, flat);
        for (int k = 0; k < GreedEntryPuffs; k++) jaw.entryPuffs[k] = GreedPart(root.transform, "Entry Puff " + k, art.puff, flat);
        root.SetActive(false);
        return jaw;
    }

    private static Transform GreedPart(Transform parent, string name, Mesh mesh, Material[] materials)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go.transform;
    }
}
