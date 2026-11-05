using UnityEngine;

// Eri'nin üzgün aşamasında konuşma kutusunun arkası solar (Faz 3 C adımı, 5 Kasım 2026 kullanıcı kararı: "D · Düz gri", ilk örnekteki
// oran; kapsam kullanıcının isteğiyle yalnız konuşma kutusu). Mutlu ve kırmızı (şeytani) aşamada normal renkler.
// Kapsam: konuşma kutusunun plakaları (DialoguePlateGraphic → UI/ComicMood). Diğer arayüz (düğmeler, paneller, bilgi pencereleri),
// yazılar ve dünya değişmez.
// Tek bir ortak gölgelendirici değeri (_UiMoodGrey) yazılır: nesnelere tek tek dokunulmaz, karede iş yok.
public static class UiMood
{
    // Gri oranı (0: renkler aynı, 1: tam gri). Kullanıcının seçtiği örnekteki değer.
    public const float SadGrey = .8f;
    public const string MaterialResource = "UI/ComicMoodUI";
    private static readonly int GreyId = Shader.PropertyToID("_UiMoodGrey");
    private static Material material;
    private static bool loaded;

    public static float Grey => DialogueProgress.Stage == DialogueProgress.Sad ? SadGrey : 0f;

    // Konuşma plakalarının ortak materyali (Resources'ta: derlemeye girer). Yoksa Unity'nin varsayılanı.
    public static Material Material
    {
        get
        {
            if (!loaded) { loaded = true; material = Resources.Load<Material>(MaterialResource); }
            return material;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { material = null; loaded = false; }

    // DialogueProgress'in olayı SubsystemRegistration'da sıfırlanır; abonelik ondan sonra.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        DialogueProgress.StageChanged -= Apply; DialogueProgress.StageChanged += Apply;
        Application.quitting -= Clear; Application.quitting += Clear;   // editörde Play bitince ortak değer sıfırlanır (editör görünümü grileşmesin)
        Apply();
    }

    public static void Apply() => Shader.SetGlobalFloat(GreyId, Grey);
    private static void Clear() => Shader.SetGlobalFloat(GreyId, 0f);
    public static float Applied => Shader.GetGlobalFloat(GreyId);
}
