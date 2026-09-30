using UnityEngine;

// Davranış tetik sayaçları (ölçüm ve test): şansı tutan tetikler ve kapasite (aktif sınır) ya da geçersiz hedef
// yüzünden başlatılamayanlar. Elektrik hasarı havuzdan bağımsızdır; yalnız görseli atlanır (HarvestBehaviorManager).
public static class HarvestBehaviorStats
{
    private static readonly int[] triggered = new int[8], skipped = new int[8];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        System.Array.Clear(triggered, 0, triggered.Length);
        System.Array.Clear(skipped, 0, skipped.Length);
    }

    public static void Record(DamageType type, bool started)
    {
        int i = (int)type;
        if (i < 0 || i >= triggered.Length) return;
        triggered[i]++;
        if (!started) skipped[i]++;
    }

    public static int Triggered(DamageType type) => triggered[(int)type];
    public static int Skipped(DamageType type) => skipped[(int)type];
}
