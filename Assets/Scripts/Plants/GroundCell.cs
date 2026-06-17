using UnityEngine;

public class GroundCell : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Renderer groundRenderer;

    [Header("Materials")]
    [SerializeField] private Material lockedMaterial;
    [SerializeField] private Material[] unlockedMaterials;

    private GridPosition gridPosition;
    private GridObject gridObject;
    private bool isLocked = true;
    private TileModifierSO currentModifier;

    public bool IsLocked => isLocked;
    public TileModifierSO CurrentModifier => currentModifier;

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

    public void SetGridObject(GridObject obj)
    {
        gridObject = obj;
    }

    public void Unlock()
    {
        if (groundRenderer == null || unlockedMaterials == null || unlockedMaterials.Length == 0)
        {
            Debug.LogWarning($"{name}: Unlock için material eksik.");
            return;
        }

        isLocked = false;
        ApplyMaterials(unlockedMaterials);
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
            materials[i] = lockedMaterial;

        groundRenderer.materials = materials;
    }

    public void ApplyModifier(TileModifierSO modifier)
    {
        currentModifier = modifier;

        Material[] materials = groundRenderer.materials;
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = new Material(unlockedMaterials[i % unlockedMaterials.Length]);
            materials[i].color = modifier.tileColor;
        }
        groundRenderer.materials = materials;

        // Planter varsa haber ver
        gridObject?.GetPlanterBrain()?.ApplyBuff(modifier);
    }

    private void ApplyMaterials(Material[] newMaterials)
    {
        Material[] materials = groundRenderer.materials;
        for (int i = 0; i < materials.Length; i++)
            materials[i] = newMaterials[i % newMaterials.Length];

        groundRenderer.materials = materials;
    }
}