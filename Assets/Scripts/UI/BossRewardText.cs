using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Boss ödülü metinleri: etki değeri asset'ten yazılır. Çarpan "×", yüzde puanı "puan"; aralık küçülmesi ile sıklık artışı ayrı yazılır.
public static class BossRewardText
{
    // Tek alışın etkisi: "Saldırı aralığı ×0,90 (saldırı sıklığı ×1,11)".
    public static string Effect(BossRewardSO reward) => Describe(reward, 1);

    // n alışın toplam etkisi: çarpanlar üst üste çarpılır (×1,15 → ×1,32 → ×1,52), puanlar toplanır (+8 → +16 → +24).
    public static string Total(BossRewardSO reward, int count) => count <= 0 ? "etki yok" : Describe(reward, count);

    private static string Describe(BossRewardSO reward, int count) => reward == null ? "" : $"{reward.effectLabel} {Value(reward, count)}".Trim();

    // Yalnız değer: "×1,32", "×0,81 (saldırı sıklığı ×1,23)", "+16 puan".
    public static string Value(BossRewardSO reward, int count)
    {
        if (reward == null || count <= 0) return "";
        if (reward.IsBreakthrough) return Breakthrough(reward);
        if (IsTrade(reward)) return TradeValue(reward, count);
        int special = (!Mathf.Approximately(reward.directDamageMultiplier, 1f) ? 1 : 0) +
            (!Mathf.Approximately(reward.rareDirectMultiplier, 1f) ? 1 : 0) +
            (!Mathf.Approximately(reward.behaviorDamageMultiplier, 1f) ? 1 : 0);
        bool mixed = false;
        for (int i = 1; i < reward.modifiersPerStack.Count; i++)
        {
            var first = reward.modifiersPerStack[0]; var next = reward.modifiersPerStack[i];
            if (first.operation != next.operation || first.value != next.value) mixed = true;
        }
        if (mixed || special > 1 || (special > 0 && reward.modifiersPerStack.Count > 0))
        {
            var parts = new List<string>();
            if (!Mathf.Approximately(reward.directDamageMultiplier, 1f)) parts.Add("Doğrudan ×" + Number(Mathf.Pow(reward.directDamageMultiplier, count)));
            if (!Mathf.Approximately(reward.rareDirectMultiplier, 1f)) parts.Add(reward.rareDirectFrom + "+ doğrudan ×" + Number(Mathf.Pow(reward.rareDirectMultiplier, count)));
            if (!Mathf.Approximately(reward.behaviorDamageMultiplier, 1f)) parts.Add("Davranış ×" + Number(Mathf.Pow(reward.behaviorDamageMultiplier, count)));
            foreach (var modifier in reward.modifiersPerStack)
                parts.Add(TileBuffText.Name(modifier.statType) + " " + ModifierValue(modifier, count, ""));
            return string.Join(" · ", parts);
        }
        if (!Mathf.Approximately(reward.directDamageMultiplier, 1f)) return "×" + Number(Mathf.Pow(reward.directDamageMultiplier, count));
        if (!Mathf.Approximately(reward.rareDirectMultiplier, 1f)) return "×" + Number(Mathf.Pow(reward.rareDirectMultiplier, count));
        if (!Mathf.Approximately(reward.behaviorDamageMultiplier, 1f)) return "×" + Number(Mathf.Pow(reward.behaviorDamageMultiplier, count));
        if (reward.modifiersPerStack.Count == 0) return "";
        StatModifier m = reward.modifiersPerStack[0];
        return ModifierValue(m, count, reward.inverseLabel);
    }

