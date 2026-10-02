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
        : "Uzun run · 130 round (normal ekonomi)";

    private static void Select(string path)
    {
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        if (selection == null) { Debug.LogError("RunProfileSelection.asset bulunamadı: " + SelectionPath); return; }
        selection.active = path != null ? AssetDatabase.LoadAssetAtPath<RunProfileSO>(path) : null;
        if (path != null && selection.active == null) { Debug.LogError("Run profili bulunamadı: " + path); return; }
        EditorUtility.SetDirty(selection);
        AssetDatabase.SaveAssetIfDirty(selection);
        Debug.Log("Run profili: " + (selection.active != null ? selection.active.displayName : "yok (sahnedeki ayarlar)"), selection);
    }
}
