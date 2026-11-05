using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Boss ödülü metinleri: etki değeri asset'ten yazılır. Çarpan "×", yüzde puanı "puan"; aralık küçülmesi ile sıklık artışı ayrı yazılır.
// Yazılar çeviri tablosundan (ui.reward.*), ödül adı / etiketi verideki çeviriden (BossRewardSO.DisplayName / EffectLabel);
// ondalık ayırıcı etkin dile göre (tr "1,15", en "1.15").
public static class BossRewardText
{
    // Tek alışın etkisi: "Saldırı aralığı ×0,90 (saldırı sıklığı ×1,11)".
    public static string Effect(BossRewardSO reward) => Describe(reward, 1);

    // n alışın toplam etkisi: çarpanlar üst üste çarpılır (×1,15 → ×1,32 → ×1,52), puanlar toplanır (+8 → +16 → +24).
    public static string Total(BossRewardSO reward, int count) => count <= 0 ? GameText.Get("ui.reward.no_effect") : Describe(reward, count);

    private static string Describe(BossRewardSO reward, int count) => reward == null ? "" : $"{reward.EffectLabel} {Value(reward, count)}".Trim();

    // Yalnız değer: "×1,32", "×0,81 (saldırı sıklığı ×1,23)", "+16 puan".
    public static string Value(BossRewardSO reward, int count)
    {
        if (reward == null || count <= 0) return "";
        // Çalışma zamanı etkisi (Bölüm 3.8): metin etkinin kendi verisinden yazılır.
        if (reward.effect != null) return reward.effect.Summary;
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
            if (!Mathf.Approximately(reward.directDamageMultiplier, 1f))
                parts.Add(Factor("ui.reward.factor.direct.short", Mathf.Pow(reward.directDamageMultiplier, count)));
            if (!Mathf.Approximately(reward.rareDirectMultiplier, 1f))
                parts.Add(Factor("ui.reward.factor.rare.short", Mathf.Pow(reward.rareDirectMultiplier, count), Rarity(reward)));
            if (!Mathf.Approximately(reward.behaviorDamageMultiplier, 1f))
                parts.Add(Factor("ui.reward.factor.behavior.short", Mathf.Pow(reward.behaviorDamageMultiplier, count)));
            foreach (var modifier in reward.modifiersPerStack)
                parts.Add(TileBuffText.Name(modifier.statType) + " " + ModifierValue(modifier, count, ""));
            return string.Join(" · ", parts);
        }
        if (!Mathf.Approximately(reward.directDamageMultiplier, 1f)) return "×" + Number(Mathf.Pow(reward.directDamageMultiplier, count));
        if (!Mathf.Approximately(reward.rareDirectMultiplier, 1f)) return "×" + Number(Mathf.Pow(reward.rareDirectMultiplier, count));
        if (!Mathf.Approximately(reward.behaviorDamageMultiplier, 1f)) return "×" + Number(Mathf.Pow(reward.behaviorDamageMultiplier, count));
        if (reward.modifiersPerStack.Count == 0) return "";
        StatModifier m = reward.modifiersPerStack[0];
        return ModifierValue(m, count, reward.InverseLabel);
    }

    // "Rare" (nadirlik adı listeden).
    private static string Rarity(BossRewardSO reward) => TileBuffText.RarityName(reward.rareDirectFrom);

    // "<etiket> ×<çarpan>": etiket anahtarı (yer tutuculu olabilir: {0} nadirlik), çarpan sonda.
    private static string Factor(string labelKey, float value, string rarity = null) =>
        (rarity != null ? GameText.Format(labelKey, rarity) : GameText.Get(labelKey)) + " ×" + Number(value);

    private static string Percent(int value) => GameText.Format("ui.format.percent", value);

    // Kırılma ödülü: değerler ödül verisinden. "ilk patlamanın %70'i · yarıçap ×1,25 · 0,20 sn sonra".
    private static string Breakthrough(BossRewardSO reward)
    {
        int Ratio(float ratio) => Mathf.RoundToInt(ratio * 100f);
        switch (reward.echo)
        {
            case BossRewardEcho.Explosion:
                return GameText.Format("ui.reward.echo.explosion", BehaviorEchoes.AftershockSteps, Number(BehaviorEchoes.AftershockInterval), Percent(Ratio(reward.echoDamage)));
            case BossRewardEcho.Electric:
                // Erişim yalnız ödül kendi erişimini taşıyorsa yazılır (Bölüm 3.7.5); eski ödülün metni aynıdır.
                string reach = reward.echoReach > 0 ? " · " + GameText.Format("ui.reward.echo.reach", reward.echoReach) : "";
                return GameText.Format("ui.reward.echo.electric", Percent(Ratio(reward.echoDamage))) + reach + " · " +
                       GameText.Format("ui.reward.echo.delay", Number(reward.echoDelay));
        }
        if (reward.chain) return Chain(reward);
        return GameText.Format("ui.reward.rhythm", reward.rhythmHarvests, Number(reward.rhythmDamage), Number(reward.rhythmRadius));
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
        string falloff = chance == damage ? GameText.Format("ui.reward.chain.same", chance) : GameText.Format("ui.reward.chain.split", chance, damage);
        return GameText.Format("ui.reward.chain", reward.chainGenerations, falloff);
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
                return (m.value >= 0 ? "+" : "−") + GameText.Format("ui.format.percent", GameText.Number(Mathf.Abs(m.value) * 100f * count, "0.#"));
            case ModifierOperation.Set:
                return "= " + Number(m.value);
            default:
                return (m.value >= 0 ? "+" : "−") + GameText.Number(Mathf.Abs(m.value) * count, "0.##") + " " + GameText.Get("ui.unit.points");
        }
    }

    // Run sırasındaki kısa liste satırı: "Keskin Bıçak 2/3 · Doğrudan vuruş hasarı ×1,32". Etkili ödülde uzun kural metni
    // tekrarlanmaz: yalnız ad ve etiket ("Elektrik Anteni · Anten").
    public static string ListLine(BossRewardSO reward, int count) => reward.effect != null
        ? $"{reward.DisplayName} · {reward.EffectLabel}".TrimEnd(' ', '·')
        : $"{reward.DisplayName} {count}/{reward.maxStacks} · {Total(reward, count)}";

    // Etkili ödülün kartındaki alt satırlar (Bölüm 3.8): bir kez alındığı, ön koşulu, yerleşim adımı ve birlikte alınamayan ödül.
    public static string EffectStackText(BossRewardSO reward, List<BossRewardSO> exclusive)
    {
        var text = new System.Text.StringBuilder("<color=#6E606E>").Append(GameText.Get("ui.reward.once"));
        if (reward.requires != null) text.Append(" · ").Append(GameText.Format("ui.reward.requires", reward.requires.DisplayName));
        text.Append("</color>");
        if (reward.effect != null && reward.effect.NeedsPlacement) text.Append("\n<color=#2E7D32>").Append(GameText.Get("ui.reward.you_place")).Append("</color>");
        if (exclusive != null && exclusive.Count > 0)
        {
            var names = new List<string>();
            foreach (BossRewardSO other in exclusive) names.Add(other.DisplayName);
            text.Append("\n<color=#8A3A12>").Append(GameText.Format("ui.reward.excludes", string.Join(", ", names))).Append("</color>");
        }
        return text.ToString();
    }

    // Run sonu ekranı: "Keskin Bıçak ×2, Nadir Tohum".
    public static string Summary(BossRewardManager manager)
    {
        if (manager == null || manager.Taken.Count == 0) return null;
        var parts = new List<string>();
        foreach (BossRewardSO reward in manager.Taken)
        {
            int n = manager.Stacks(reward);
            parts.Add(n > 1 ? $"{reward.DisplayName} ×{n}" : reward.DisplayName);
        }
        return string.Join(", ", parts);
    }

    // ---------------------------------------------------------------- kazanç / bedel (Bölüm 3.7.4)
    // Kazanç ve bedel satırları ödülün VERİSİNDEN üretilir (ödül adına bakılmaz): seçim hakkı değişimi artıysa kazanç, eksiyse
    // bedel; hasar çarpanı 1'in üstündeyse kazanç, altındaysa bedel. Stat modifier'ı satırları yön bilgisi taşımaz, kazançların
    // ardına yazılır.

    // Kartta kazanç / bedel ayrımıyla gösterilen ödül: bedeli olan ya da seçim hakkını değiştiren.
    public static bool IsTrade(BossRewardSO reward) => reward != null && (reward.HasCost || reward.levelChoiceDelta != 0 || reward.movingBehaviorLimitBonus != 0);

    private enum TradeStyle { Card, Short, Long }

    // Ödülün kazanç ve bedel satırları, üç biçimde: kart ("Doğrudan vuruş hasarı ×0,80"), kısa liste ("Doğrudan ×0,80") ve
    // run sonu ("doğrudan vuruş hasarı ×0,80"). count: alış adedi (çarpanlar üst üste çarpılır, seçim hakkı toplanır).
    private static void Trade(BossRewardSO reward, int count, TradeStyle style, int choicesNow, List<string> gains, List<string> costs)
    {
        gains.Clear(); costs.Clear();
        if (reward == null) return;
        if (reward.movingBehaviorLimitBonus > 0)
        {
            int bonus = reward.movingBehaviorLimitBonus * count;
            int current = MovingBehaviorLimits.Resolve(12);
            gains.Add(style == TradeStyle.Card
                ? GameText.Format("ui.reward.trade.moving_card", current, current + bonus)
                : GameText.Format("ui.reward.trade.moving", bonus));
        }
        int delta = reward.levelChoiceDelta * count;
        if (delta != 0)
        {
            string signed = delta > 0 ? "+" + delta : "−" + Mathf.Abs(delta);
            (delta > 0 ? gains : costs).Add(style == TradeStyle.Card ? GameText.Format("ui.reward.trade.choices_card", choicesNow, choicesNow + delta)
                : GameText.Format(style == TradeStyle.Short ? "ui.reward.trade.choices_short" : "ui.reward.trade.choices_long", signed));
        }
        // Etiket anahtarı = önek + biçim (ui.reward.factor.direct.card / .short / .long).
        string Suffix() => style == TradeStyle.Card ? "card" : style == TradeStyle.Short ? "short" : "long";
        void Add(string prefix, float value, string rarity = null)
        {
            if (Mathf.Approximately(value, 1f)) return;
            (BossRewardSO.IsCost(value) ? costs : gains).Add(Factor(prefix + Suffix(), Mathf.Pow(value, count), rarity));
        }
        Add("ui.reward.factor.direct.", reward.directDamageMultiplier);
        Add("ui.reward.factor.rare.", reward.rareDirectMultiplier, Rarity(reward));
        Add("ui.reward.factor.behavior.", reward.behaviorDamageMultiplier);
        foreach (StatModifier modifier in reward.modifiersPerStack)
            gains.Add(TileBuffText.Name(modifier.statType) + " " + ModifierValue(modifier, count, ""));
    }

    // Kart satırları. choicesNow: şu an level başına seçim hakkı; "3 → 4" gerçek mevcut ve sonuç değerinden yazılır.
    public static void TradeLines(BossRewardSO reward, int choicesNow, List<string> gains, List<string> costs) =>
        Trade(reward, 1, TradeStyle.Card, choicesNow, gains, costs);

    // Seçim hakkı değişiminin ne zaman geçerli olduğu (kartın açıklamasının başına yazılır).
    public static string PendingChoicesNote => GameText.Get("ui.reward.pending_note");

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
            string line = reward.DisplayName + " →";
            if (gains.Count > 0) line += " " + GameText.Format("ui.reward.trade.gain", string.Join(", ", gains));
            if (costs.Count > 0) line += (gains.Count > 0 ? " · " : " ") + GameText.Format("ui.reward.trade.cost", string.Join(", ", costs));
            lines.Add(line);
        }
        return lines.Count > 0 ? string.Join("\n", lines) : null;
    }

    // Level başına seçim hakkı satırı (HUD ve run sonu): yalnız run'daki bir etki temel hakkı değiştirdiyse yazılır.
    public static string LevelChoiceLine(int choices, int baseChoices) =>
        choices == baseChoices ? null : GameText.Format("ui.reward.level_choice_line", choices, baseChoices);

    // Aşamalı havuz (Bölüm 3.7.3): ödül ekranının başlığı ve kartın sınıf etiketi. Sınıf adı bir güç vaadi değildir;
    // etiket yalnız aşamayı ve ödülün ilk sunulduğu boss'u söyler (round, havuz verisi ve run takviminden gelir).
    // Başlık teklifin aşamasını anlatır, kartların sınıfını değil: mevcut aşama tükenip alt aşama kartları geldiğinde de doğrudur.
    public static string StageHeader(BossRewardStage stage) => stage == null ? "" : GameText.Format("ui.reward.stage_header", stage.DisplayName);

    public static string StageTag(BossRewardStage stage, int firstBossRound) =>
        stage == null ? "" : firstBossRound > 0 ? GameText.Format("ui.reward.stage_tag_from", stage.DisplayName, firstBossRound)
            : GameText.Format("ui.reward.stage_tag", stage.DisplayName);

    public static string Number(float value) => GameText.Number(value, "0.00");
}
