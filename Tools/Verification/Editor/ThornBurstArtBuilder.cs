using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Diken çıkış patlaması asset üreticisi (24 Ekim 2026). Yalnız izole kopyada çalışır:
//   Tools/run-isolated-verification.ps1 -Method ThornBurstArtBuilder.BuildBatch -Full
// Girdi: Tools/Art/ThornBurst/export/thorn_burst.json (Blender export_thorn_burst.py).
// Çıktı: Assets/Art/LateGame/ThornBurst/*.asset (ağlar) ve Assets/Resources/ThornBurstArt.asset; ana projeye kopyalanır.
// Işıklı ağlar iki alt ağlı (0 gövde, 1 mürekkep kabuğu); kabuk malzemesi Açgözlü ağzınınkiyle aynı (GreedJawInk.mat, yeni malzeme yok).
public static class ThornBurstArtBuilder
{
    const string Folder = "Assets/Art/LateGame/ThornBurst";
    const string ArtPath = "Assets/Resources/ThornBurstArt.asset";

    [Serializable] class MeshData { public string name; public float[] positions; public float[] colors; public int[] triangles; public int[] ink; }
    [Serializable] class Count { public string name; public int body, ink, vertices; }
    [Serializable] class Doc { public MeshData[] meshes; public float[] clods, clouds; public Count[] counts; }

    public static void BuildBatch()
    {
        var log = new List<string>();
        try
        {
            if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Tools/Art/ThornBurst/export/thorn_burst.json"));
            var doc = JsonUtility.FromJson<Doc>(File.ReadAllText(source));
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/LateGame", "ThornBurst");
            var meshes = new Dictionary<string, Mesh>();
            foreach (var d in doc.meshes)
            {
                meshes[d.name] = Save(d);
                log.Add($"{d.name}: {d.triangles.Length / 3} body + {d.ink.Length / 3} ink triangles, {d.positions.Length / 3} vertices");
            }
            var art = AssetDatabase.LoadAssetAtPath<ThornBurstArtSO>(ArtPath);
            if (art == null) { art = ScriptableObject.CreateInstance<ThornBurstArtSO>(); AssetDatabase.CreateAsset(art, ArtPath); }
            art.lit = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornToon.mat");
            art.ink = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/GreedJaw/GreedJawInk.mat");
            art.blast = meshes["blast"];
            art.lines = meshes["lines"];
            art.clouds = Enumerable.Range(0, 3).Select(i => meshes["cloud" + i]).ToArray();
            art.clods = Enumerable.Range(0, 3).Select(i => meshes["clod" + i]).ToArray();
            art.craterLit = meshes["craterLit"];
            art.craterScorch = meshes["craterScorch"];
            art.clodFlights = Enumerable.Range(0, doc.clods.Length / 7).Select(i => new ThornBurstArtSO.ClodFlight
            {
                variant = (int)doc.clods[i * 7], direction = new Vector2(doc.clods[i * 7 + 1], doc.clods[i * 7 + 2]),
                distance = doc.clods[i * 7 + 3], rise = doc.clods[i * 7 + 4], spin = doc.clods[i * 7 + 5], size = doc.clods[i * 7 + 6],
            }).ToArray();
            art.cloudPuffs = Enumerable.Range(0, doc.clouds.Length / 7).Select(i => new ThornBurstArtSO.CloudPuff
            {
                variant = (int)doc.clouds[i * 7], screen = new Vector2(doc.clouds[i * 7 + 1], doc.clouds[i * 7 + 2]),
                reach = doc.clouds[i * 7 + 3], rise = doc.clouds[i * 7 + 4], delay = doc.clouds[i * 7 + 5], size = doc.clouds[i * 7 + 6],
            }).ToArray();
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            if (!art.Valid) throw new Exception("art incomplete");
            log.Insert(0, "PASS");
        }
        catch (Exception e) { log.Insert(0, "FAIL " + e); }
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/ThornBurstArtBuilder.txt", log);
        EditorApplication.Exit(log[0] == "PASS" ? 0 : 1);
    }

    static Mesh Save(MeshData d)
    {
        int n = d.positions.Length / 3;
        var mesh = new Mesh { name = "ThornBurst_" + d.name };
        var vertices = new Vector3[n];
        var colors = new Color32[n];
        for (int i = 0; i < n; i++)
        {
            vertices[i] = new Vector3(d.positions[i * 3], d.positions[i * 3 + 1], d.positions[i * 3 + 2]);
            colors[i] = new Color(d.colors[i * 3], d.colors[i * 3 + 1], d.colors[i * 3 + 2], 1f);
        }
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        bool lit = d.ink != null && d.ink.Length > 0;
        mesh.subMeshCount = lit ? 2 : 1;
        mesh.SetTriangles(d.triangles, 0);
        if (lit) mesh.SetTriangles(d.ink, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        string path = $"{Folder}/ThornBurst_{d.name}.asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, old);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(old);
        return old;
    }
}
