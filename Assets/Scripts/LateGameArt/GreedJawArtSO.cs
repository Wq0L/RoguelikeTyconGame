using UnityEngine;

// Açgözlü Hasat "aç ağız halkası" (24 Ekim 2026): Blender'da tasarlanıp dışa aktarılan ortak ağlar.
// Kaynak: Tools/Art/GreedJaw (build_greed_jaw.py → export_greed_jaw.py → greed_jaw.json);
// ağları, mürekkep malzemesini ve bu asset'i Tools/Verification/Editor/GreedJawArtBuilder.cs üretir. Sunum: LateGamePresenter.Greed.cs.
// - Işıklı parçalar (diş eti, dişler, mini diş, kırıntı) tek ağda iki alt ağdır: 0 = toon gövde (lit), 1 = mürekkep kabuğu (ink).
//   Kabuk Blender'da Solidify ile üretilip içe bakar; ink malzemesi arka yüzleri ayıklar, yalnız arkadaki kenar görünür.
// - Kartlar (diken, toz) kart uzayındadır (x ekranda sağ, y yukarı, z bakış yönü) ve köşe rengiyle ışıksız çizilir.
// - Çene uzayı: dış yön +Z, halka zemini y = 0; ölçüler designRadius'lu halkaya göredir, sunum gerçek yarıçapla ölçekler.
// Renkler doğrusaldır (projede ağ köşe rengi dönüştürülmez).
[CreateAssetMenu(menuName = "ClickerGame/Late Game/Greed Jaw Art", fileName = "GreedJawArt")]
public sealed class GreedJawArtSO : ScriptableObject
{
    public const string ResourcePath = "GreedJawArt";

    [System.Serializable]
    public struct ToothSlot
    {
        public int kind;            // 0 köpek dişi, 1 normal, 2 küçük
        public Vector3 position;    // çene uzayında taban
        public float yaw;           // Y çevresinde (derece)
        public float scale;
    }

    [System.Serializable]
    public struct ChipSlot
    {
        public int color;           // chips[] sırası
        public Vector2 direction;   // zeminde (x, z)
        public float distance, rise, scale;
    }

    [Header("Malzemeler")]
    public Material lit;
    public Material ink;

    [Header("Işıklı ağlar (gövde + kabuk)")]
    public Mesh gumUpper;
    public Mesh gumLower;
    public Mesh[] teethUpper = new Mesh[0];
    public Mesh[] teethLower = new Mesh[0];
    public Mesh mini;
    public Mesh[] chips = new Mesh[0];

    [Header("Kartlar (köşe rengi)")]
    public Mesh spike;
    public Mesh puff;

    [Header("Yerleşim (Blender)")]
    public ToothSlot[] upper = new ToothSlot[0];
    public ToothSlot[] lower = new ToothSlot[0];
    public ChipSlot[] chipSlots = new ChipSlot[0];
    public float designRadius = 1.5f;
    public float miniRadius = 1.7f;

    public bool Valid => lit != null && ink != null && gumUpper != null && gumLower != null && mini != null && spike != null && puff != null &&
                         teethUpper.Length == 3 && teethLower.Length == 3 && chips.Length > 0 && upper.Length > 0 && lower.Length > 0 &&
                         chipSlots.Length > 0 && designRadius > 0f;
}
