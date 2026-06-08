using UnityEngine;

public class GhostController : MonoBehaviour
{
    [SerializeField] private GameObject realModel;
    [SerializeField] private GameObject ghostModel;
    [SerializeField] private Renderer ghostRenderer;
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;

    public void SetColor(bool isValid)
    {
        Material mat = isValid ? validMaterial : invalidMaterial;
        
        Material[] materials = new Material[ghostRenderer.materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = mat;
        }
        
        ghostRenderer.materials = materials;
    }

    public void SetGhostMode(bool isGhost)
    {
        realModel.SetActive(!isGhost);
        ghostModel.SetActive(isGhost);
    }
}