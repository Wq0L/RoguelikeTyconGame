using UnityEditor;
using UnityEngine;

// Tools > Run Profili: hangi run profilinin oynanacağını seçer (Assets/Resources/RunProfileSelection.asset).
// Sahneye dokunmaz. Seçim Play'e basmadan önce yapılır; oyun içinde değişmez.
// "Deney 2.2 ayarı" yalnız Deney22_20 profilinin deney alanlarını değiştirir; diğer profiller etkilenmez.
public static class RunProfileMenu
{
    private const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    private const string Folder = "Assets/ScriptableObjects/RunProfiles/";
    private const string Root = "Tools/Run Profili/";
    private const string ExperimentPath = Folder + "Deney22_20.asset";
    private const string Experiment = Root + "Deney 2.2 ayarı/";
    private const float FixedDuration = 45f;

    [MenuItem(Root + "Prototip · 10 round (normal ekonomi)", priority = 1)]
    private static void Prototype() => Select(Folder + "Prototip10.asset");

    [MenuItem(Root + "Prototip · 10 round (debug bütçe)", priority = 2)]
    private static void PrototypeDebug() => Select(Folder + "Prototip10_DebugButce.asset");

    [MenuItem(Root + "Uzmanlaşma testi · 20 round (normal ekonomi)", priority = 10)]
    private static void Specialization() => Select(Folder + "Uzmanlasma20.asset");

    [MenuItem(Root + "Deney 2.2 · 20 round (süre deneyi)", priority = 12)]
    private static void ExperimentProfile() => Select(ExperimentPath);

    [MenuItem(Root + "Uzun run · 130 round (normal ekonomi)", priority = 20)]
    private static void LongRun() => Select(Folder + "UzunRun130.asset");

    // Bölüm 3.2: uzun run'ın ilk 50 round'u (aynı kota eğrisi, olay ve uzmanlaşma yok). Ölçüm tabanı; nihai 50 round deneyimi değil.
    [MenuItem(Root + "Run50 Referans · 50 round (ölçüm tabanı)", priority = 21)]
    private static void Reference50() => Select(Folder + "Run50_Referans.asset");

    // Bölüm 3.4: her 5 round'da boss (yalnız segmentin son round'unda aktif), ayrı boss hasadı hedefi ve boss ödülleri. İlk test değerleri.
    [MenuItem(Root + "Run50 Boss Prototip · 50 round (boss + ödül)", priority = 22)]
    private static void BossPrototype50() => Select(Folder + "Run50_BossPrototip.asset");

    // Bölüm 3.5: ilk denge adayı. Kendi denge seti (ağaç, can, XP, temel statlar), genişletilmiş boss ödülleri, sabit 45 sn round.
    // Run50 Referans ve Run50 Boss Prototip karşılaştırma için değişmeden durur.
    [MenuItem(Root + "Run50 Denge V1 · 50 round (ilk denge adayı)", priority = 23)]
    private static void BalanceV1() => Select(Folder + "Run50_DengeV1.asset");

    // Bölüm 3.6: DengeV1'in can eğrisi, kotası, süresi ve ekonomisi; erken davranış erişimi, level başına üç kart seçimi ve
    // üç kırılma ödülü (Artçı Patlama, Çifte Akım, Hasat Ritmi). Ölçülmüş denge adayı; insan testi yapılmadı.
    [MenuItem(Root + "Run50 Kırılma V1 · 50 round (build kırılması adayı)", priority = 24)]
    private static void BreakthroughV1() => Select(Folder + "Run50_KirilmaV1.asset");

    // Bölüm 3.7.2: Kırılma V1'in oynanış ayarları (aynı denge seti, XP, 3 seçim, 45 sn, aynı boss ve ödül havuzları) + açık boss takvimi:
    // 15 boss (R3, 6, 10, 13, 16, 20, 23, 26, 30, 33, 36, 40, 43, 46, 50), kota dönemleri boss aralıklarını izler. Hedefler eski tablodan
    // aktarılmış geçici değerlerdir (denge adayı değil); R50 şimdilik havuzdaki normal boss'tur.
    [MenuItem(Root + "Run50 Takvim V1 · 50 round (15 boss takvimi prototipi)", priority = 25)]
    private static void CalendarV1() => Select(Folder + "Run50_TakvimV1.asset");

