using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Aşılı Kabuk kişiliği asset üreticisi (25 Ekim 2026). Yalnız izole kopyada çalışır:
//   Tools/run-isolated-verification.ps1 -Method ShellBurstArtBuilder.BuildBatch -Full
// Girdi: Tools/Art/ShellBurst/export/shell_burst.json (Blender export_shell_burst.py).
// Çıktı: Assets/Art/LateGame/ShellBurst/*.asset (ağlar) ve Assets/Resources/ShellBurstArt.asset; ana projeye kopyalanır.
// Işıklı ağlar iki alt ağlı (0 gövde, 1 mürekkep kabuğu); kabuk malzemesi GreedJawInk.mat (yeni malzeme yok).
public static class ShellBurstArtBuilder
{
    const string Folder = "Assets/Art/LateGame/ShellBurst";
    const string ArtPath = "Assets/Resources/ShellBurstArt.asset";

    [Serializable] class MeshData { public string name; public float[] positions; public float[] colors; public int[] triangles; public int[] ink; }
    [Serializable]
    class Doc
    {
        public MeshData[] meshes;
        public float[] plateHinges, plateDirections, knobRest, nodeSize, slabHinges, slabDirections, clampCenter, collarHinges, collarDirections, crustCenters;
        public int[] collarCracked;
    }

    public static void BuildBatch()
    {
        var log = new List<string>();
        try
        {
            if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Tools/Art/ShellBurst/export/shell_burst.json"));
            var doc = JsonUtility.FromJson<Doc>(File.ReadAllText(source));
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art/LateGame", "ShellBurst");
            var meshes = new Dictionary<string, Mesh>();
            int total = 0;
            foreach (var d in doc.meshes) { meshes[d.name] = Save(d); total += d.triangles.Length / 3 + d.ink.Length / 3; }
            log.Add($"{doc.meshes.Length} meshes, {total} triangles (body + ink)");
            foreach (var d in doc.meshes) log.Add($"  {d.name}: {d.triangles.Length / 3} + {d.ink.Length / 3}");
            var art = AssetDatabase.LoadAssetAtPath<ShellBurstArtSO>(ArtPath);
            if (art == null) { art = ScriptableObject.CreateInstance<ShellBurstArtSO>(); AssetDatabase.CreateAsset(art, ArtPath); }
            art.lit = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/ThornSiege/ThornToon.mat");
            art.ink = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/LateGame/GreedJaw/GreedJawInk.mat");
            Mesh[] Z(string pattern) => Enumerable.Range(0, 2).Select(z => meshes[string.Format(pattern, z)]).ToArray();
            art.nodeBody = Z("node{0}_body");
            art.nodeCore = Z("node{0}_core");
            art.nodeMotif = Z("node{0}_motif");
            art.nodeKnob = Z("node{0}_knob");
            art.nodeCracks = Enumerable.Range(0, 6).Select(n => meshes[$"node{n / 3}_crack{n % 3}"]).ToArray();
            art.nodePlates = Enumerable.Range(0, 10).Select(n => meshes[$"node{n / 5}_plate{n % 5}"]).ToArray();
            art.plateHinges = V3(doc.plateHinges);
            art.plateDirections = V3(doc.plateDirections);
            art.knobRest = V3(doc.knobRest);
            art.nodeSize = V3(doc.nodeSize);
            art.birthGlow = Z("birth{0}_glow");
            art.birthCenter = meshes["birthCenter"];
            art.birthSlabs = Enumerable.Range(0, ShellBurstArtSO.Slabs).Select(k => meshes["birthSlab" + k]).ToArray();
            art.slabHinges = V3(doc.slabHinges);
            art.slabDirections = V3(doc.slabDirections);
            art.veinSegment = Z("vein{0}");
            art.clamp = Z("clamp{0}");
            art.bead = Z("bead{0}");
            art.clampCenter = V3(doc.clampCenter)[0];
            art.collarPlates = Enumerable.Range(0, ShellBurstArtSO.CollarPlates).Select(k => meshes["collarPlate" + k]).ToArray();
            art.collarHinges = V3(doc.collarHinges);
            art.collarDirections = V3(doc.collarDirections);
            art.collarCracks = Enumerable.Range(0, 12).Select(n => meshes.TryGetValue($"collar{n / 6}_crack{n % 6}", out Mesh m) ? m : null).ToArray();
            art.collarBand = Z("collar{0}_band");
            art.crust = Enumerable.Range(0, ShellBurstArtSO.Crust).Select(k => meshes["crust" + k]).ToArray();
            art.crustCenters = V3(doc.crustCenters);
            art.chip = meshes["chip"];
            art.shock = meshes["shock"];
            art.dust = Enumerable.Range(0, 3).Select(k => meshes["dust" + k]).ToArray();
            art.tink = Z("tink{0}");
            art.spark = Z("spark{0}");
            art.sparkCream = meshes["sparkCream"];
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            if (!art.Valid) throw new Exception("art incomplete");
            if (art.collarCracks.Count(m => m != null) != 2 * doc.collarCracked.Sum()) throw new Exception("collar cracks mismatch");
            log.Insert(0, "PASS");
        }
        catch (Exception e) { log.Insert(0, "FAIL " + e); }
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/ShellBurstArtBuilder.txt", log);
        EditorApplication.Exit(log[0] == "PASS" ? 0 : 1);
    }

    static Vector3[] V3(float[] f) => Enumerable.Range(0, f.Length / 3).Select(i => new Vector3(f[i * 3], f[i * 3 + 1], f[i * 3 + 2])).ToArray();

    static Mesh Save(MeshData d)
    {
        int n = d.positions.Length / 3;
        var mesh = new Mesh { name = "ShellBurst_" + d.name };
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
        string path = $"{Folder}/ShellBurst_{d.name}.asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, old);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(old);
        return old;
    }
}
