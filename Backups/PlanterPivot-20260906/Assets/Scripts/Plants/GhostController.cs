using UnityEngine;

public class GhostController : MonoBehaviour
{
    [SerializeField] private GameObject realModel;
    [SerializeField] private GameObject ghostModel;
    [SerializeField] private Renderer ghostRenderer;
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;

    private bool? lastValid; // henüz set edilmedi diye null

    public void SetColor(bool isValid)
    {
        if (lastValid == isValid) return; // değişmediyse dokunma
        lastValid = isValid;

        Material mat = isValid ? validMaterial : invalidMaterial;
        Material[] materials = new Material[ghostRenderer.sharedMaterials.Length];
        for (int i = 0; i < materials.Length; i++)
            materials[i] = mat;

        ghostRenderer.sharedMaterials = materials;
    }

    public void SetGhostMode(bool isGhost)
    {
        realModel.SetActive(!isGhost);
        ghostModel.SetActive(isGhost);
        lastValid = null; // yeni ghost başlarken sıfırla, ilk SetColor kesin çalışsın
    }
}