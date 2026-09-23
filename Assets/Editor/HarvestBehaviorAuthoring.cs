using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Explicit authoring only: never runs on import or enters play mode automatically.
public static class HarvestBehaviorAuthoring
{
    const string Folder = "Assets/Art/Behaviors";
    static void Field(Object obj, string name, Object value)
    { var s = new SerializedObject(obj); s.FindProperty(name).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    static Material EffectMaterial(string name, bool overlay)
    {
        string path = Folder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("ClickerGame/Harvest Effect")); AssetDatabase.CreateAsset(m, path); }
        m.SetFloat("_ZTest", overlay ? 8 : 4); m.renderQueue = overlay ? 3990 : 3100; EditorUtility.SetDirty(m); return m;
    }
    static Material ToonMaterial(string name, Color color, bool overlay)
    {
        string path = Folder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/GameToon/Planter_e40d91d8cb0832b44a1207d2f059956e_2100000.mat"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetTexture("_MainTex", null); m.SetColor("_Color", color); m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_OtlWorldWidth", .012f); m.SetFloat("_MinLight", .5f);
        m.SetFloat("_ToonZTest", overlay ? 8 : 4); m.SetFloat("_ToonZWrite", overlay ? 0 : 1);
        m.renderQueue = overlay ? 3991 : 2000;
        if (overlay) foreach (string pass in new[] { "ShadowCaster", "DepthOnly", "DepthNormals" }) m.SetShaderPassEnabled(pass, false);
        EditorUtility.SetDirty(m); return m;
    }
    static GameObject Scythe(Transform parent, bool overlay, float scale)
    {
        var imported = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SciFiScythe.fbx");
        if (imported == null) throw new Exception("Blender FBX missing");
        var model = Object.Instantiate(imported, parent); model.name = "Sci-fi Scythe";
        model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity; model.transform.localScale = Vector3.one * scale;
        foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                string key = materials[i] != null ? materials[i].name : "Gunmetal";
                Color color = key.Contains("Plasma") ? new Color(.035f, .9f, .78f) : key.Contains("Ceramic") ? new Color(.8f, .9f, .92f) :
                    key.Contains("Amber") ? new Color(1f, .5f, .07f) : new Color(.035f, .065f, .1f);
                materials[i] = ToonMaterial(key + (overlay ? " Cursor" : " Toon"), color, overlay);
            }
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = !overlay;
        }
        return model;
    }
    [MenuItem("Tools/Harvest Behaviors/Author Approved Assets")]
    public static void Author()
    {
        var effect = EffectMaterial("Electric and Scythe Trail", false);
        var overlay = EffectMaterial("Harvest Cursor Overlay", true);
        var bo = new GameObject("Bumerang Orak");
        var bc = bo.AddComponent<BoomerangScythe>(); var bv = Scythe(bo.transform, false, .65f);
        var trail = bo.AddComponent<TrailRenderer>(); trail.sharedMaterial = effect; trail.time = .2f;
        trail.startWidth = .23f; trail.endWidth = 0; trail.minVertexDistance = .08f; trail.emitting = false;
        trail.startColor = new Color(.2f, 1f, .8f, .6f); trail.endColor = new Color(.1f, .7f, 1f, 0);
        Field(bc, "visual", bv.transform); Field(bc, "trail", trail);
        var bp = PrefabUtility.SaveAsPrefabAsset(bo, Folder + "/BoomerangScythe.prefab").GetComponent<BoomerangScythe>(); Object.DestroyImmediate(bo);
        var eo = new GameObject("Çapraz Elektrik"); var ec = eo.AddComponent<ElectricBurst>(); Field(ec, "boltMaterial", effect);
        var ep = PrefabUtility.SaveAsPrefabAsset(eo, Folder + "/ElectricBurst.prefab").GetComponent<ElectricBurst>(); Object.DestroyImmediate(eo);
        var co = new GameObject("Harvest cursor wind and scythe"); var cv = co.AddComponent<HarvestCursorVisual>();
        Field(cv, "scythe", Scythe(co.transform, true, .28f).transform); Field(cv, "overlayMaterial", overlay);
        var cp = PrefabUtility.SaveAsPrefabAsset(co, Folder + "/HarvestCursor.prefab").GetComponent<HarvestCursorVisual>(); Object.DestroyImmediate(co);

        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        // Scene replacement can unload asset objects held only by managed locals.
        bp = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/BoomerangScythe.prefab").GetComponent<BoomerangScythe>();
        ep = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/ElectricBurst.prefab").GetComponent<ElectricBurst>();
        cp = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/HarvestCursor.prefab").GetComponent<HarvestCursorVisual>();
        overlay = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Harvest Cursor Overlay.mat");
        var manager = Object.FindFirstObjectByType<HarvestBehaviorManager>(FindObjectsInactive.Include);
        if (manager == null) manager = new GameObject("Harvest Behavior Manager").AddComponent<HarvestBehaviorManager>();
        Field(manager, "boomerangPrefab", bp); Field(manager, "electricPrefab", ep);
        var player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        Field(player, "cursorVisualPrefab", cp); Field(player, "radiusOverlayMaterial", overlay);
        var picker = Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
        var serialized = new SerializedObject(picker); var pool = serialized.FindProperty("allModifiers");
        for (int family = 0; family < 2; family++) for (int rarity = 0; rarity < 4; rarity++)
        {
            string dir = "Assets/ScriptableObjects/GridModifiers/" + (family == 0 ? "Boomerang" : "Electric");
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/ScriptableObjects/GridModifiers", family == 0 ? "Boomerang" : "Electric");
            string path = dir + "/" + (family == 0 ? "Boomerang" : "Electric") + "-" + (TileRarity)rarity + ".asset";
            var card = AssetDatabase.LoadAssetAtPath<TileModifierSO>(path);
            if (card == null) { card = ScriptableObject.CreateInstance<TileModifierSO>(); AssetDatabase.CreateAsset(card, path); }
            card.modifierName = family == 0 ? "Bumerang Orak" : "Çapraz Elektrik";
            card.modifierType = family == 0 ? TileModifierType.Boomerang : TileModifierType.Electric;
            card.behavior = family == 0 ? TileBehavior.Boomerang : TileBehavior.Electric;
            card.requiredUnlock = family == 0 ? UnlockType.TileBehavior_Boomerang : UnlockType.TileBehavior_Electric;
            card.rarity = (TileRarity)rarity;
            card.tileColor = family == 0 ? new Color(.18f, .85f, .65f) : new Color(.15f, .65f, 1f);
            card.modifierRanges.Clear(); card.modifierRanges.Add(new StatModifierRange {
                statType = family == 0 ? StatType.BoomerangChance : StatType.ElectricChance, target = StatTarget.Planter, operation = ModifierOperation.Flat,
                minValue = new[] { .12f, .2f, .3f, .45f }[rarity], maxValue = new[] { .18f, .28f, .4f, .6f }[rarity] });
            EditorUtility.SetDirty(card);
            bool found = false; for (int i = 0; i < pool.arraySize; i++) if (pool.GetArrayElementAtIndex(i).objectReferenceValue == card) found = true;
            if (!found) { pool.InsertArrayElementAtIndex(pool.arraySize); pool.GetArrayElementAtIndex(pool.arraySize - 1).objectReferenceValue = card; }
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
        Debug.Log("Authored sci-fi scythe, pooled behavior prefabs, eight cards, manager and overlay cursor.");
    }
    public static void AuthorBatch()
    {
        try { Author(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