    // Bölüm 3.7.3: Takvim V1'in her şeyi (15 boss, hedefler, denge seti, 3 seçim, 45 sn) + aşamalı boss ödül havuzu:
    // erken (R1–11), orta (R12–21), güçlü (R22–50). Mevcut 16 ödülün ilk dağılımı; etki ve katsayılar değişmedi.
    [MenuItem(Root + "Run50 Ödül Aşamaları V1 · 50 round (aşamalı boss ödülleri prototipi)", priority = 26)]
    private static void RewardStagesV1() => Select(Folder + "Run50_OdulAsamalariV1.asset");

    // Bölüm 3.7.4: Ödül Aşamaları V1'in her şeyi + güçlü aşamada iki bedelli ödül (Bereketli Öğrenim: level başına seçim +1,
    // doğrudan vuruş ×0,80 · Davranışa Adanış: davranış hasarı ×1,50, level başına seçim −1). İlk test değerleri; dengelenmedi.
    [MenuItem(Root + "Run50 Bedelli Ödüller V1 · 50 round (bedelli boss ödülleri prototipi)", priority = 27)]
    private static void CostRewardsV1() => Select(Folder + "Run50_BedelliOdullerV1.asset");

    // Bölüm 3.7.5: Bedelli Ödüller V1'in her şeyi; yalnız Artçı Patlama'nın ikinci darbesinin yarıçapı (ve havuzda kaldıysa Çifte Akım'ın
    // ikinci dalgasının erişimi) bu profile ait varyantlardan gelir. Hasar oranı, gecikme ve tetik kuralı aynıdır.
    [MenuItem(Root + "Run50 Kırılma Erişimi V1 · 50 round (artçı ve ikinci dalga erişimi adayı)", priority = 28)]
    private static void BreakthroughReachV1() => Select(Folder + "Run50_KirilmaErisimiV1.asset");

    // Bölüm 3.7.6: Kırılma Erişimi V1'in her şeyi + güçlü aşamada Zincir Hasat ödülü (davranış hasatları başka saksıların
    // davranışlarını sınırlı tetikler; iki ek nesil, kök başına bütçe). Ödül normal akışta R23 boss'undan itibaren kazanılır.
    [MenuItem(Root + "Run50 Zincir V1 · 50 round (dört davranışlı zincir prototipi)", priority = 29)]
    private static void ChainV1() => Select(Folder + "Run50_ZincirV1.asset");
    // Bölüm 3.7.7: Zincir V1'in her şeyi + P7 ilerleme ayarları (tablo sonrası büyüyen level maliyeti, toplanan temel güç XP
    // kartları). Veri: Tools/Balance/XPV1.
    [MenuItem(Root + "Run50 XP V1 · 50 round (XP ve sınırsız level adayı)", priority = 30)]
    private static void XpV1() => Select(Folder + "Run50_XPV1.asset");
    // Bölüm 3.7.8: XP V1'in her şeyi + round süresi tablosu (45 / 55 / 65 sn; toplam 51 dk 40 sn aktif süre) ve bu süreye göre
    // ayarlanan can eğrisi, kotalar ve boss hedefleri. Veri: Tools/Balance/AlphaDengeV1.
    [MenuItem(Root + "Run50 Alpha Denge V1 · 50 round (51:40 aktif süre, ilk alpha denge adayı)", priority = 31)]
    private static void AlphaV1() => Select(Folder + "Run50_AlphaDengeV1.asset");

    [MenuItem(Root + "Profil yok (sahnedeki ayarlar)", priority = 40)]
    private static void None() => Select(null);

