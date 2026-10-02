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
                return $"ilk dalganın {Percent(reward.echoDamage)} hasarı · {Seconds(reward.echoDelay)}";
        }
        return $"her {reward.rhythmHarvests} doğrudan hasatta bir: hasar ×{Number(reward.rhythmDamage)} · yarıçap ×{Number(reward.rhythmRadius)}";
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

    public static string Number(float value) => value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
}
