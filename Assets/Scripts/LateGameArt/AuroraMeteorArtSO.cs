using UnityEngine;

// Hasat Yankısı kuzey ışığı meteoru (23 Ekim 2026): Blender'da tasarlanıp dışa aktarılan ağlar.
// Kaynak: Tools/Art/AuroraMeteor (build_aurora_meteor.py → export_aurora_meteor.py → aurora_meteor.json);
// ağları ve bu asset'i Tools/Verification/Editor/AuroraMeteorArtBuilder.cs üretir. Sunum: LateGamePresenter.Meteor.cs.
// - Işıklı parçalar (taş, enkaz, krater kenar blokları) mevcut toon gövde + mürekkep kabuk malzemeleriyle çizilir.
// - Işıksız parçalar (çatlak, alev izi, çarpma tacı, toz bulutları, krater zemini) köşe rengiyle çizilir.
// - Kartlar (iz, taç, bulut) kart uzayındadır: y = ekrandaki yön, z = kamera bakış yönü; sunum her karede kameraya çevirir.
// Renkler doğrusaldır (projede ağ köşe rengi dönüştürülmez).
[CreateAssetMenu(menuName = "ClickerGame/Late Game/Aurora Meteor Art", fileName = "AuroraMeteorArt")]
public sealed class AuroraMeteorArtSO : ScriptableObject
{
    public const string ResourcePath = "AuroraMeteorArt";

    [Header("Işıklı toon: gövde + mürekkep kabuk")]
    public Material lit;
    public Material ink;
    public Mesh rock;
    public Mesh[] stones = new Mesh[0];
    public Mesh[] rims = new Mesh[0];
    public Vector3[] rimPositions = new Vector3[0];

    [Header("Işıksız köşe rengi")]
    public Mesh rockCracks;
    public Mesh trail;
    public Mesh crown;
    public Mesh craterGround;
    public Mesh[] clouds = new Mesh[0];

    [Header("Tasarım ölçüsü (Blender)")]
    public float cell = 2f;
    public float rockRadius = .6f;

    public bool Valid => lit != null && ink != null && rock != null && rockCracks != null && trail != null && crown != null &&
                         craterGround != null && clouds.Length > 0 && stones.Length > 0 && rims.Length > 0 && rims.Length == rimPositions.Length;
}