    [MenuItem(Root + "Prototip · 10 round (normal ekonomi)", true)]
    private static bool PrototypeCheck() => Check(Folder + "Prototip10.asset");
    [MenuItem(Root + "Prototip · 10 round (debug bütçe)", true)]
    private static bool PrototypeDebugCheck() => Check(Folder + "Prototip10_DebugButce.asset");
    [MenuItem(Root + "Uzmanlaşma testi · 20 round (normal ekonomi)", true)]
    private static bool SpecializationCheck() => Check(Folder + "Uzmanlasma20.asset");
    [MenuItem(Root + "Deney 2.2 · 20 round (süre deneyi)", true)]
    private static bool ExperimentProfileCheck() => Check(ExperimentPath);
    [MenuItem(Root + "Uzun run · 130 round (normal ekonomi)", true)]
    private static bool LongRunCheck() => Check(Folder + "UzunRun130.asset");
    [MenuItem(Root + "Run50 Referans · 50 round (ölçüm tabanı)", true)]
    private static bool Reference50Check() => Check(Folder + "Run50_Referans.asset");
    [MenuItem(Root + "Run50 Boss Prototip · 50 round (boss + ödül)", true)]
    private static bool BossPrototype50Check() => Check(Folder + "Run50_BossPrototip.asset");
    [MenuItem(Root + "Run50 Denge V1 · 50 round (ilk denge adayı)", true)]
    private static bool BalanceV1Check() => Check(Folder + "Run50_DengeV1.asset");
    [MenuItem(Root + "Run50 Kırılma V1 · 50 round (build kırılması adayı)", true)]
    private static bool BreakthroughV1Check() => Check(Folder + "Run50_KirilmaV1.asset");
    [MenuItem(Root + "Run50 Takvim V1 · 50 round (15 boss takvimi prototipi)", true)]
    private static bool CalendarV1Check() => Check(Folder + "Run50_TakvimV1.asset");
    [MenuItem(Root + "Run50 Ödül Aşamaları V1 · 50 round (aşamalı boss ödülleri prototipi)", true)]
    private static bool RewardStagesV1Check() => Check(Folder + "Run50_OdulAsamalariV1.asset");
    [MenuItem(Root + "Run50 Bedelli Ödüller V1 · 50 round (bedelli boss ödülleri prototipi)", true)]
    private static bool CostRewardsV1Check() => Check(Folder + "Run50_BedelliOdullerV1.asset");
    [MenuItem(Root + "Run50 Kırılma Erişimi V1 · 50 round (artçı ve ikinci dalga erişimi adayı)", true)]
    private static bool BreakthroughReachV1Check() => Check(Folder + "Run50_KirilmaErisimiV1.asset");
    [MenuItem(Root + "Run50 Zincir V1 · 50 round (dört davranışlı zincir prototipi)", true)]
    private static bool ChainV1Check() => Check(Folder + "Run50_ZincirV1.asset");
    [MenuItem(Root + "Run50 XP V1 · 50 round (XP ve sınırsız level adayı)", true)]
    private static bool XpV1Check() => Check(Folder + "Run50_XPV1.asset");
    [MenuItem(Root + "Run50 Alpha Denge V1 · 50 round (51:40 aktif süre, ilk alpha denge adayı)", true)]
    private static bool AlphaV1Check() => Check(Folder + "Run50_AlphaDengeV1.asset");
    [MenuItem(Root + "Profil yok (sahnedeki ayarlar)", true)]
    private static bool NoneCheck() => Check(null);

    // ---------------- Deney 2.2 ayarı ----------------
    [MenuItem(Experiment + "Süre A · yükseltmeler ve tempo (mevcut)", priority = 60)]
    private static void DurationUpgrades() => Edit(p => p.fixedRoundDuration = 0f);
    [MenuItem(Experiment + "Süre B · profil sabit 45 sn (kontrol)", priority = 61)]
    private static void DurationFixed() => Edit(p => p.fixedRoundDuration = FixedDuration);

    [MenuItem(Experiment + "Süre A · yükseltmeler ve tempo (mevcut)", true)]
    private static bool DurationUpgradesCheck() => Mark(Experiment + "Süre A · yükseltmeler ve tempo (mevcut)", p => p.fixedRoundDuration <= 0f);
    [MenuItem(Experiment + "Süre B · profil sabit 45 sn (kontrol)", true)]
    private static bool DurationFixedCheck() => Mark(Experiment + "Süre B · profil sabit 45 sn (kontrol)", p => p.fixedRoundDuration > 0f);

