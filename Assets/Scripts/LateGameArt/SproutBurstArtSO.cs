using UnityEngine;

// İstilacı Filiz kişiliği (25 Ekim 2026): Blender'da tasarlanıp dışa aktarılan ortak ağlar ve sabit yerleşim tabloları.
// Kaynak: Tools/Art/SproutBurst (build_sprout_burst.py → export_sprout_burst.py → sprout_burst.json); ağları ve bu asset'i
// Tools/Verification/Editor/SproutBurstArtBuilder.cs üretir. Sunum: LateGamePresenter.SproutBurst.cs.
// - Hücre uzayı: kök hücrenin toprak üstü = 0 (x, y yukarı, z). Kartlar kart uzayında (x ekranda sağ, y yukarı, z bakış).
// - Işıklı ağlar iki alt ağlı: 0 toon gövde (lit), 1 Blender'dan gelen mürekkep kabuğu (ink). Renkler doğrusal.
[CreateAssetMenu(menuName = "ClickerGame/Late Game/Sprout Burst Art", fileName = "SproutBurstArt")]
public sealed class SproutBurstArtSO : ScriptableObject
{
    public const string ResourcePath = "SproutBurstArt";
    public const int Tendrils = 4, TendrilSegments = 9, VineSegments = 12;

    [Header("Malzemeler")]
    public Material lit;
    public Material ink;

    [Header("Yer (hücre uzayı, ışıksız)")]
    public Mesh[] cracks = new Mesh[0];

    [Header("Işıklı ağlar (gövde + kabuk)")]
    public Mesh[] tendrils = new Mesh[0];        // k * TendrilSegments + i, parça başına göre
    public Vector3[] tendrilStarts = new Vector3[0];
    public Vector3[] tendrilRoots = new Vector3[0];
    public Vector2[] tendrilDirections = new Vector2[0];
    public Mesh[] vine = new Mesh[0];            // +X yönünde 2 birim, parça başına göre
    public Vector3[] vineStarts = new Vector3[0];
    public Mesh drop;
    public Mesh shred;

    [Header("Kartlar (köşe rengi)")]
    public Mesh[] puffs = new Mesh[0];
    public Mesh[] pollen = new Mesh[0];
    public Mesh splat;

    public bool Valid => lit != null && ink != null && cracks.Length > 0 && puffs.Length > 0 && pollen.Length > 0 && splat != null && drop != null && shred != null &&
                         tendrils.Length == Tendrils * TendrilSegments && tendrilStarts.Length == tendrils.Length && tendrilRoots.Length == Tendrils &&
                         tendrilDirections.Length == Tendrils && vine.Length == VineSegments && vineStarts.Length == VineSegments;
}
