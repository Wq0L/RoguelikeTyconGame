using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// İstilacı Filiz kişiliği asset üreticisi (25 Ekim 2026). Yalnız izole kopyada çalışır:
//   Tools/run-isolated-verification.ps1 -Method SproutBurstArtBuilder.BuildBatch -Full
// Girdi: Tools/Art/SproutBurst/export/sprout_burst.json (Blender export_sprout_burst.py).
// Çıktı: Assets/Art/LateGame/SproutBurst/*.asset (ağlar) ve Assets/Resources/SproutBurstArt.asset; ana projeye kopyalanır.
// Işıklı ağlar iki alt ağlı (0 gövde, 1 mürekkep kabuğu); kabuk malzemesi GreedJawInk.mat (yeni malzeme yok).
public static class SproutBurstArtBuilder
{
    const string Folder = "Assets/Art/LateGame/SproutBurst";
    const string ArtPath = "Assets/Resources/SproutBurstArt.asset";

    [Serializable] class MeshData { public string name; public float[] positions; public float[] colors; public int[] triangles; public int[] ink; }
    [Serializable] class Doc { public MeshData[] meshes; public float[] tendrilStarts, tendrilRoots, vineStarts; }

    public static void BuildBatch()
    {
        var log = new List<string>();
        try
        {
            if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Tools/Art/SproutBurst/export/sprout_burst.json"));
            var doc = JsonUtility.FromJson<Doc>(File.ReadAllText(source));
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/LateGame", "SproutBurst");
            var meshes = new Dictionary<string, Mesh>();
            int total = 0;
            foreach (var d in doc.meshes) { meshes[d.name] = Save(d); total += d.triangles.Length / 3 + d.ink.Length / 3; }
            log.Add($"{doc.meshes.Length} meshes, {total} triangles (body + ink)");
            var art = AssetDatabase.LoadAssetAtPath<SproutBurstArtSO>(ArtPath);
            if (art == null) { art = ScriptableObject.CreateInstance<SproutBurstArtSO>(); AssetDatabase.CreateAsset(art, ArtPath); }
            art.lit = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornToon.mat");
            art.ink = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/GreedJaw/GreedJawInk.mat");
            art.cracks = Enumerable.Range(0, 5).Select(i => meshes["crack" + i]).ToArray();
            art.tendrils = Enumerable.Range(0, SproutBurstArtSO.Tendrils * SproutBurstArtSO.TendrilSegments)
                .Select(n => meshes[$"tendril{n / SproutBurstArtSO.TendrilSegments}_{n % SproutBurstArtSO.TendrilSegments}"]).ToArray();
            art.tendrilStarts = V3(doc.tendrilStarts, 3);
            art.tendrilRoots = Enumerable.Range(0, doc.tendrilRoots.Length / 5).Select(i => new Vector3(doc.tendrilRoots[i * 5], doc.tendrilRoots[i * 5 + 1], doc.tendrilRoots[i * 5 + 2])).ToArray();
            // Blender yönü (x, y) → Unity (x, z)
            art.tendrilDirections = Enumerable.Range(0, doc.tendrilRoots.Length / 5).Select(i => new Vector2(doc.tendrilRoots[i * 5 + 3], doc.tendrilRoots[i * 5 + 4])).ToArray();
            art.vine = Enumerable.Range(0, SproutBurstArtSO.VineSegments).Select(i => meshes["vine" + i]).ToArray();
            art.vineStarts = V3(doc.vineStarts, 3);
            art.puffs = Enumerable.Range(0, 3).Select(i => meshes["puff" + i]).ToArray();
            art.pollen = Enumerable.Range(0, 2).Select(i => meshes["pollen" + i]).ToArray();
            art.splat = meshes["splat"];
            art.drop = meshes["drop"];
            art.shred = meshes["shred"];
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            if (!art.Valid) throw new Exception("art incomplete");
            log.Insert(0, "PASS");
        }
        catch (Exception e) { log.Insert(0, "FAIL " + e); }
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/SproutBurstArtBuilder.txt", log);
        EditorApplication.Exit(log[0] == "PASS" ? 0 : 1);
    }

    static Vector3[] V3(float[] f, int stride) => Enumerable.Range(0, f.Length / stride).Select(i => new Vector3(f[i * stride], f[i * stride + 1], f[i * stride + 2])).ToArray();

    static Mesh Save(MeshData d)
    {
        int n = d.positions.Length / 3;
        var mesh = new Mesh { name = "SproutBurst_" + d.name };
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
        string path = $"{Folder}/SproutBurst_{d.name}.asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, old);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(old);
        return old;
    }
}
