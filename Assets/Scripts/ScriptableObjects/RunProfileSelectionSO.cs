using UnityEngine;

// Hangi run profilinin oynanacağı: Assets/Resources/RunProfileSelection.asset. Tools > Run Profili menüsü değiştirir;
// asset seçilip Inspector'dan da değiştirilebilir. Boş: sahnedeki RoundManager / ResourceManager ayarları.
public class RunProfileSelectionSO : ScriptableObject
{
    public const string ResourcePath = "RunProfileSelection";

    public RunProfileSO active;

    private static RunProfileSelectionSO loaded;
    private static bool attempted;
    private static bool overridden;
    private static RunProfileSO sessionProfile;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loaded = null;
        attempted = false;
    }

    // Testler: seçim dosyasına dokunmadan bu oturumda oynanacak profil (null: "profil yok"). Dosyaya yazılmaz, oyuncunun
    // seçimi değişmez. Play'e girmeden önce kurulur (bu yüzden Play başındaki sıfırlamaya girmez); iş bitince çağıran kaldırır.
    public static bool HasSessionOverride => overridden;

    public static void OverrideForSession(RunProfileSO profile)
    {
        overridden = true;
        sessionProfile = profile;
    }

    public static void ClearSessionOverride()
    {
        overridden = false;
        sessionProfile = null;
    }

    public static RunProfileSO Active
    {
        get
        {
            if (DemoSceneSettings.IsDemo) return DemoSceneSettings.Instance.Profile;
            if (overridden) return sessionProfile;
            if (!attempted)
            {
                loaded = Resources.Load<RunProfileSelectionSO>(ResourcePath);
                attempted = true;
            }
            return loaded != null ? loaded.active : null;
        }
    }
}
