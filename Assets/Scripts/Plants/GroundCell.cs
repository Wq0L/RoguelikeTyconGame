using System.Collections.Generic;
using UnityEngine;

public class GroundCell : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    [Header("References")]
    [SerializeField] private Renderer groundRenderer;

    [Header("Materials")]
    [SerializeField] private Material lockedMaterial;
    [SerializeField] private Material[] unlockedMaterials;

    private GridPosition gridPosition;
    private GridObject gridObject;
    private bool isLocked = true;
    private TileModifierSO currentModifier;
    private List<StatModifier> rolledModifiers = new();
    private MaterialPropertyBlock mpb;

    public Renderer GroundRenderer => groundRenderer;
    public bool IsLocked => isLocked;
    public TileModifierSO CurrentModifier => currentModifier;
    public List<StatModifier> RolledModifiers => rolledModifiers;
    public PlanterBrain Planter => gridObject?.GetPlanterBrain();

    private void Awake()
    {
        mpb = new MaterialPropertyBlock();
        Lock();
    }

    public void SetGridPosition(GridPosition gridPosition) => this.gridPosition = gridPosition;
    public GridPosition GetGridPosition() => gridPosition;
    public void SetGridObject(GridObject obj) => gridObject = obj;

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

        Material[] materials = groundRenderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
            materials[i] = lockedMaterial;

        groundRenderer.sharedMaterials = materials;

    }

    // Tile seviyesi: grid dolunca kartlar tile'ları yükseltir. Her seviye tile'ın zar değerine +%25 (Sv 3 = +%75).
    public const int MaxLevel = 3;
    public const float LevelBonus = .25f;
    private int level;
    public int Level => level;
    public bool CanUpgrade => !isLocked && currentModifier != null && level < MaxLevel;
    public static int LevelsFor(TileRarity rarity) => rarity == TileRarity.Legendary ? 3 : rarity == TileRarity.Epic ? 2 : 1;

    // Yükseltme sonrası değerler; kart önizlemesi için, tile'a dokunmaz.
    public List<StatModifier> PreviewLevels(int add) => PreviewLevelsFrom(level, Mathf.Min(MaxLevel, level + Mathf.Max(0, add)));

    // Kazanılan seviye sayısını döner (max'ta 0).
    public int AddLevels(int add)
    {
        if (!CanUpgrade || add <= 0) return 0;
        int before = level;
        level = Mathf.Min(MaxLevel, level + add);
        rolledModifiers = PreviewLevelsFrom(before, level);
        Planter?.RefreshTileBuffs();
        return level - before;
    }

    private List<StatModifier> PreviewLevelsFrom(int from, int to)
    {
        float factor = LevelFactor(from, to);
        var result = new List<StatModifier>(rolledModifiers);
        for (int i = 0; i < result.Count; i++) { var mod = result[i]; mod.value *= factor; result[i] = mod; }
        return result;
    }

    private static float LevelFactor(int from, int to) => (1f + LevelBonus * to) / (1f + LevelBonus * from);

    public void ApplyModifier(TileModifierSO modifier, IReadOnlyList<StatModifier> offeredModifiers = null)
    {
        currentModifier = modifier;
        level = 0;
        rolledModifiers = modifier == null ? new List<StatModifier>() :
            offeredModifiers != null ? new List<StatModifier>(offeredModifiers) : modifier.RollModifiers();

        // foreach (var mod in rolledModifiers)
        //     Debug.Log($"[TILE] {mod.statType} = {mod.value} ({mod.operation})");

        if (groundRenderer != null)
        {
            groundRenderer.sharedMaterials = unlockedMaterials;
            mpb.Clear();
            if (modifier != null) mpb.SetColor(ColorId, modifier.tileColor);
            groundRenderer.SetPropertyBlock(mpb);
        }

        Planter?.RefreshTileBuffs();
    }

    private void ApplyMaterials(Material[] newMaterials)
    {
        Material[] materials = groundRenderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
            materials[i] = newMaterials[i % newMaterials.Length];

        groundRenderer.sharedMaterials = materials;
    }
}