    // Kırılma ödülü: değerler ödül verisinden. "ilk patlamanın %70'i · yarıçap ×1,25 · 0,20 sn sonra".
    private static string Breakthrough(BossRewardSO reward)
    {
        string Seconds(float s) => s.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',') + " sn sonra";
        string Percent(float ratio) => "%" + Mathf.RoundToInt(ratio * 100f);
        switch (reward.echo)
        {
            case BossRewardEcho.Explosion:
                return $"ilk patlamanın {Percent(reward.echoDamage)} hasarı · yarıçap ×{Number(reward.echoRadius)} · {Seconds(reward.echoDelay)}";
            case BossRewardEcho.Electric:
                // Erişim yalnız ödül kendi erişimini taşıyorsa yazılır (Bölüm 3.7.5); eski ödülün metni aynıdır.
                string reach = reward.echoReach > 0 ? $" · çaprazda {reward.echoReach} hücre" : "";
                return $"ilk dalganın {Percent(reward.echoDamage)} hasarı{reach} · {Seconds(reward.echoDelay)}";
        }
        if (reward.chain) return Chain(reward);
        return $"her {reward.rhythmHarvests} doğrudan hasatta bir: hasar ×{Number(reward.rhythmDamage)} · yarıçap ×{Number(reward.rhythmRadius)}";
    }

    // Zincir Hasat (Bölüm 3.7.6): "davranış hasatları başka saksıların davranışlarını tetikler · en çok 2 ek nesil · şans ve hasar
    // ×0,75 / ×0,50". Nesil başına çarpanlar veriden; şans ve hasar çarpanı aynıysa tek dizi yazılır.
    private static string Chain(BossRewardSO reward)
    {
        string Factors(float[] values)
        {
            var parts = new List<string>();
            if (values != null) foreach (float v in values) parts.Add("×" + Number(v));
            return string.Join(" / ", parts);
        }
        string chance = Factors(reward.chainChance), damage = Factors(reward.chainDamage);
        string falloff = chance == damage ? $"şans ve hasar {chance}" : $"şans {chance} · hasar {damage}";
        return $"davranış hasatları başka saksıların davranışlarını tetikler · en çok {reward.chainGenerations} ek nesil · {falloff}";
    }

    private static string ModifierValue(StatModifier m, int count, string inverseLabel)
    {
        switch (m.operation)
        {
            case ModifierOperation.MorePercent:
                float factor = Mathf.Pow(1f + m.value, count);
                string inverse = string.IsNullOrEmpty(inverseLabel) || factor <= 0f ? "" : $" ({inverseLabel} ×{Number(1f / factor)})";
                return $"×{Number(factor)}{inverse}";
            case ModifierOperation.AddPercent:
                return $"{(m.value >= 0 ? "+" : "−")}%{(Mathf.Abs(m.value) * 100f * count).ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',')}";
            case ModifierOperation.Set:
                return "= " + Number(m.value);
            default:
                return $"{(m.value >= 0 ? "+" : "−")}{(Mathf.Abs(m.value) * count).ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',')} puan";
        }
    }

    // Run sırasındaki kısa liste satırı: "Keskin Bıçak 2/3 · Doğrudan vuruş hasarı ×1,32".
    public static string ListLine(BossRewardSO reward, int count) => $"{reward.displayName} {count}/{reward.maxStacks} · {Total(reward, count)}";

    // Run sonu ekranı: "Keskin Bıçak ×2, Nadir Tohum".
    public static string Summary(BossRewardManager manager)
    {
        if (manager == null || manager.Taken.Count == 0) return null;
        var parts = new List<string>();
        foreach (BossRewardSO reward in manager.Taken)
        {
            int n = manager.Stacks(reward);
            parts.Add(n > 1 ? $"{reward.displayName} ×{n}" : reward.displayName);
        }
        return string.Join(", ", parts);
    }

    // ---------------------------------------------------------------- kazanç / bedel (Bölüm 3.7.4)
    // Kazanç ve bedel satırları ödülün VERİSİNDEN üretilir (ödül adına bakılmaz): seçim hakkı değişimi artıysa kazanç, eksiyse
    // bedel; hasar çarpanı 1'in üstündeyse kazanç, altındaysa bedel. Stat modifier'ı satırları yön bilgisi taşımaz, kazançların
    // ardına yazılır.

    // Kartta kazanç / bedel ayrımıyla gösterilen ödül: bedeli olan ya da seçim hakkını değiştiren.
    public static bool IsTrade(BossRewardSO reward) => reward != null && (reward.HasCost || reward.levelChoiceDelta != 0);

    private enum TradeStyle { Card, Short, Long }

    // Ödülün kazanç ve bedel satırları, üç biçimde: kart ("Doğrudan vuruş hasarı ×0,80"), kısa liste ("Doğrudan ×0,80") ve
    // run sonu ("doğrudan vuruş hasarı ×0,80"). count: alış adedi (çarpanlar üst üste çarpılır, seçim hakkı toplanır).
    private static void Trade(BossRewardSO reward, int count, TradeStyle style, int choicesNow, List<string> gains, List<string> costs)
    {
        gains.Clear(); costs.Clear();
        if (reward == null) return;
        int delta = reward.levelChoiceDelta * count;
        if (delta != 0)
            (delta > 0 ? gains : costs).Add(style == TradeStyle.Card ? $"Gelecekteki her level: {choicesNow} → {choicesNow + delta} seçim"
                : (style == TradeStyle.Short ? "Level seçimi " : "level başına seçim ") + (delta > 0 ? "+" + delta : "−" + Mathf.Abs(delta)));
        void Factor(string card, string brief, string full, float value)
        {
            if (Mathf.Approximately(value, 1f)) return;
            string label = style == TradeStyle.Card ? card : style == TradeStyle.Short ? brief : full;
            (BossRewardSO.IsCost(value) ? costs : gains).Add($"{label} ×{Number(Mathf.Pow(value, count))}");
        }
        Factor("Doğrudan vuruş hasarı", "Doğrudan", "doğrudan vuruş hasarı", reward.directDamageMultiplier);
        Factor($"{reward.rareDirectFrom}+ bitkiye doğrudan vuruş", $"{reward.rareDirectFrom}+ doğrudan", $"{reward.rareDirectFrom}+ bitkiye doğrudan vuruş", reward.rareDirectMultiplier);
        Factor("Davranış hasarı", "Davranış", "davranış hasarı", reward.behaviorDamageMultiplier);
        foreach (StatModifier modifier in reward.modifiersPerStack)
            gains.Add(TileBuffText.Name(modifier.statType) + " " + ModifierValue(modifier, count, ""));
    }

    // Kart satırları. choicesNow: şu an level başına seçim hakkı; "3 → 4" gerçek mevcut ve sonuç değerinden yazılır.
    public static void TradeLines(BossRewardSO reward, int choicesNow, List<string> gains, List<string> costs) =>
        Trade(reward, 1, TradeStyle.Card, choicesNow, gains, costs);

    // Seçim hakkı değişiminin ne zaman geçerli olduğu (kartın açıklamasının başına yazılır).
    public const string PendingChoicesNote = "Önceden kazanılmış seçimler değişmez.";

    // Kısa biçim (HUD listesi, alınan ödüller): "Level seçimi +1 · Doğrudan ×0,80". Önce kazançlar, sonra bedeller.
    private static string TradeValue(BossRewardSO reward, int count)
    {
        var gains = new List<string>(); var costs = new List<string>();
        Trade(reward, count, TradeStyle.Short, 0, gains, costs);
        gains.AddRange(costs);
        return string.Join(" · ", gains);
    }

    // Run sonu: alınan bedelli ödüllerin kazancı ve bedeli, ödül başına bir satır. Bedelli ödül alınmadıysa null.
    // "Bereketli Öğrenim → kazanç: level başına seçim +1 · bedel: doğrudan vuruş hasarı ×0,80"
    public static string TradeSummary(BossRewardManager manager)
    {
        if (manager == null) return null;
        var lines = new List<string>(); var gains = new List<string>(); var costs = new List<string>();
        foreach (BossRewardSO reward in manager.Taken)
        {
            if (!IsTrade(reward)) continue;
            Trade(reward, manager.Stacks(reward), TradeStyle.Long, 0, gains, costs);
            string line = reward.displayName + " →";
            if (gains.Count > 0) line += " kazanç: " + string.Join(", ", gains);
            if (costs.Count > 0) line += (gains.Count > 0 ? " · " : " ") + "bedel: " + string.Join(", ", costs);
            lines.Add(line);
        }
        return lines.Count > 0 ? string.Join("\n", lines) : null;
    }

    // Level başına seçim hakkı satırı (HUD ve run sonu): yalnız run'daki bir etki temel hakkı değiştirdiyse yazılır.
    public static string LevelChoiceLine(int choices, int baseChoices) =>
        choices == baseChoices ? null : $"Level başına seçim: {choices} (temel {baseChoices}) · yeni kazanılan level'lar";

    // Aşamalı havuz (Bölüm 3.7.3): ödül ekranının başlığı ve kartın sınıf etiketi. Sınıf adı bir güç vaadi değildir;
    // etiket yalnız aşamayı ve ödülün ilk sunulduğu boss'u söyler (round, havuz verisi ve run takviminden gelir).
    // Başlık teklifin aşamasını anlatır, kartların sınıfını değil: mevcut aşama tükenip alt aşama kartları geldiğinde de doğrudur.
    public static string StageHeader(BossRewardStage stage) => stage == null ? "" : $"{stage.displayName} AŞAMA · BOSS ÖDÜLÜ";

    public static string StageTag(BossRewardStage stage, int firstBossRound) =>
        stage == null ? "" : firstBossRound > 0 ? $"{stage.displayName} AŞAMA · Round {firstBossRound} boss'undan itibaren" : $"{stage.displayName} AŞAMA";

    public static string Number(float value) => value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
}
