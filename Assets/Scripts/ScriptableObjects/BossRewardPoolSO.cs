using System;
using System.Collections.Generic;
using UnityEngine;

// Boss ödül havuzu: profil hangi ödüllerin sunulacağını ve bir ekranda kaç seçenek olacağını buradan alır.
// İki kullanım:
// - Düz havuz (stages boş; eski profiller): 'rewards' + 'breakthroughs'. Sunulma zamanı ödülün kendi minRound / maxRound alanıdır.
// - Aşamalı havuz (stages dolu; Bölüm 3.7.3): ödüller aşamalara dağıtılır. Sunulma zamanı YALNIZ aşamanın ilk round'udur;
//   ödülün minRound / maxRound alanları okunmaz. Etki tanımı ödül asset'inde kalır; aşama ve teklif politikası buradadır.
// Aşama sınıfı (erken / orta / güçlü) tile ve bitki nadirliğinden ayrı bir kavramdır.
[CreateAssetMenu(menuName = "ClickerGame/Boss Reward Pool", fileName = "BossRewardPool")]
public sealed class BossRewardPoolSO : ScriptableObject
{
    public List<BossRewardSO> rewards = new();
    [Tooltip("Kırılma ödülleri (Bölüm 3.6). Sunulabilir ve henüz alınmamış olan varsa teklifin bir slotu bunların arasından rastgele " +
             "seçilir; kalan slotlar 'rewards' listesinden gelir. Uygun kırılma ödülü kalmayınca teklif tamamen 'rewards' listesinden olur.")]
    public List<BossRewardSO> breakthroughs = new();
    [Tooltip("Bir boss sonrası gösterilen farklı seçenek sayısı. Uygun ödül daha azsa o kadar gösterilir.")]
    [Range(1, 4)] public int choices = 3;

    [Header("Aşamalı teklif (Bölüm 3.7.3) — boş bırakılırsa havuz eskisi gibi çalışır")]
    [Tooltip("Aşamalar, ilk round'a göre küçükten büyüğe (ilk aşama Round 1'de başlar). Boss round'u bir aşamanın içindeyse o aşama ve " +
             "daha önce açılmış aşamaların ödülleri aday olur. Bir ödül yalnız bir aşamada yazılır.")]
    public List<BossRewardStage> stages = new();
    [Tooltip("Kalan slotlarda aşama uzaklığına göre ağırlık çarpanı: 0. eleman mevcut aşama, 1. bir önceki, 2. iki önceki … " +
             "Ödülün kendi ağırlığı bu çarpanla çarpılır.")]
    public List<float> stageDistanceWeights = new() { 1f, 0.6f, 0.35f };
    [Tooltip("Mevcut aşamada uygun aday varsa teklifin bir slotu o aşamadan seçilir (ödülün kendi ağırlığıyla); ekrandaki yeri rastgeledir.")]
    public bool reserveCurrentStageSlot = true;
    [Tooltip("Sınırsız mod için ayrı aşama (P10 / P11). Şimdilik yalnız veri alanı: oyun bu aşamayı hiçbir round'da açmaz, listesi boş olabilir.")]
    public BossRewardStage endlessStage = new();

    public bool IsStaged => stages != null && stages.Count > 0;

    // Round'un aşaması: ilk round'u o round'dan büyük olmayan son aşama. Aşamasız havuzda ya da ilk aşamadan önce −1.
    public int StageIndexAt(int round)
    {
        int index = -1;
        if (!IsStaged) return index;
        for (int i = 0; i < stages.Count; i++)
            if (stages[i].firstRound <= round) index = i;
        return index;
    }

    public BossRewardStage StageAt(int round)
    {
        int index = StageIndexAt(round);
        return index >= 0 ? stages[index] : null;
    }

    // Ödülün yazıldığı ana run aşaması (yoksa −1).
    public int StageIndexOf(BossRewardSO reward)
    {
        if (!IsStaged || reward == null) return -1;
        for (int i = 0; i < stages.Count; i++)
            foreach (BossRewardStageEntry entry in stages[i].rewards)
                if (entry.reward == reward) return i;
        return -1;
    }

    // Aşamanın son round'u: sıradaki aşamanın ilk round'undan bir önceki; son aşamada run'ın son round'u.
    public int StageLastRound(int index, int runLength) =>
        IsStaged && index >= 0 && index + 1 < stages.Count ? stages[index + 1].firstRound - 1 : runLength;

    // Kartta gösterilen açıklama: aşamalı havuz kendi metnini yazdıysa o (eski profilin sunulma zamanını anlatan cümle taşınmaz),
    // yoksa ödül asset'indeki metin.
    public string NoteFor(BossRewardSO reward)
    {
        if (reward == null) return "";
        if (IsStaged)
            foreach (BossRewardStage stage in stages)
                foreach (BossRewardStageEntry entry in stage.rewards)
                    if (entry.reward == reward && !string.IsNullOrEmpty(entry.note)) return entry.note;
        return reward.note;
    }

    // Havuzda, verilen ödülle aynı dışlama grubunda olan diğer ödüller (aşamalı havuzda aşamalardan, düz havuzda iki listeden).
    public void CollectExclusive(BossRewardSO reward, List<BossRewardSO> into)
    {
        if (reward == null || string.IsNullOrEmpty(reward.exclusiveGroup)) return;
        void Consider(BossRewardSO other)
        {
            if (other != null && other != reward && other.exclusiveGroup == reward.exclusiveGroup && !into.Contains(other)) into.Add(other);
        }
        if (IsStaged)
            foreach (BossRewardStage stage in stages)
                foreach (BossRewardStageEntry entry in stage.rewards) Consider(entry.reward);
        foreach (BossRewardSO other in rewards) Consider(other);
        if (breakthroughs != null)
            foreach (BossRewardSO other in breakthroughs) Consider(other);
    }

