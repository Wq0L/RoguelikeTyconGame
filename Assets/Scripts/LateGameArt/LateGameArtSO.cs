using System;
using System.Collections.Generic;
using UnityEngine;

// Geç oyun içeriğinin (Bölüm 3.8: tarla olayları ve yeni boss ödülleri) GÖRSEL / SES bağlama kataloğu. Nihai model, animasyon,
// VFX ve sesler bu asset'e prefab / klip olarak bağlanır; oyun kodu değişmez. Boş bırakılan her yuva için yerel, düşük poligonlu
// bir yer tutucu çizilir (PlaceholderShapes); katalog hiç yoksa da oynanış aynen çalışır.
// Asset: Resources/LateGameArt (LateGamePresenter yükler). Bağlama kuralları: Docs/Bolum3-8-IcerikBaglama.md.
[CreateAssetMenu(menuName = "ClickerGame/Late Game Art", fileName = "LateGameArt")]
public sealed class LateGameArtSO : ScriptableObject
{
    public const string ResourcePath = "LateGameArt";

    [Serializable]
    public struct SlotBinding
    {
        public LateGameSlot slot;
        [Tooltip("Bu yuvanın prefab'ı. Kökü hücre merkezinde, zemin yüksekliğinde durur; +Z ileri. Boş: yer tutucu.")]
        public GameObject prefab;
        public LateGameVariant variant;
        [Min(0f)] public float lifetime;
    }

    [Serializable]
    public struct SoundBinding
    {
        public LateGameSound sound;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
        public LateGameVariant variant;
        [Min(0f)] public float repeatSeconds;
        [Range(1, 8)] public int voices;
    }

    [Tooltip("Görsel yuvalar. Listede olmayan ya da prefab'ı boş olan yuva yer tutucuyla çizilir.")]
    public List<SlotBinding> slots = new();
    [Tooltip("Sesler. Listede olmayan ya da klibi boş olan ses çalınmaz.")]
    public List<SoundBinding> sounds = new();

    public SlotBinding Binding(LateGameSlot slot, LateGameVariant variant = LateGameVariant.Default)
    {
        SlotBinding fallback = default;
        foreach (var entry in slots)
        {
            if (entry.slot != slot) continue;
            if (entry.variant == variant) return entry;
            if (entry.variant == LateGameVariant.Default) fallback = entry;
        }
        return fallback;
    }

    public GameObject Prefab(LateGameSlot slot, LateGameVariant variant = LateGameVariant.Default) => Binding(slot, variant).prefab;
    public float Lifetime(LateGameSlot slot, LateGameVariant variant = LateGameVariant.Default) =>
        Binding(slot, variant).lifetime > 0f ? Binding(slot, variant).lifetime : .22f;

    public SoundBinding Sound(LateGameSound sound, LateGameVariant variant = LateGameVariant.Default)
    {
        SoundBinding fallback = default;
        foreach (var entry in sounds)
        {
            if (entry.sound != sound || entry.clip == null) continue;
            if (entry.variant == variant) return entry;
            if (entry.variant == LateGameVariant.Default) fallback = entry;
        }
        return fallback;
    }

    public bool TrySound(LateGameSound sound, out AudioClip clip, out float volume)
    {
        SoundBinding binding = Sound(sound);
        clip = binding.clip;
        volume = Mathf.Clamp01(binding.volume);
        return clip != null;
    }
}

// Görsel yuvalar. Sona eklenir (asset'teki sayı değerleri korunur).
public enum LateGameSlot
{
    // Tarla olayları
    GroundSign = 0,        // bölge hücresinin zemin belirtisi (hazırlık ve aktif)
    ThornRoot = 1,         // Diken Kuşatması kökü (müdahale hedefi)
    ThornPiece = 2,        // kapalı bölgedeki diken parçası (dekor)
    SproutMain = 3,        // İstilacı Filiz ana filizi (müdahale hedefi)
    SproutPiece = 4,       // yayılan filiz parçası (dekor)
    ShellNode = 5,         // Aşılı Kabuk düğümü (müdahale hedefi)
    ShellCover = 6,        // kapanan bölgedeki kabuk parçası (dekor)
    BountyMarker = 7,      // Gezgin Bereket bölge belirtisi / polen
    BreakBurst = 8,        // darbe / kırılma parçaları (kısa ömürlü)
    BloomBurst = 9,        // canlanma (kısa ömürlü)
    // Boss ödülleri
    Antenna = 10,
    AntennaBolt = 11,      // anten vuruşu (kısa ömürlü, iki nokta arası)
    LineBolt = 12,         // iletken hat vuruşu (kısa ömürlü, iki nokta arası)
    CrescentBlade = 13,    // hilal adımı (kısa ömürlü)
    ArcBolt = 14,          // Kritik Ark / Taşan Hasar (kısa ömürlü, iki nokta arası)
    Pulse = 15,            // tek noktalı darbe: yankı, kritik patlama, reaktör, açgözlülük, aşırı yükleme
    // Yerleşim adımı
    PlacementMarker = 16,  // seçilebilir kenar konumu
    PlacementCell = 17,    // seçilen konumun etkileyeceği hücre
    OverheadWarning = 18, // plant canopy warning, independent of ground-attached content
    HitBurst = 19,
    GroundBurst = 20,
    BountyProgress = 21, BountyFlow = 22, BountyCurtain = 23,
    AntennaContact = 24, CircuitReturn = 25, PlacementSelected = 26,
    MoonContact = 27, HarvestEchoPreparation = 28, ReactorIndicator = 29, GreedIndicator = 30, OverloadPreparation = 31,
}

public enum LateGameVariant
{
    Default, Thorn, ShellA, ShellB, FullMoon, HarvestEcho, CritBurst, Reactor, Greed, Overload, CrescentGrow, Overflow, Sprout, Bounty, Antenna, Circuit, Crescent, MoonReturn, CritArc = 18,
}

public enum LateGameSound
{
    AntennaPlaced = 19, CircuitPulse = 20,
    MoonGrowth = 21, Overflow = 22, HarvestEcho = 23, ReactorOn = 24, GreedSpent = 25, OverloadPrepared = 26,
    EventPrepared = 0, EventActivated = 1, TargetHit = 2, TargetBroken = 3, EventResolved = 4, Bloom = 5,
    AntennaPulse = 6, LinePulse = 7, CrescentLaunch = 8, FullMoon = 9, CritArc = 10, CritBurst = 11, Overload = 12, GreedReady = 13, Spread = 14, ShellChoice = 15, ShellClose = 16, ShellOpen = 17, BountyProgress = 18,
}

