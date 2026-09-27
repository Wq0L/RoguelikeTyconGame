using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Run only in the isolated verification project; no gameplay assets are edited.
public static class ToonRenderingVerification
{
    public static void RunBatch()
    {
        try
        {
            foreach (string name in new[] { "Simple Toon/SToon Default", "Simple Toon/SToon Outline" })
            {
                var shader = Shader.Find(name);
                if (shader == null) throw new Exception("Missing shader: " + name);
                var material = new Material(shader) { enableInstancing = true };
                // INSTANCING_ON is selected by the draw API, not Material.EnableKeyword.
                for (int pass = 0; pass < material.passCount; pass++) ShaderUtil.CompilePass(material, pass, true);
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    if (message.severity.ToString() == "Error") throw new Exception(message.message);
                VerifyPixels(material);
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    if (message.severity.ToString() == "Error") throw new Exception(message.message);
                UnityEngine.Object.DestroyImmediate(material);
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/ToonRenderingVerification.txt", "PASS: toon passes compile; instanced tint/flash match separate draws.\n" + SystemInfo.graphicsDeviceType);
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    static void VerifyPixels(Material material)
    {
        material.SetFloat("_MinLight", 1); material.SetFloat("_MaxLight", 1);
        material.SetFloat("_ShnIntense", 0); material.SetFloat("_Cull", 0);
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var mesh = go.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(go);
        var matrices = new[] { Matrix4x4.TRS(new Vector3(-.6f,0,0), Quaternion.identity, Vector3.one), Matrix4x4.TRS(new Vector3(.6f,0,0), Quaternion.identity, Vector3.one) };
        var colors = new[] { new Vector4(0,1,0,1), new Vector4(1,0,0,1) };
        var flashes = new[] { 0f, 1f };
        var flashColors = new[] { new Vector4(1,1,1,1), new Vector4(0,0,1,1) };
        Color32[] Render(bool instanced)
        {
            var rt = new RenderTexture(128,64,24,RenderTextureFormat.ARGB32);
            rt.Create();
            var cb = new CommandBuffer();
            cb.SetRenderTarget(rt); cb.ClearRenderTarget(true,true,Color.black);
            cb.SetViewProjectionMatrices(Matrix4x4.Translate(new Vector3(0,0,-3)), GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-1.3f,1.3f,-.65f,.65f,.1f,10),true));
            var props = new MaterialPropertyBlock();
            if (instanced)
            {
                props.SetVectorArray("_BaseColor",colors); props.SetFloatArray("_ToonFlash",flashes); props.SetVectorArray("_ToonFlashColor",flashColors);
                cb.DrawMeshInstanced(mesh,0,material,0,matrices,2,props);
            }
            else for (int i=0;i<2;i++)
            {
                props.Clear(); props.SetVector("_BaseColor",colors[i]); props.SetFloat("_ToonFlash",flashes[i]); props.SetVector("_ToonFlashColor",flashColors[i]);
                cb.DrawMesh(mesh,matrices[i],material,0,0,props);
            }
            Graphics.ExecuteCommandBuffer(cb); cb.Release();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(128,64,TextureFormat.RGBA32,false);
            texture.ReadPixels(new Rect(0,0,128,64),0,0); texture.Apply();
            var pixels = texture.GetPixels32();
            RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            return pixels;
        }
        var ordinary = Render(false); var batched = Render(true);
        int colored = 0; int errors = 0;
        for (int i=0;i<ordinary.Length;i++)
        {
            if (ordinary[i].g > 128 || ordinary[i].b > 128) colored++;
            if (Math.Abs(ordinary[i].r-batched[i].r)>2 || Math.Abs(ordinary[i].g-batched[i].g)>2 || Math.Abs(ordinary[i].b-batched[i].b)>2) errors++;
        }
        if (colored < 500 || errors > 10) throw new Exception($"{material.shader.name}: colored={colored}, different={errors}");
    }
}