    public float DistanceWeight(int distance) =>
        distance >= 0 && distance < stageDistanceWeights.Count ? Mathf.Max(0f, stageDistanceWeights[distance]) : 0f;

    // Aşamalı havuzun veri denetimi. null: geçerli (ya da aşamasız: eski havuz). Aksi halde hatayı anlatan metin; veri düzeltilmez.
    public static string Validate(BossRewardPoolSO pool, int runLength)
    {
        if (pool == null || !pool.IsStaged) return null;
        if (pool.rewards.Count > 0 || (pool.breakthroughs != null && pool.breakthroughs.Count > 0))
            return "aşamalı havuzda eski 'rewards' / 'breakthroughs' listeleri boş olmalı (ödüller yalnız aşamalarda yazılır)";
        if (pool.stages[0].firstRound != 1) return $"ilk aşama Round 1'de başlamalı (şu an ilk round'u {pool.stages[0].firstRound}): öncesindeki boss'ların aşaması olmaz";
        var seen = new Dictionary<BossRewardSO, string>();
        var seenIds = new Dictionary<string, string>();
        var all = new List<BossRewardStage>(pool.stages);
        if (pool.endlessStage != null) all.Add(pool.endlessStage);
        for (int i = 0; i < all.Count; i++)
        {
            BossRewardStage stage = all[i];
            bool endless = i >= pool.stages.Count;
            string name = string.IsNullOrEmpty(stage.displayName) ? (endless ? "sınırsız aşama" : $"{i + 1}. aşama") : stage.displayName;
            if (!endless)
            {
                if (string.IsNullOrEmpty(stage.displayName)) return $"{i + 1}. aşamanın adı boş";
                if (i > 0 && stage.firstRound <= pool.stages[i - 1].firstRound)
                    return $"aşamalar sıralı değil: {name} aşamasının ilk round'u {stage.firstRound}, bir önceki aşamanınki {pool.stages[i - 1].firstRound}";
                if (stage.firstRound > runLength) return $"{name} aşamasının ilk round'u {stage.firstRound}; run {runLength} round (aşama hiç açılmaz)";
            }
            foreach (BossRewardStageEntry entry in stage.rewards)
            {
                if (entry.reward == null) return $"{name} aşamasında boş ödül satırı var";
                if (seen.TryGetValue(entry.reward, out string other))
                    return $"'{entry.reward.displayName}' ödülü iki kez yazılmış ({other} ve {name}): bir ödül yalnız bir aşamada olabilir";
                seen[entry.reward] = name;
                string id = entry.reward.id;
                if (!string.IsNullOrEmpty(id))
                {
                    if (seenIds.TryGetValue(id, out string otherId))
                        return $"'{id}' kimlikli ödül iki ayrı asset olarak yazılmış ({otherId} ve {name}): aynı etki ayrı stack olarak çoğaltılamaz";
                    seenIds[id] = name;
                }
            }
        }
        if (pool.stageDistanceWeights.Count < pool.stages.Count)
            return $"aşama uzaklığı ağırlıklarında {pool.stageDistanceWeights.Count} değer var, {pool.stages.Count} aşama için {pool.stages.Count} değer gerekir";
        for (int i = 0; i < pool.stageDistanceWeights.Count; i++)
            if (pool.stageDistanceWeights[i] < 0f) return $"aşama uzaklığı ağırlığı negatif ({i}. değer)";
        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        string error = Validate(this, int.MaxValue);
        if (error != null) Debug.LogError($"Boss ödül havuzu '{name}': ödül aşamaları geçersiz — {error}. Bu hâliyle run başlamaz.", this);
    }
#endif
}

// Bir ödül aşaması: adı, ilk round'u ve o aşamaya ait ödüller. Aşama açıldıktan sonra ödülleri run sonuna kadar aday kalır.
[Serializable]
public sealed class BossRewardStage
{
    [Tooltip("Sabit kimlik (log ve test için).")]
    public string id;
    [Tooltip("Arayüzdeki ad: \"ERKEN\" → ödül ekranında \"ERKEN AŞAMA · BOSS ÖDÜLÜ\", kartta \"ERKEN AŞAMA\".")]
    public string displayName;
    [Tooltip("Aşamanın ilk round'u. Son round'u, sıradaki aşamanın ilk round'undan bir öncekidir.")]
    [Min(1)] public int firstRound = 1;
    [Tooltip("Aşama etiketinin rengi (sunum). Nadirlik renkleriyle aynı anlamı taşımaz.")]
    public Color color = Color.white;
    public List<BossRewardStageEntry> rewards = new();
}

[Serializable]
public struct BossRewardStageEntry
{
    public BossRewardSO reward;
    [Tooltip("Bu havuzda kartta gösterilecek açıklama (boş: ödül asset'indeki metin). Ödül asset'inin metni eski profilin sunulma " +
             "zamanını anlatıyorsa burada o cümle olmadan yazılır; sunulma zamanı aşamadan üretilir.")]
    [TextArea] public string note;
}
