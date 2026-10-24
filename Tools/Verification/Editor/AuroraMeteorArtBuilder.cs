using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Kuzey ışığı meteoru asset üreticisi (23 Ekim 2026). Yalnız izole kopyada çalışır:
//   Tools/run-isolated-verification.ps1 -Method AuroraMeteorArtBuilder.BuildBatch -Full
// Girdi: Tools/Art/AuroraMeteor/export/aurora_meteor.json (Blender export_aurora_meteor.py).
// Çıktı: Assets/Art/LateGame/AuroraMeteor/*.asset (ağlar) ve Assets/Resources/AuroraMeteorArt.asset; ana projeye kopyalanır.
// Ağlar düz gölgeli (her üçgen kendi köşeleri), renkler doğrusal köşe rengi; normaller Unity'de yeniden hesaplanır.
public static class AuroraMeteorArtBuilder
{
    const string Folder = "Assets/Art/LateGame/AuroraMeteor";
    const string ArtPath = "Assets/Resources/AuroraMeteorArt.asset";

    [Serializable] class MeshData { public string name; public float[] positions; public float[] colors; public int[] triangles; }
    [Serializable] class Doc { public MeshData[] meshes; public float[] rims; public float cell; public float rockRadius; }

    public static void BuildBatch()
    {
        var log = new List<string>();
        try
        {
            if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Tools/Art/AuroraMeteor/export/aurora_meteor.json"));
            var doc = JsonUtility.FromJson<Doc>(File.ReadAllText(source));
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/LateGame", "AuroraMeteor");
            var meshes = new Dictionary<string, Mesh>();
            foreach (var d in doc.meshes)
            {
                meshes[d.name] = Save(d);
                log.Add($"{d.name}: {d.triangles.Length / 3} triangles");
            }
            var art = AssetDatabase.LoadAssetAtPath<AuroraMeteorArtSO>(ArtPath);
            if (art == null) { art = ScriptableObject.CreateInstance<AuroraMeteorArtSO>(); AssetDatabase.CreateAsset(art, ArtPath); }
            art.lit = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornToon.mat");
            art.ink = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornInk.mat");
            art.rock = meshes["rock"];
            art.rockCracks = meshes["rockCracks"];
            art.trail = meshes["trail"];
            art.crown = meshes["crown"];
            art.craterGround = meshes["craterGround"];
            art.clouds = Enumerable.Range(0, 9).Select(i => "cloud" + i).Where(meshes.ContainsKey).Select(n => meshes[n]).ToArray();
            art.stones = Enumerable.Range(0, 9).Select(i => "stone" + i).Where(meshes.ContainsKey).Select(n => meshes[n]).ToArray();
            art.rims = Enumerable.Range(0, 32).Select(i => "rim" + i).Where(meshes.ContainsKey).Select(n => meshes[n]).ToArray();
            art.rimPositions = Enumerable.Range(0, doc.rims.Length / 3).Select(i => new Vector3(doc.rims[i * 3], doc.rims[i * 3 + 1], doc.rims[i * 3 + 2])).ToArray();
            art.cell = doc.cell;
            art.rockRadius = doc.rockRadius;
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            if (!art.Valid) throw new Exception("art incomplete");
            log.Insert(0, "PASS");
        }
        catch (Exception e) { log.Insert(0, "FAIL " + e); }
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/AuroraMeteorArtBuilder.txt", log);
        EditorApplication.Exit(log[0] == "PASS" ? 0 : 1);
    }

    static Mesh Save(MeshData d)
    {
        int n = d.positions.Length / 3;
        var mesh = new Mesh { name = "AuroraMeteor_" + d.name };
        var vertices = new Vector3[n];
        var colors = new Color[n];
        for (int i = 0; i < n; i++)
        {
            vertices[i] = new Vector3(d.positions[i * 3], d.positions[i * 3 + 1], d.positions[i * 3 + 2]);
            colors[i] = new Color(d.colors[i * 3], d.colors[i * 3 + 1], d.colors[i * 3 + 2], 1f);
        }
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = d.triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        string path = $"{Folder}/AuroraMeteor_{d.name}.asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, old);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(old);
        return old;
    }
}
