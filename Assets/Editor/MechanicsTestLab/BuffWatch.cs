using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MechanicsTestLab
{
    // Geçici ve koşullu etkilerin yaşam döngüsü: eklendi · süresi yenilendi · hak hazırlandı / harcandı / kayboldu · süresi
    // bitti · temizlendi. Yalnız gözlemdir: oyunun kendi durumunu (etkilerin açık alanları, StatManager listesi, boss ve tarla
    // olayı yürütücüleri, saksıların etkin rezonansları) kare sonunda okur ve değişimi olay kaydına yazar. Hiçbir süreyi kendisi
    // saymaz. Olay kaydı kapalıyken çalışmaz.
    public static class BuffWatch
    {
        private static bool attached;
        private static bool reactorOn, rhythmOn, rhythmReady, greedOn, greedReady, pauseOn;
        private static int reactorRefreshes, rhythmCharges, rhythmSpent, greedSpent, greedLost, blocked, slowed;
        private static string bossName = "", eventName = "";
        private static FieldEventPhase eventPhase;
        private static readonly Dictionary<PlanterBrain, string> resonances = new();
        private static readonly List<PlanterBrain> planters = new();
        private static readonly List<PlanterBrain> gone = new();
        private static readonly StatType[] ReactorChances = { StatType.ExplosionChance, StatType.TornadoChance, StatType.BoomerangChance, StatType.ElectricChance };

        public static void SetAttached(bool wanted)
        {
            if (wanted == attached) return;
            attached = wanted;
            if (wanted) { Sync(); TestTime.FrameEnded += Poll; }
            else TestTime.FrameEnded -= Poll;
        }

        // Yeni sahne: karşılaştırma tabanı o anki duruma çekilir (eski oturumun durumu "kalktı" diye yazılmaz).
        public static void Sync()
        {
            resonances.Clear();
            Read(false);
        }

        private static void Poll() { if (TestSession.Ready) Read(true); }

        private static void Log(string text) => TestLog.Buff(text);

        // Nadir Reaktör'ün eklediği şans çarpanlarının StatManager'daki sayısı (her davranış şansı için tam bir tane olmalı).
        public static int ReactorModifiers(out float bonus)
        {
            bonus = 0f;
            RunEffects effects = RunEffects.Instance;
            RareReactorEffect reactor = effects != null ? effects.Find<RareReactorEffect>() : null;
            StatManager stats = StatManager.Instance;
            if (reactor == null || stats == null || !(reactor.Data is RareReactorEffectSO data)) return 0;
            bonus = data.chanceBonus;
            int count = 0;
            foreach (StatModifier modifier in stats.GlobalModifiers)
                if (System.Array.IndexOf(ReactorChances, modifier.statType) >= 0 && modifier.target == StatTarget.Planter &&
                    modifier.operation == ModifierOperation.MorePercent && Mathf.Approximately(modifier.value, data.chanceBonus)) count++;
            return count;
        }

        private static void Read(bool report)
        {
            RunEffects effects = RunEffects.Instance;
            RareReactorEffect reactor = effects != null ? effects.Find<RareReactorEffect>() : null;
            bool on = reactor != null && reactor.IsActive;
            int refreshes = reactor != null ? reactor.Refreshes : 0;
            if (report && on && !reactorOn)
                Log($"Nadir Reaktör EKLENDİ · süre {reactor.Remaining:0.0} sn · şans çarpanı ×{1f + ((RareReactorEffectSO)reactor.Data).chanceBonus:0.##} · eklenen modifier {ReactorModifiers(out _)}");
            else if (report && on && refreshes > reactorRefreshes)
            {
                int count = ReactorModifiers(out _);
                Log($"Nadir Reaktör süresi YENİLENDİ · kalan {reactor.Remaining:0.0} sn · modifier sayısı {count} " + (count == ReactorChances.Length ? "(çarpan katlanmadı)" : "(BEKLENMEYEN: 4 olmalı)"));
            }
            else if (report && !on && reactorOn)
                Log($"Nadir Reaktör {(reactor == null ? "TEMİZLENDİ" : "süresi BİTTİ")} · kalan modifier {ReactorModifiers(out _)} (0 olmalı)");
            reactorOn = on; reactorRefreshes = refreshes;

            HarvestRhythm rhythm = RunPower.Rhythm;
            bool enabled = rhythm.Enabled;
            if (report && enabled && !rhythmOn) Log($"Hasat Ritmi koşullu etkisi KURULDU · eşik {rhythm.Threshold} doğrudan hasat · henüz tetiklenmedi");
            if (report && !enabled && rhythmOn) Log("Hasat Ritmi TEMİZLENDİ");
            if (report && enabled && rhythm.Charges > rhythmCharges) Log($"Hasat Ritmi hakkı HAZIRLANDI ({rhythm.Charges}. hak): sıradaki saldırı güçlenir");
            if (report && enabled && rhythm.EmpoweredAttacks > rhythmSpent) Log($"Hasat Ritmi hakkı HARCANDI ({rhythm.EmpoweredAttacks}. güçlenmiş saldırı)");
            else if (report && enabled && rhythmReady && !rhythm.Ready && rhythm.EmpoweredAttacks == rhythmSpent) Log("Hasat Ritmi hakkı harcanmadan kalktı");
            rhythmOn = enabled; rhythmReady = rhythm.Ready; rhythmCharges = rhythm.Charges; rhythmSpent = rhythm.EmpoweredAttacks;

            GreedyHarvestEffect greed = effects != null ? effects.Find<GreedyHarvestEffect>() : null;
            if (report && greed != null && !greedOn) Log("Açgözlü Hasat koşullu etkisi KURULDU · henüz tetiklenmedi");
            if (report && greed == null && greedOn) Log("Açgözlü Hasat TEMİZLENDİ");
            if (report && greed != null && greed.Ready && !greedReady) Log($"Açgözlü Hasat hakkı HAZIRLANDI ({greed.Charges}. hak)");
            if (report && greed != null && greed.Spent > greedSpent) Log($"Açgözlü Hasat hakkı HARCANDI ({greed.Spent}. güçlenmiş saldırı)");
            if (report && greed != null && greed.Lost > greedLost) Log($"Açgözlü Hasat hakkı KAYBOLDU: koşul sağlanmadı (tek canlı hedefe vuruldu) · toplam {greed.Lost}");
            greedOn = greed != null; greedReady = greed != null && greed.Ready; greedSpent = greed != null ? greed.Spent : 0; greedLost = greed != null ? greed.Lost : 0;

            if (report && DirectAttackPause.Active != pauseOn)
                Log(DirectAttackPause.Active ? $"Doğrudan saldırı duraklaması EKLENDİ · {DirectAttackPause.Remaining:0.00} sn" : "Doğrudan saldırı duraklaması BİTTİ");
            pauseOn = DirectAttackPause.Active;

            SegmentEventRuntime boss = SegmentEventDirector.Instance != null ? SegmentEventDirector.Instance.Active : null;
            string bossNow = boss != null && boss.Data != null ? boss.Data.displayName : "";
            if (report && bossNow != bossName)
                Log(bossNow.Length > 0 ? $"Boss kuralı EKLENDİ: {bossNow} · {boss.RuleSummary} · {boss.ZoneCells.Count} hücre" : $"Boss kuralı KALKTI: {bossName}");
            bossName = bossNow;

            FieldEventDirector director = FieldEventDirector.Instance;
            FieldEvent current = director != null ? director.Current : null;
            string eventNow = current != null ? current.Title : "";
            if (report && eventNow != eventName) Log(eventNow.Length > 0 ? $"Tarla olayı KURULDU: {eventNow}" : $"Tarla olayı TEMİZLENDİ: {eventName}");
            else if (report && current != null && current.Phase != eventPhase) Log($"Tarla olayı fazı: {eventNow} → {current.Phase}");
            eventName = eventNow; eventPhase = current != null ? current.Phase : FieldEventPhase.Waiting;
            int blockedNow = director != null ? director.Zones.BlockedCells : 0, slowedNow = director != null ? director.Zones.SlowedCells : 0;
            if (report && (blockedNow != blocked || slowedNow != slowed))
                Log($"Tarla olayı üretim etkisi: kapalı hücre {blocked} → {blockedNow} · yavaş hücre {slowed} → {slowedNow}");
            blocked = blockedNow; slowed = slowedNow;

            ReadResonances(report);
        }

        private static void ReadResonances(bool report)
        {
            TestField.Planters(planters);
            var text = new StringBuilder();
            foreach (PlanterBrain brain in planters)
            {
                text.Clear();
                foreach (ActiveResonance resonance in brain.ActiveResonances)
                    text.Append(text.Length > 0 ? ", " : "").Append(resonance.resonanceName).Append(' ').Append(resonance.requiredCount);
                string now = text.ToString();
                resonances.TryGetValue(brain, out string before);
                before ??= "";
                if (report && now != before)
                    Log($"Rezonans saksı ({brain.EditorOrigin.x}, {brain.EditorOrigin.z}): {(before.Length > 0 ? before : "yok")} → {(now.Length > 0 ? now : "yok (şart bozuldu / tile kalktı)")}");
                resonances[brain] = now;
            }
            gone.Clear();
            foreach (PlanterBrain known in resonances.Keys) if (known == null || !planters.Contains(known)) gone.Add(known);
            foreach (PlanterBrain removed in gone) resonances.Remove(removed);
        }

        // Panel: şu an aktif geçici / koşullu etkiler ve kalan süreleri (oyunun durumundan).
        public static void Describe(StringBuilder text)
        {
            RunEffects effects = RunEffects.Instance;
            RareReactorEffect reactor = effects != null ? effects.Find<RareReactorEffect>() : null;
            if (reactor != null)
                text.Append("Nadir Reaktör: ").Append(reactor.IsActive ? $"AKTİF · kalan {reactor.Remaining:0.0} sn" : "beklemede")
                    .Append($" · çalışma {reactor.Activations} · yenileme {reactor.Refreshes} · StatManager'daki çarpan sayısı {ReactorModifiers(out float bonus)} (aktifken 4, değilken 0) · ×{1f + bonus:0.##}\n");
            HarvestRhythm rhythm = RunPower.Rhythm;
            if (rhythm.Enabled)
                text.Append("Hasat Ritmi: ").Append(rhythm.Ready ? "HAK HAZIR" : $"sayaç {rhythm.Count}/{rhythm.Threshold}").Append($" · hazırlanan {rhythm.Charges} · harcanan {rhythm.EmpoweredAttacks}\n");
            GreedyHarvestEffect greed = effects != null ? effects.Find<GreedyHarvestEffect>() : null;
            if (greed != null)
                text.Append("Açgözlü Hasat: ").Append(greed.Ready ? "HAK HAZIR" : "hak yok").Append($" · hazırlanan {greed.Charges} · harcanan {greed.Spent} · kaybolan {greed.Lost}\n");
            if (DirectAttackPause.Active) text.Append($"Doğrudan saldırı duraklaması: kalan {DirectAttackPause.Remaining:0.00} sn\n");
            SegmentEventRuntime boss = SegmentEventDirector.Instance != null ? SegmentEventDirector.Instance.Active : null;
            if (boss != null) text.Append($"Boss kuralı: {boss.Data.displayName} · {boss.RuleSummary} · round sonunda kalkar\n");
            FieldEvent current = FieldEventDirector.Instance != null ? FieldEventDirector.Instance.Current : null;
            if (current != null)
                text.Append($"Tarla olayı: {current.Title} · faz {current.Phase}" + (current.Phase == FieldEventPhase.Active && !float.IsInfinity(current.ActiveRemaining) ? $" · kalan {current.ActiveRemaining:0.0} sn" : "") + "\n");
            if (TestStats.Contributions.Count > 0) text.Append($"Test katkısı: {TestStats.Contributions.Count} (sıfırlanana kadar)\n");
            if (text.Length == 0) text.Append("Aktif geçici ya da koşullu etki yok.\n");
        }
    }
}
