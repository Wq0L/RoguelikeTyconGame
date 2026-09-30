using UnityEngine;

// Hangi run profilinin oynanacağı: Assets/Resources/RunProfileSelection.asset. Tools > Run Profili menüsü değiştirir;
// asset seçilip Inspector'dan da değiştirilebilir. Boş: sahnedeki RoundManager / ResourceManager ayarları.
public class RunProfileSelectionSO : ScriptableObject
{
    public const string ResourcePath = "RunProfileSelection";

    public RunProfileSO active;

    private static RunProfileSelectionSO loaded;
    private static bool attempted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loaded = null;
        attempted = false;
    }

    public static RunProfileSO Active
    {
        get
        {
            if (!attempted)
            {
                loaded = Resources.Load<RunProfileSelectionSO>(ResourcePath);
                attempted = true;
            }
            return loaded != null ? loaded.active : null;
        }
    }
}
