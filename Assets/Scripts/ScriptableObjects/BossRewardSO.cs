using System.Collections.Generic;
using UnityEngine;

// Boss ödülü (run buff'ı): bir boss geçilince sunulan seçeneklerden biri. Etki run sonuna kadar geçerlidir, yeni run'da sıfırlanır;
// kalıcı kayda (MetaSave) yazılmaz. Aynı ödül maxStacks kez alınabilir; her alış aynı etkiyi bir kez daha ekler.
// Etki yolları (BossRewardManager uygular):
//   modifiersPerStack      → StatManager global modifier (mevcut ve sonradan alınan saksılar dahil)
//   directDamageMultiplier → yalnız oyuncunun doğrudan vuruşu (PlayerController); davranış hasarına geçmez
//   rareDirectMultiplier   → yalnız doğrudan vuruş ve yalnız rareDirectFrom ve üstü nadirlikteki bitkiye (Bölüm 3.5)
//   behaviorDamageMultiplier → yalnız davranış hasarı: patlama, kasırga, bumerang, elektrik (Bölüm 3.5); doğrudan vuruşa geçmez
//   echo                   → kırılma (Bölüm 3.6): şansı tutan normal patlama / elektrik tetiğinden sonra gecikmeli ikinci darbe (BehaviorEchoes)
//   rhythmHarvests         → kırılma (Bölüm 3.6): her N doğrudan hasatta bir sonraki oyuncu saldırısı güçlenir (RunPower.Rhythm)
[CreateAssetMenu(menuName = "ClickerGame/Boss Reward", fileName = "BossReward")]
public sealed class BossRewardSO : ScriptableObject
{
    [Tooltip("Sabit kimlik (log ve test için).")]
    public string id;
    public string displayName;
    [Tooltip("Etkinin adı (değer veriden yazılır): \"Saldırı aralığı\" → \"Saldırı aralığı ×0,90\".")]
    public string effectLabel;
    [Tooltip("Aralık küçülten etkilerde karşılığı (boş bırakılabilir): \"saldırı sıklığı\" → \"(saldırı sıklığı ×1,11)\".")]
    public string inverseLabel;
    [Tooltip("Kartta etkinin altındaki kısa açıklama.")]
    [TextArea] public string note;

    [Header("Etki (her alışta bir kez daha eklenir)")]
    public List<StatModifier> modifiersPerStack = new();
    [Min(0.01f)] public float directDamageMultiplier = 1f;
    [Tooltip("Yalnız doğrudan vuruş, yalnız aşağıdaki nadirlik ve üstündeki bitkilere. 1 = yok.")]
    [Min(0.01f)] public float rareDirectMultiplier = 1f;
    public PlantRarity rareDirectFrom = PlantRarity.Rare;
    [Tooltip("Yalnız davranış hasarı (patlama, kasırga, bumerang, elektrik). 1 = yok.")]
    [Min(0.01f)] public float behaviorDamageMultiplier = 1f;

    [Header("Kırılma etkisi (Bölüm 3.6) — boşsa sıradan stat ödülü")]
    [Tooltip("Hangi davranışın şansı tutan normal tetiğinden sonra ikinci bir darbe gelir.")]
    public BossRewardEcho echo;
    [Tooltip("İkinci darbenin gecikmesi (sn).")]
    [Min(0f)] public float echoDelay = 0.2f;
    [Tooltip("İkinci darbenin hasarı: ilk darbenin HESAPLANMIŞ hasarına oran. Katsayılar ikinci kez uygulanmaz.")]
    [Range(0f, 2f)] public float echoDamage = 0.7f;
    [Tooltip("Yalnız patlama: ikinci darbenin yarıçapı, ilk patlamanın yarıçapına oran.")]
    [Min(0f)] public float echoRadius = 1.25f;
    [Tooltip("Hasat Ritmi: kaç doğrudan bitki hasadında bir hak hazırlanır. 0: yok.")]
    [Min(0)] public int rhythmHarvests;
    [Tooltip("Hazır hakla atılan saldırının doğrudan hasar çarpanı.")]
    [Min(0.01f)] public float rhythmDamage = 1.5f;
    [Tooltip("Hazır hakla atılan saldırının vuruş yarıçapı çarpanı (yalnız o saldırı; davranışlara geçmez).")]
    [Min(0.01f)] public float rhythmRadius = 1.5f;

    public bool IsBreakthrough => echo != BossRewardEcho.None || rhythmHarvests > 0;

    [Header("Sunum")]
    [Min(1)] public int maxStacks = 3;
    [Tooltip("Seçim ağırlığı. İlk havuzda hepsi eşit.")]
    [Min(0f)] public float weight = 1f;
    public BossRewardCondition condition;
    [Tooltip("'Saksıda bu stat var' koşulunun stat'ı (ör. ExplosionChance: yerleşmiş, patlama davranışı olan saksı).")]
    public StatType conditionStat;
    [Tooltip("'Tarla boşalıyor' koşulunun eşiği: son round'da üretim noktalarının ortalama doluluğu bunun altındaysa sunulur (0–1).")]
    [Range(0f, 1f)] public float conditionThreshold = 0.7f;
    [Tooltip("Bu round'daki boss'tan itibaren sunulur (5, 10 … 45). 0: baştan itibaren. Geç ödüllerin güç bütçesi daha büyük olabilir.")]
    [Min(0)] public int minRound;
    [Tooltip("Bu round'daki boss'tan sonra sunulmaz. 0: sınır yok.")]
    [Min(0)] public int maxRound;

    public bool OfferedAt(int bossRound) => (minRound <= 0 || bossRound >= minRound) && (maxRound <= 0 || bossRound <= maxRound);
}

public enum BossRewardCondition
{
    Always = 0,
    // En az bir saksının bu ödülün dokunduğu stat'larından biri 0'dan büyükse (Kıvılcım: davranış şansı olan saksı).
    AnyPlanterHasModifiedStat = 1,
    // En az bir saksının herhangi bir davranış şansı (patlama, kasırga, bumerang, elektrik) 0'dan büyükse.
    AnyBehaviorPlanter = 2,
    // Tarla boşalıyorsa: son round'da üretim noktalarının ortalama doluluğu ödülün eşiğinin altındaysa (üretim darboğaz olmuş).
    // Tarla zaten doluyken üretim ödülü hiçbir şey değiştirmez; böyle bir ödül o zaman sunulmaz.
    FieldRunsLow = 3,
    // Yerleşmiş en az bir saksının conditionStat değeri 0'dan büyükse (Artçı Patlama: patlama şansı; Çifte Akım: elektrik şansı).
    AnyPlanterHasConditionStat = 4,
}

public enum BossRewardEcho
{
    None = 0,
    Explosion = 1,
    Electric = 2,
}
