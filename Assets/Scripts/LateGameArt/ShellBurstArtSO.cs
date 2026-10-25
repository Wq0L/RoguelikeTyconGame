using UnityEngine;

// Aşılı Kabuk kişiliği (25 Ekim 2026, "aşı damarlı zırh tohumu"): Blender'da tasarlanıp dışa aktarılan ortak ağlar ve sabit
// yerleşim tabloları. Kaynak: Tools/Art/ShellBurst (build_shell_burst.py → export_shell_burst.py → shell_burst.json); ağları ve bu
// asset'i Tools/Verification/Editor/ShellBurstArtBuilder.cs üretir. Sunum: LateGamePresenter.ShellBurst.cs.
// - Hücre uzayı: hücre yarı boyu 1 (oyunda ölçek = hücre / 2), x, y yukarı, z. Kartlar kart uzayında (x ekranda sağ, y yukarı,
//   z bakış). Bölge dizini: 0 = A (amber), 1 = B (mor).
// - Işıklı ağlar iki alt ağlı: 0 toon gövde (lit), 1 Blender'dan gelen mürekkep kabuğu (ink). Düz ağlar (çatlak, motif, ışık,
//   kart) tek alt ağlı, köşe renkli ışıksız. Renkler doğrusal.
[CreateAssetMenu(menuName = "ClickerGame/Late Game/Shell Burst Art", fileName = "ShellBurstArt")]
public sealed class ShellBurstArtSO : ScriptableObject
{
    public const string ResourcePath = "ShellBurstArt";
    public const int Plates = 5, CrackStages = 3, Slabs = 6, CollarPlates = 6, Crust = 5;

    [Header("Malzemeler")]
    public Material lit;
    public Material ink;

    [Header("Düğüm (bölge × …; düğüm uzayı, kök = toprak üstü)")]
    public Mesh[] nodeBody = new Mesh[2];       // beş lob plaka + topuz (ışıklı)
    public Mesh[] nodeCore = new Mesh[2];       // çekirdek (gövde ışıksız + mürekkep)
    public Mesh[] nodeMotif = new Mesh[2];      // A üçgen / B baklava (düz)
    public Mesh[] nodeCracks = new Mesh[6];     // bölge * 3 + aşama (düz)
    public Mesh[] nodePlates = new Mesh[10];    // bölge * 5 + k, menteşe uzayında (açılış); plaka 0 motifi taşır
    public Vector3[] plateHinges = new Vector3[10];
    public Vector3[] plateDirections = new Vector3[10];
    public Mesh[] nodeKnob = new Mesh[2];
    public Vector3[] knobRest = new Vector3[2];
    public Vector3[] nodeSize = new Vector3[2];  // (rx, rz, cz)

    [Header("Doğum plakaları (düğüm hücresi)")]
    public Mesh[] birthGlow = new Mesh[2];
    public Mesh birthCenter;
    public Mesh[] birthSlabs = new Mesh[6];
    public Vector3[] slabHinges = new Vector3[6];
    public Vector3[] slabDirections = new Vector3[6];

    [Header("Aşı damarı, kelepçe, boncuk (bölge başına)")]
    public Mesh[] veinSegment = new Mesh[2];    // +Z boyunca 1 birim
    public Mesh[] clamp = new Mesh[2];          // +X'e bakan, kendi merkezinde
    public Mesh[] bead = new Mesh[2];
    public Vector3 clampCenter;

    [Header("Kabuk yaka (kapanan hücre)")]
    public Mesh[] collarPlates = new Mesh[6];   // menteşe uzayında
    public Vector3[] collarHinges = new Vector3[6];
    public Vector3[] collarDirections = new Vector3[6];
    public Mesh[] collarCracks = new Mesh[12];  // bölge * 6 + k (yalnız öndeki plakalarda; diğerleri boş)
    public Mesh[] collarBand = new Mesh[2];
    public Mesh[] crust = new Mesh[5];
    public Vector3[] crustCenters = new Vector3[5];

    [Header("Parçalar ve kartlar")]
    public Mesh chip;
    public Mesh shock;
    public Mesh[] dust = new Mesh[3];
    public Mesh[] tink = new Mesh[2];
    public Mesh[] spark = new Mesh[2];
    public Mesh sparkCream;

    private static bool All(Mesh[] meshes, int count)
    {
        if (meshes == null || meshes.Length != count) return false;
        foreach (Mesh m in meshes) if (m == null) return false;
        return true;
    }

    public bool Valid => lit != null && ink != null && All(nodeBody, 2) && All(nodeCore, 2) && All(nodeMotif, 2) && All(nodeCracks, 6) &&
                         All(nodePlates, 10) && plateHinges.Length == 10 && plateDirections.Length == 10 && All(nodeKnob, 2) && knobRest.Length == 2 &&
                         nodeSize.Length == 2 && All(birthGlow, 2) && birthCenter != null && All(birthSlabs, Slabs) && slabHinges.Length == Slabs &&
                         slabDirections.Length == Slabs && All(veinSegment, 2) && All(clamp, 2) && All(bead, 2) && All(collarPlates, CollarPlates) &&
                         collarHinges.Length == CollarPlates && collarDirections.Length == CollarPlates && collarCracks.Length == 12 && All(collarBand, 2) &&
                         All(crust, Crust) && crustCenters.Length == Crust && chip != null && shock != null && All(dust, 3) && All(tink, 2) && All(spark, 2) &&
                         sparkCream != null;
}
