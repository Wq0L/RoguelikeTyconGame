using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class LateGameGeometryReport
{
    public static void RunBatch(){Write();EditorApplication.Exit(0);}
    public static void Write()
    {
        var lines=new List<string>{"# Diken ve Filiz geometri bütçesi", "", "Unity üçgen ve vertex sayımı. Vertexler flat shading nedeniyle yüzler arasında yinelenir; bunlar benzersiz geometrik köşe sayısı değildir. Kapalı hasar aşamaları üst sınıra dahildir. Ink ve AttentionRim aynı mesh'i paylaşır, ek mesh belleği üretmez ancak ek çizim ve üçgen gönderimi getirir.","", "| Prefab | Gövde vertex | Gövde üçgen | Koyu kontur üçgen | Renkli kontur üçgen | Toplam üçgen | Renderer |", "|---|---:|---:|---:|---:|---:|---:|"};
        foreach(var dir in new[]{"ThornSiege","InvasiveSprout"})
        foreach(var path in Directory.GetFiles("Assets/Art/LateGame/"+dir,"*.prefab").Where(p=>Path.GetFileNameWithoutExtension(p)!="ThornWarning").OrderBy(p=>p))
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);var filters=root.GetComponentsInChildren<MeshFilter>(true);
            var body=filters.Where(f=>f.name!="Ink"&&f.name!="AttentionRim");
            int b=body.Sum(f=>f.sharedMesh.triangles.Length/3),v=body.Sum(f=>f.sharedMesh.vertexCount),ink=filters.Where(f=>f.name=="Ink").Sum(f=>f.sharedMesh.triangles.Length/3),rim=filters.Where(f=>f.name=="AttentionRim").Sum(f=>f.sharedMesh.triangles.Length/3);
            lines.Add($"| {root.name} | {v} | {b} | {ink} | {rim} | {b+ink+rim} | {filters.Length} |");
        }
        Directory.CreateDirectory("Logs");File.WriteAllLines("Logs/LateGameGeometry.md",lines);
    }
}

