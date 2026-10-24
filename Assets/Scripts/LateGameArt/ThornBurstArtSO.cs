using UnityEngine;

// Diken Kuşatması çıkış patlaması (24 Ekim 2026): Blender'da tasarlanıp dışa aktarılan ortak ağlar ve sabit uçuş tabloları.
// Kaynak: Tools/Art/ThornBurst (build_thorn_burst.py → export_thorn_burst.py → thorn_burst.json); ağları ve bu asset'i
// Tools/Verification/Editor/ThornBurstArtBuilder.cs üretir. Sunum: LateGamePresenter.ThornBurst.cs.
// - Kartlar (patlama, hız çizgileri, toz bulutları) kart uzayında (x ekranda sağ, y yukarı, z bakış), köşe rengiyle ışıksız.
// - Işıklı ağlar (kesekler, krater toprağı) iki alt ağlı: 0 toon gövde (lit), 1 Blender'dan gelen mürekkep kabuğu (ink).
// - Uçuş tabloları oyunda Random kullanılmasın diye sabit; renkler doğrusal (projede ağ köşe rengi dönüştürülmez).
[CreateAssetMenu(menuName = "ClickerGame/Late Game/Thorn Burst Art", fileName = "ThornBurstArt")]
public sealed class ThornBurstArtSO : ScriptableObject
{
    public const string ResourcePath = "ThornBurstArt";

    [System.Serializable]
    public struct ClodFlight
    {
        public int variant;
        public Vector2 direction;   // zeminde (x, z)
        public float distance, rise, spin, size;
    }

    [System.Serializable]
    public struct CloudPuff
    {
        public int variant;
        public Vector2 screen;      // ekranda (sağ, yukarı) yön
        public float reach, rise, delay, size;
    }

    [Header("Malzemeler")]
    public Material lit;
    public Material ink;

    [Header("Kartlar (köşe rengi)")]
    public Mesh blast;
    public Mesh lines;
    public Mesh[] clouds = new Mesh[0];
    public Mesh craterScorch;

    [Header("Işıklı ağlar (gövde + kabuk)")]
    public Mesh[] clods = new Mesh[0];
    public Mesh craterLit;

    [Header("Sabit uçuş tabloları (Blender)")]
    public ClodFlight[] clodFlights = new ClodFlight[0];
    public CloudPuff[] cloudPuffs = new CloudPuff[0];

    public bool Valid => lit != null && ink != null && blast != null && lines != null && craterScorch != null && craterLit != null &&
                         clouds.Length > 0 && clods.Length > 0 && clodFlights.Length > 0 && cloudPuffs.Length > 0;
}
