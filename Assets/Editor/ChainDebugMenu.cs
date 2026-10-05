using UnityEditor;
using UnityEngine;

// Bölüm 3.7.6 karşılaştırma yolu (yalnız Editor, yalnız Play modunda ve round sırasında): o anki run'a Zincir Hasat'ı verir.
// Aynı run'da zincirsiz oynanan round'lar ile zincirli round'lar karşılaştırılabilir. Normal akışta ödül R23 boss'undan sonra
// havuzdan kazanılır; bu menü o akışı değiştirmez. Ödül run'a aittir (yeni run'da kalkar); gerçek kayda (MetaSave) ve profil
// seçimine yazılmaz.
// Ödül oyunun kendi doğrulama ve uygulama yolundan geçer (BossRewardManager.EditorGrant: yalnız Editor'da derlenen dar giriş;
// alınabilirlik denetimi ve tek işlemli uygulama oyunun kendisininkidir). Yöneticinin iç alanlarına yansımayla dokunulmaz;
// geçersiz ya da zaten alınmış ödül zorla uygulanmaz. Bu dosya Editor derlemesindedir: build'e girmez.
public static class ChainDebugMenu
{
    private const string Item = "Tools/Zincir Deneme/Bu run'a Zincir Hasat ver (yalnız Play · karşılaştırma)";
    private const string RewardPath = "Assets/ScriptableObjects/BossRewards/ZincirV1/ZincirHasat_Z1.asset";

    [MenuItem(Item, priority = 100)]
    private static void Grant()
    {
        var boss = BossRewardManager.Instance;
        var reward = AssetDatabase.LoadAssetAtPath<BossRewardSO>(RewardPath);
        if (boss == null || reward == null) { Debug.LogError("Zincir deneme: BossRewardManager ya da Zincir Hasat asset'i bulunamadı."); return; }
        bool taken = boss.EditorGrant(reward, out string refusal);
        Debug.Log(taken ? "Zincir deneme: Zincir Hasat bu run'a verildi (yalnız bu Play oturumu; yeni run'da kalkar)." : $"Zincir deneme: ödül verilmedi ({refusal}).");
    }

    [MenuItem(Item, true)]
    private static bool CanGrant() =>
        EditorApplication.isPlaying && BossRewardManager.Instance != null && !BossRewardManager.Instance.IsPending && !BossRewardManager.TryGetChain(out _) &&
        GameManager.Instance != null && GameManager.Instance.CurrentState == GameStates.Round;
}
