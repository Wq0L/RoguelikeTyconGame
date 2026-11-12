using UnityEditor;
using UnityEngine;

// Hasat imlecinin okunurluğu (11 Kasım 2026; kullanıcı: "kullanıcının imleci görünmüyor, oluşan bir çok şey yüzünden kayboluyor").
// Boyalı zemin + mürekkep geçişinde beyaz yarıçap halkası krem karoda kayboluyordu. HarvestCursor prefabına iki alt nesne ekler:
//  - "Ring Ink": beyaz halkanın (PlayerController.radiusIndicator, sortingOrder 100) altında daha kalın koyu mürekkep halka (99);
//    beyaz çizgi her zeminde koyu kenarla okunur (beyaz, krem, renkli değiştirici, koyu kapalı karo).
//  - "Ground Fill": saldırı alanında hafif koyu disk (98, derinlik testli: bitkiler önünde kalır).
// Yönetilen öğe kuralı: yoksa ekler, varsa dokunmaz (prefabda elle yapılan ayar ezilmez); iki kez çalıştırmak çoğaltmaz.
// Renk / kalınlık / yükseklik prefabda ayarlanır; HarvestCursorVisual yalnız yarıçapa göre yerleştirir.
// Toplu: Unity -batchmode -projectPath <kök> -executeMethod HarvestCursorReadability.RunBatch
public static class HarvestCursorReadability
{
    public const string PrefabPath = "Assets/Art/Behaviors/HarvestCursor.prefab";
    public const string FillMaterialPath = "Assets/Art/Behaviors/Harvest Cursor Fill.mat";
    public const string DiscMeshPath = "Assets/Art/Behaviors/HarvestCursorDisc.asset";
    public const string InkName = "Ring Ink", FillName = "Ground Fill";
    public static readonly Color Ink = new(.08f, .05f, .10f, .95f);
    static readonly Color FillCenter = new(.08f, .05f, .10f, .16f), FillEdge = new(.08f, .05f, .10f, .34f);
    const float InkWidth = .16f, InkLocalY = -.07f, FillLocalY = -.105f;   // görsel merkez +0,12'de; beyaz halka merkez +0,05'te
    const int InkSegments = 64, DiscSegments = 48;

    [MenuItem("Tools/Oyun/Hasat İmleci: Okunurluk (mürekkep halka + zemin)")]
    public static void FromMenu() => Debug.Log(Apply());

    public static void RunBatch()
    {
        int code = 0;
        try { Debug.Log(Apply()); }
        catch (System.Exception e) { Debug.LogException(e); code = 1; }
        EditorApplication.Exit(code);
    }

    public static string Apply()
    {
        Mesh disc = AssetDatabase.LoadAssetAtPath<Mesh>(DiscMeshPath);
        if (disc == null) { disc = BuildDisc(); AssetDatabase.CreateAsset(disc, DiscMeshPath); }
        var fillMaterial = AssetDatabase.LoadAssetAtPath<Material>(FillMaterialPath);
        if (fillMaterial == null)
        {
            fillMaterial = new Material(Shader.Find("ClickerGame/Harvest Effect")) { name = "Harvest Cursor Fill" };
            fillMaterial.SetFloat("_ZTest", 4f);   // LessEqual: bitkiler ve saksılar diskin önünde kalır
            AssetDatabase.CreateAsset(fillMaterial, FillMaterialPath);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var visual = root.GetComponent<HarvestCursorVisual>();
            if (visual == null) throw new System.Exception("HarvestCursorVisual not found on " + PrefabPath);
            var so = new SerializedObject(visual);
            var overlay = so.FindProperty("overlayMaterial").objectReferenceValue as Material;
            int added = 0;

            Transform ink = root.transform.Find(InkName);
            if (ink == null)
            {
                ink = new GameObject(InkName).transform; ink.SetParent(root.transform, false);
                ink.localPosition = new Vector3(0f, InkLocalY, 0f);
                var line = ink.gameObject.AddComponent<LineRenderer>();
                line.sharedMaterial = overlay; line.loop = true; line.useWorldSpace = false;
                line.widthMultiplier = InkWidth; line.startColor = line.endColor = Ink;
                line.sortingOrder = 99; line.numCornerVertices = 0; line.numCapVertices = 0;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
                line.positionCount = InkSegments;
                for (int i = 0; i < InkSegments; i++)   // editörde görünsün diye 1 birim yarıçapla; oyunda gerçek yarıçapa oturur
                {
                    float a = i * Mathf.PI * 2f / InkSegments;
                    line.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
                }
                added++;
            }

            Transform fill = root.transform.Find(FillName);
            if (fill == null)
            {
                fill = new GameObject(FillName).transform; fill.SetParent(root.transform, false);
                fill.localPosition = new Vector3(0f, FillLocalY, 0f); fill.localScale = new Vector3(2f, 1f, 2f);
                fill.gameObject.AddComponent<MeshFilter>().sharedMesh = disc;
                var r = fill.gameObject.AddComponent<MeshRenderer>();
                r.sharedMaterial = fillMaterial; r.sortingOrder = 98;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                added++;
            }

            var inkProp = so.FindProperty("inkRing"); var fillProp = so.FindProperty("groundFill");
            if (inkProp.objectReferenceValue == null) inkProp.objectReferenceValue = ink.GetComponent<LineRenderer>();
            if (fillProp.objectReferenceValue == null) fillProp.objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return $"Hasat imleci okunurluğu: {added} öğe eklendi (var olanlara dokunulmadı), alanlar bağlı: mürekkep halka {inkProp.objectReferenceValue != null}, zemin {fillProp.objectReferenceValue != null}";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // 0,5 yarıçaplı düz disk (XZ), köşe renkleri: ortası çok hafif, kenara doğru biraz koyu (alan sınırı okunur).
    static Mesh BuildDisc()
    {
        var vertices = new Vector3[DiscSegments + 1]; var colors = new Color[DiscSegments + 1]; var normals = new Vector3[DiscSegments + 1];
        var triangles = new int[DiscSegments * 3];
        vertices[0] = Vector3.zero; colors[0] = FillCenter; normals[0] = Vector3.up;
        for (int i = 0; i < DiscSegments; i++)
        {
            float a = i * Mathf.PI * 2f / DiscSegments;
            vertices[i + 1] = new Vector3(Mathf.Cos(a) * .5f, 0f, Mathf.Sin(a) * .5f); colors[i + 1] = FillEdge; normals[i + 1] = Vector3.up;
            triangles[i * 3] = 0; triangles[i * 3 + 1] = (i + 1) % DiscSegments + 1; triangles[i * 3 + 2] = i + 1;
        }
        var mesh = new Mesh { name = "HarvestCursorDisc", vertices = vertices, colors = colors, normals = normals, triangles = triangles };
        mesh.RecalculateBounds();
        return mesh;
    }
}
