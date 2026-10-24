using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Açgözlü Hasat "aç ağız halkası" asset üreticisi (24 Ekim 2026). Yalnız izole kopyada çalışır:
//   Tools/run-isolated-verification.ps1 -Method GreedJawArtBuilder.BuildBatch -Full
// Girdi: Tools/Art/GreedJaw/export/greed_jaw.json (Blender export_greed_jaw.py).
// Çıktı: Assets/Art/LateGame/GreedJaw/*.asset (ağlar), GreedJawInk.mat ve Assets/Resources/GreedJawArt.asset; ana projeye kopyalanır.
// Işıklı ağlar iki alt ağlıdır (0 gövde, 1 mürekkep kabuğu); düz gölgeli, renkler doğrusal köşe rengi.
public static class GreedJawArtBuilder
{
    const string Folder = "Assets/Art/LateGame/GreedJaw";
    const string ArtPath = "Assets/Resources/GreedJawArt.asset";
    const string InkPath = Folder + "/GreedJawInk.mat";

    [Serializable] class MeshData { public string name; public float[] positions; public float[] colors; public int[] triangles; public int[] ink; }
    [Serializable] class Doc { public MeshData[] meshes; public float[] upper, lower, chips; public float designRadius, miniRadius; }

    public static void BuildBatch()
    {
        var log = new List<string>();
        try
        {
            if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Tools/Art/GreedJaw/export/greed_jaw.json"));
            var doc = JsonUtility.FromJson<Doc>(File.ReadAllText(source));
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/LateGame", "GreedJaw");
            var meshes = new Dictionary<string, Mesh>();
            foreach (var d in doc.meshes)
            {
                meshes[d.name] = Save(d);
                log.Add($"{d.name}: {d.triangles.Length / 3} body + {d.ink.Length / 3} ink triangles");
            }
            var thornInk = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornInk.mat");
            var ink = AssetDatabase.LoadAssetAtPath<Material>(InkPath);
            if (ink == null) { ink = new Material(thornInk) { name = "GreedJawInk" }; AssetDatabase.CreateAsset(ink, InkPath); }
            ink.CopyPropertiesFromMaterial(thornInk);
            ink.SetFloat("_OutlineWidth", 0f);   // kabuk Blender'dan hazır gelir; merkezden itme yok
            ink.SetFloat("_Cull", 2f);           // içe bakan kabuk: arka yüz ayıklanır, arkadaki kenar kalır
            EditorUtility.SetDirty(ink);

            var art = AssetDatabase.LoadAssetAtPath<GreedJawArtSO>(ArtPath);
            if (art == null) { art = ScriptableObject.CreateInstance<GreedJawArtSO>(); AssetDatabase.CreateAsset(art, ArtPath); }
            art.lit = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornToon.mat");
            art.ink = ink;
            art.gumUpper = meshes["gumUpper"];
            art.gumLower = meshes["gumLower"];
            art.teethUpper = Enumerable.Range(0, 3).Select(i => meshes["toothUpper" + i]).ToArray();
            art.teethLower = Enumerable.Range(0, 3).Select(i => meshes["toothLower" + i]).ToArray();
            art.mini = meshes["mini"];
            art.chips = Enumerable.Range(0, 9).Select(i => "chip" + i).Where(meshes.ContainsKey).Select(n => meshes[n]).ToArray();
            art.spike = meshes["spike"];
            art.puff = meshes["puff"];
            art.upper = Slots(doc.upper);
            art.lower = Slots(doc.lower);
            art.chipSlots = Enumerable.Range(0, doc.chips.Length / 6).Select(i => new GreedJawArtSO.ChipSlot
            {
                color = (int)doc.chips[i * 6], direction = new Vector2(doc.chips[i * 6 + 1], doc.chips[i * 6 + 2]),
                distance = doc.chips[i * 6 + 3], rise = doc.chips[i * 6 + 4], scale = doc.chips[i * 6 + 5],
            }).ToArray();
            art.designRadius = doc.designRadius;
            art.miniRadius = doc.miniRadius;
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            if (!art.Valid) throw new Exception("art incomplete");
            log.Insert(0, "PASS");
        }
        catch (Exception e) { log.Insert(0, "FAIL " + e); }
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/GreedJawArtBuilder.txt", log);
        EditorApplication.Exit(log[0] == "PASS" ? 0 : 1);
    }

    // Blender sırası: x, z, y'si dönüştürülmüş konum (çene uzayı); tip, konum, dönüş, ölçek.
    static GreedJawArtSO.ToothSlot[] Slots(float[] rows) => Enumerable.Range(0, rows.Length / 6).Select(i => new GreedJawArtSO.ToothSlot
    {
        kind = (int)rows[i * 6], position = new Vector3(rows[i * 6 + 1], rows[i * 6 + 2], rows[i * 6 + 3]),
        yaw = rows[i * 6 + 4], scale = rows[i * 6 + 5],
    }).ToArray();

    static Mesh Save(MeshData d)
    {
        int n = d.positions.Length / 3;
        var mesh = new Mesh { name = "GreedJaw_" + d.name };
        var vertices = new Vector3[n];
        var colors = new Color[n];
        for (int i = 0; i < n; i++)
        {
            vertices[i] = new Vector3(d.positions[i * 3], d.positions[i * 3 + 1], d.positions[i * 3 + 2]);
            colors[i] = new Color(d.colors[i * 3], d.colors[i * 3 + 1], d.colors[i * 3 + 2], 1f);
        }
        mesh.vertices = vertices;
        mesh.colors = colors;
        bool lit = d.ink != null && d.ink.Length > 0;
        mesh.subMeshCount = lit ? 2 : 1;
        mesh.SetTriangles(d.triangles, 0);
        if (lit) mesh.SetTriangles(d.ink, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.UploadMeshData(false);
        string path = $"{Folder}/GreedJaw_{d.name}.asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, old);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(old);
        return old;
    }
}
