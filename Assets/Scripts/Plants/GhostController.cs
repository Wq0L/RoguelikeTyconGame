using UnityEngine;

public class GhostController : MonoBehaviour
{
    [Tooltip("Saksinin gorsel objesi. Bos birakilirsa ana obje kullanilir.")]
    [SerializeField] private GameObject realModel;
    [HideInInspector, SerializeField] private GameObject ghostModel;
    [SerializeField] private Material validMaterial;
    [SerializeField] private Material invalidMaterial;

    private Renderer[] renderers;
    private Material[][] originalMaterials;
    private Collider[] colliders;
    private bool[] originalColliderStates;
    private bool isGhost;
    private bool? lastValid;

    private void CacheVisuals()
    {
        if (renderers != null) return;
        if (ghostModel != null && ghostModel != realModel && ghostModel != gameObject)
            ghostModel.SetActive(false);
        GameObject visual = realModel != null ? realModel : gameObject;
        visual.SetActive(true);
        renderers = visual.GetComponentsInChildren<Renderer>(false);
        originalMaterials = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++)
            originalMaterials[i] = renderers[i].sharedMaterials;
        colliders = GetComponentsInChildren<Collider>(true);
        originalColliderStates = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
            originalColliderStates[i] = colliders[i].enabled;
    }

    public void SetColor(bool isValid)
    {
        if (!isGhost || lastValid == isValid) return;
        Material mat = isValid ? validMaterial : invalidMaterial;
        if (mat == null) return;
        lastValid = isValid;
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materials = new Material[originalMaterials[i].Length];
            for (int j = 0; j < materials.Length; j++) materials[j] = mat;
            renderers[i].sharedMaterials = materials;
        }
    }

    public void SetGhostMode(bool isGhost)
    {
        CacheVisuals();
        this.isGhost = isGhost;
        lastValid = null;
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = isGhost ? false : originalColliderStates[i];
        if (isGhost)
            SetColor(false);
        else
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterials = originalMaterials[i];
    }
}
