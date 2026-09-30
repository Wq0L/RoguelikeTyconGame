using System;
using System.Collections.Generic;
using UnityEngine;

// Bir run'ın tek yapılandırması: uzunluk, kota segmentleri ve hedefleri, segment olayları (boss), başlangıç ekonomisi.
// Aktif profil Resources/RunProfileSelection'dan okunur (Tools > Run Profili). Profil seçili değilse
// RoundManager ve ResourceManager sahnedeki kendi alanlarını kullanır.
[CreateAssetMenu(menuName = "ClickerGame/Run Profile", fileName = "RunProfile")]
public class RunProfileSO : ScriptableObject
{
    [Tooltip("Arayüzde ve konsolda görünen ad.")]
    public string displayName = "Run";
    [Min(1)] public int runLength = 130;

    [Header("Hasat Kotası")]
    [Min(1)] public int segmentRounds = HarvestQuota.DefaultSegmentRounds;
    [Tooltip("Tabloda olmayan segmentlerin kotası: başlangıç × büyüme^(segment−1).")]
    [Min(5f)] public float quotaStart = HarvestQuota.DefaultStart;
    [Range(1f, 3f)] public float quotaGrowth = HarvestQuota.DefaultGrowth;
    [Tooltip("Segment kotaları tablosu: 0. eleman 1. segment. Boş ya da 0 olan segmentler eğriden gelir.")]
    public List<long> segmentTargets = new();

    [Header("Segment olayları")]
    [Tooltip("Hangi segmentte hangi olay (boss) olacağı. Olay bir önceki segmentin başında duyurulur.")]
    public List<SegmentEventEntry> events = new();

    [Header("Boss sonrası uzmanlaşma")]
    [Tooltip("Bu segmentin kotası geçilince uzmanlaşma seçilir (0: yok). Segment run'ın son segmentiyse ekran açılmaz. Run başına tek seçim.")]
    [Min(0)] public int specializationAfterSegment;
    [Tooltip("Seçim ekranındaki seçenekler (yalnız biri alınır).")]
    public List<SpecializationSO> specializationOptions = new();

    [Header("Başlangıç ekonomisi")]
    [Min(0)] public int startingGold = 80;
    [Min(0)] public int startingIron;
    [Min(0)] public int startingStone;
    [Tooltip("Yüksek bütçeli test profili. Yalnız editörde uygulanır; build'de ResourceManager'ın normal başlangıcı kullanılır.")]
    public bool debugBudget;

    [Header("Bitiş")]
    [Tooltip("Son round'da kota geçilince run sonu ekranının başlığı.")]
    public string victoryTitle = "RUN TAMAMLANDI";

    public long TargetFor(int segment) =>
        segment >= 1 && segment <= segmentTargets.Count && segmentTargets[segment - 1] > 0
            ? segmentTargets[segment - 1]
            : HarvestQuota.Target(segment, quotaStart, quotaGrowth);
}

[Serializable]
public struct SegmentEventEntry
{
    [Min(1)] public int segment;
    public SegmentEventSO segmentEvent;
}