    private static bool Mark(string item, System.Func<RunProfileSO, bool> state)
    {
        var profile = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ExperimentPath);
        Menu.SetChecked(item, profile != null && state(profile));
        return profile != null && !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static void Edit(System.Action<RunProfileSO> change)
    {
        var profile = AssetDatabase.LoadAssetAtPath<RunProfileSO>(ExperimentPath);
        if (profile == null) { Debug.LogError("Deney profili bulunamadı: " + ExperimentPath); return; }
        Undo.RecordObject(profile, "Deney 2.2 ayarı");
        change(profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        Debug.Log("Deney 2.2: elektrik öldürmeyle tetiklenir (yük deneyi kaldırıldı) · süre " +
                  (profile.fixedRoundDuration > 0f ? $"B (sabit {profile.fixedRoundDuration:0} sn)" : "A (yükseltmeler)"), profile);
    }

    private static bool Check(string path)
    {
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        string current = selection != null && selection.active != null ? AssetDatabase.GetAssetPath(selection.active) : null;
        Menu.SetChecked(Root + Label(path), current == path);
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static string Label(string path) => path == null ? "Profil yok (sahnedeki ayarlar)"
        : path.EndsWith("Prototip10.asset") ? "Prototip · 10 round (normal ekonomi)"
        : path.EndsWith("Prototip10_DebugButce.asset") ? "Prototip · 10 round (debug bütçe)"
        : path.EndsWith("Uzmanlasma20.asset") ? "Uzmanlaşma testi · 20 round (normal ekonomi)"
        : path.EndsWith("Deney22_20.asset") ? "Deney 2.2 · 20 round (süre deneyi)"
        : path.EndsWith("Run50_Referans.asset") ? "Run50 Referans · 50 round (ölçüm tabanı)"
        : path.EndsWith("Run50_BossPrototip.asset") ? "Run50 Boss Prototip · 50 round (boss + ödül)"
        : path.EndsWith("Run50_DengeV1.asset") ? "Run50 Denge V1 · 50 round (ilk denge adayı)"
        : path.EndsWith("Run50_KirilmaV1.asset") ? "Run50 Kırılma V1 · 50 round (build kırılması adayı)"
        : path.EndsWith("Run50_TakvimV1.asset") ? "Run50 Takvim V1 · 50 round (15 boss takvimi prototipi)"
        : path.EndsWith("Run50_OdulAsamalariV1.asset") ? "Run50 Ödül Aşamaları V1 · 50 round (aşamalı boss ödülleri prototipi)"
        : path.EndsWith("Run50_BedelliOdullerV1.asset") ? "Run50 Bedelli Ödüller V1 · 50 round (bedelli boss ödülleri prototipi)"
        : path.EndsWith("Run50_KirilmaErisimiV1.asset") ? "Run50 Kırılma Erişimi V1 · 50 round (artçı ve ikinci dalga erişimi adayı)"
        : path.EndsWith("Run50_ZincirV1.asset") ? "Run50 Zincir V1 · 50 round (dört davranışlı zincir prototipi)"
        : path.EndsWith("Run50_XPV1.asset") ? "Run50 XP V1 · 50 round (XP ve sınırsız level adayı)"
        : path.EndsWith("Run50_AlphaDengeV1.asset") ? "Run50 Alpha Denge V1 · 50 round (51:40 aktif süre, ilk alpha denge adayı)"
        : "Uzun run · 130 round (normal ekonomi)";

    private static void Select(string path)
    {
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        if (selection == null) { Debug.LogError("RunProfileSelection.asset bulunamadı: " + SelectionPath); return; }
        RunProfileSO profile = path != null ? AssetDatabase.LoadAssetAtPath<RunProfileSO>(path) : null;
        if (path != null && profile == null) { Debug.LogError("Run profili bulunamadı: " + path); return; }
        // Geçersiz boss takvimi olan profil seçilmez (oyun onu başlatmaz); seçim olduğu gibi kalır.
        string calendarError = RunCalendar.Validate(profile);
        if (calendarError != null) { Debug.LogError($"Run profili seçilmedi: '{profile.displayName}' boss takvimi geçersiz — {calendarError}", profile); return; }
        string rewardError = profile != null ? BossRewardPoolSO.Validate(profile.bossRewards, profile.runLength) : null;
        if (rewardError != null) { Debug.LogError($"Run profili seçilmedi: '{profile.displayName}' boss ödül aşamaları geçersiz — {rewardError}", profile); return; }
        selection.active = profile;
        EditorUtility.SetDirty(selection);
        AssetDatabase.SaveAssetIfDirty(selection);
        Debug.Log("Run profili: " + (selection.active != null ? selection.active.displayName : "yok (sahnedeki ayarlar)"), selection);
    }
}
