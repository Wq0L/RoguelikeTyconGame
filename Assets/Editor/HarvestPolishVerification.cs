using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class HarvestPolishVerification
{
    [MenuItem("Tools/Resonance/Verify HP Cards and Unlock")]
    public static void Run()
    {
        var cleanup = new List<Object>();
        try
        {
            var hp = Resources.Load<PlantHealthScalingSO>("PlantHealthScaling");
            Require(hp != null, "HP config");
            var plant = ScriptableObject.CreateInstance<PlantSO>(); cleanup.Add(plant);
            plant.maxHealth = 10;
            plant.rarity = PlantRarity.Common;
            Require(hp.Calculate(plant, 1) == 5 && hp.Calculate(plant, 130) == 180000, "Common endpoints");
            plant.rarity = PlantRarity.Uncommon;
            Require(hp.Calculate(plant, 130) == 216000, "Uncommon rounding");
            plant.rarity = PlantRarity.Rare;
            Require(hp.Calculate(plant, 130) == 288000, "Rare rounding");
            plant.rarity = PlantRarity.Legendary;
            Require(hp.Calculate(plant, 1) == 15 && hp.Calculate(plant, 130) == 540000, "Legendary endpoints");
            int previous = 0;
            for (int r = 1; r <= 130; r++) { int current = hp.Calculate(plant, r); Require(current >= previous, "Monotonic HP"); previous = current; }
            plant.maxHealth = 20;
            Require(hp.Calculate(plant, 130) == 1080000, "Per-plant authored tuning");
            Require(hp.Calculate(plant, 200) == hp.Calculate(plant, 130), "After last anchor clamp");

            var water = AssetDatabase.LoadAssetAtPath<TileModifierSO>("Assets/ScriptableObjects/GridModifiers/Water/Water-Common.asset");
            var offer = new TileCardOffer(water);
            var cellObject = new GameObject("PolishCheck_Cell"); cleanup.Add(cellObject);
            var cell = cellObject.AddComponent<GroundCell>();
            cell.ApplyModifier(water, offer.Modifiers);
            Require(cell.RolledModifiers.Count == offer.Modifiers.Count, "Offer modifier count");
            Require(cell.RolledModifiers[0].value == offer.Modifiers[0].value, "Displayed roll equals applied roll");
            string displayed = TileBuffText.Modifiers(offer.Modifiers);
            Require(displayed.Contains("%") && displayed.Contains("XP"), "Percentage card text");
            Require(TileBuffText.Modifier(new StatModifier { statType=StatType.RareSpawnChance,
                operation=ModifierOperation.Flat,value=15 }).Contains("15% puan"), "Crystal units");
            var tier2 = new ActiveResonance { tileType=TileModifierType.Damage, statType=StatType.PlanterDamageMultiplier, requiredCount=2, multiplier=2 };
            var tier3 = new ActiveResonance { tileType=TileModifierType.Damage, statType=StatType.PlanterDamageMultiplier, requiredCount=3, multiplier=4 };
            Require(ResonanceManager.IsNewTier(tier2, new List<ActiveResonance>()), "Placement unlock");
            Require(!ResonanceManager.IsNewTier(tier2, new List<ActiveResonance>{tier2}), "No duplicate unlock");
            Require(ResonanceManager.IsNewTier(tier3, new List<ActiveResonance>{tier2}), "Tier upgrade unlock");
            Require(!ResonanceManager.IsNewTier(tier2, new List<ActiveResonance>{tier3}), "No downgrade unlock");
            Require(TileBuffText.Resonance(tier2).Contains("+100%"), "Resonance percentage");
            var mat = Resources.Load<Material>("ResonanceBurst");
            Require(mat != null && !ShaderUtil.ShaderHasError(mat.shader), "Burst shader");
            var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Grid Mutation Card.prefab");
            var card = Object.Instantiate(cardPrefab); cleanup.Add(card);
            card.GetComponent<CardUI>().Setup(offer, _ => {});
            var buff = card.transform.Find("Buff Value").GetComponent<TextMeshProUGUI>();
            Require(buff.text == displayed && !buff.raycastTarget, "Serialized card label");
            RenderCard(cardPrefab, offer);
            RenderBurst(mat);
            Debug.Log("HARVEST_POLISH_VERIFICATION_PASS");
        }
        finally { for (int i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); }
    }

    private static void RenderCard(GameObject prefab, TileCardOffer offer)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var cameraGO = new GameObject("Preview Camera"); SceneManager.MoveGameObjectToScene(cameraGO, scene);
            var camera = cameraGO.AddComponent<Camera>();
            camera.scene = scene;
            camera.orthographic = true; camera.orthographicSize = 300;
            camera.transform.position = new Vector3(0,0,-100);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f,.035f,.055f);
            var canvasGO = new GameObject("Preview Canvas", typeof(RectTransform), typeof(Canvas)); SceneManager.MoveGameObjectToScene(canvasGO, scene);
            var canvas = canvasGO.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            var card = Object.Instantiate(prefab, canvas.transform); card.transform.localPosition = Vector3.zero;
            card.GetComponent<CardUI>().Setup(offer, _ => {});
            Canvas.ForceUpdateCanvases();
            foreach(var text in card.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            SaveRender(camera, 460, 640, "Temp/ResonanceChecks/card-preview.png");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void RenderBurst(Material material)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var cameraGO = new GameObject("Preview Camera"); SceneManager.MoveGameObjectToScene(cameraGO, scene);
            var camera = cameraGO.AddComponent<Camera>(); camera.scene = scene;
            camera.orthographic = true; camera.orthographicSize = 3f;
            camera.transform.position = new Vector3(3,4,-6); camera.transform.LookAt(Vector3.up*.6f);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f,.035f,.055f);
            var effectGO = new GameObject("Burst Preview"); SceneManager.MoveGameObjectToScene(effectGO, scene);
            var effect = effectGO.AddComponent<ResonanceBurst>(); effect.Configure(material);
            effect.Play(new Bounds(Vector3.zero,new Vector3(1,0,2)),new Color(1,.65f,.2f),"ODAK REZONANSI\nHasar +100%");
            effect.SendMessage("Draw", .22f);
            effect.GetComponentInChildren<ParticleSystem>().Simulate(.15f,true,false);
            var label = effect.GetComponentInChildren<TextMeshPro>(); label.transform.rotation=camera.transform.rotation; label.ForceMeshUpdate();
            SaveRender(camera, 640, 440, "Temp/ResonanceChecks/burst-preview.png");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void SaveRender(Camera camera, int width, int height, string path)
    {
        var rt = new RenderTexture(width,height,24); var image = new Texture2D(width,height,TextureFormat.RGB24,false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally { camera.targetTexture=null; RenderTexture.active=previous; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image); }
    }
    private static void Require(bool valid,string message) { if(!valid) throw new Exception("Polish verification: "+message); }
}
