using UnityEngine;

public class GroundCell : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Renderer groundRenderer;

    [Header("Materials")]
    [SerializeField] private Material lockedMaterial;
    [SerializeField] private Material[] unlockedMaterials;

    private GridPosition gridPosition;
    private bool isLocked = true;

    public bool IsLocked => isLocked;

    private void Awake()
    {
        Lock();
    }

    public void SetGridPosition(GridPosition gridPosition)
    {
        this.gridPosition = gridPosition;
    }

    public GridPosition GetGridPosition()
    {
        return gridPosition;
    }

    public void Unlock()
    {
        if (groundRenderer == null || unlockedMaterials == null || unlockedMaterials.Length == 0)
        {
            Debug.LogWarning($"{name}: Unlock için material eksik.");
            return;
        }

        isLocked = false;

        Material[] materials = groundRenderer.materials;

        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = unlockedMaterials[i % unlockedMaterials.Length];
        }

        groundRenderer.materials = materials;
    }

    public void Lock()
    {
        if (groundRenderer == null || lockedMaterial == null)
        {
            Debug.LogWarning($"{name}: Lock için material eksik.");
            return;
        }

        isLocked = true;

        Material[] materials = groundRenderer.materials;

        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = lockedMaterial;
        }

        groundRenderer.materials = materials;
    }
}