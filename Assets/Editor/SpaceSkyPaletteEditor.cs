using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Gökyüzü paleti inspector'ı (23 Kasım 2026): Play'e basmadan "güç önizleme" kaydırıcısıyla her aşamanın / geçişin oyundaki shader'la
// çizilmiş hâli görülür. Önizleme yalnız editörde bir dokuya çizilir; sahneye, oyun durumuna ya da kayda dokunmaz.
[CustomEditor(typeof(SpaceSkyPaletteSO))]
public sealed class SpaceSkyPaletteEditor : Editor
{
    const int PreviewWidth = 480, PreviewHeight = 270;
    static float previewPower;
    static bool previewFixed;

    RenderTexture preview;
    Material material;
    Mesh quad;

    public override void OnInspectorGUI()
    {
        var palette = (SpaceSkyPaletteSO)target;
        serializedObject.Update();
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Önizleme (oyundaki shader)", EditorStyles.boldLabel);
        previewFixed = EditorGUILayout.Toggle(new GUIContent("Sabit (ayar: Sabit)", "Ayarlarda 'Sabit' seçildiğinde görünen palet."), previewFixed);
        int max = palette.stages.Count > 0 ? palette.stages[palette.stages.Count - 1].minPower + 6 : 10;
        using (new EditorGUI.DisabledScope(previewFixed))
            previewPower = EditorGUILayout.Slider(new GUIContent("Güç önizleme", "Müzik gücü (boss ödülleri). Aşamalar arası değerlerde iki aşamanın karışımı gösterilir; oyunda geçiş 'transitionSeconds' sürer."), previewPower, 0f, max);

        SpaceSkyPaletteSO.SkyColors colors = previewFixed ? palette.fixedPalette : palette.Preview(previewPower);
        if (!previewFixed && palette.stages.Count > 0)
        {
            var stage = palette.stages[palette.StageIndexFor(Mathf.FloorToInt(previewPower))];
            EditorGUILayout.LabelField("Aşama", stage != null ? stage.name : "-");
        }

        Rect swatches = GUILayoutUtility.GetRect(PreviewWidth, 18f, GUILayout.ExpandWidth(true));
        Color[] row = { colors.deep, colors.mid, colors.glow, colors.nebulaA, colors.nebulaB, colors.lit };
        float w = swatches.width / row.Length;
        for (int i = 0; i < row.Length; i++)
            EditorGUI.DrawRect(new Rect(swatches.x + i * w + 1f, swatches.y, w - 2f, swatches.height), row[i]);

        Rect area = GUILayoutUtility.GetAspectRect(16f / 9f, GUILayout.MaxWidth(PreviewWidth * 1.5f));
        if (Event.current.type == EventType.Repaint && Render(palette, colors))
            GUI.DrawTexture(area, preview, ScaleMode.ScaleToFit, false);
        serializedObject.ApplyModifiedProperties();
    }

    bool Render(SpaceSkyPaletteSO palette, SpaceSkyPaletteSO.SkyColors colors)
    {
        if (preview == null)
        {
            preview = new RenderTexture(PreviewWidth, PreviewHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { hideFlags = HideFlags.DontSave };
            preview.Create();
        }
        return RenderPreview(palette, colors, preview, ref material, ref quad);
    }

    // Doğrulama da kullanır: oyunun shader'ıyla tam ekran çizim (sabit zaman 37 sn: kare durağan kalır).
    public static bool RenderPreview(SpaceSkyPaletteSO palette, SpaceSkyPaletteSO.SkyColors colors, RenderTexture target, ref Material material, ref Mesh quad)
    {
        if (material == null)
        {
            Shader shader = Resources.Load<Shader>("SpaceToonBackground");
            if (shader == null || !shader.isSupported) return false;
            material = new Material(shader) { hideFlags = HideFlags.DontSave, name = "Sky Palette Preview" };
        }
        if (quad == null)
        {
            quad = new Mesh { hideFlags = HideFlags.DontSave, name = "Sky Palette Preview Quad" };
            quad.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            quad.bounds = new Bounds(Vector3.zero, Vector3.one * 10f);
        }
        SpaceToonNoiseBaker.Bind(material);
        material.SetVector("_SkyDeep", colors.deep);
        material.SetVector("_SkyMid", colors.mid);
        material.SetVector("_SkyGlow", colors.glow);
        material.SetVector("_SkyNebulaA", colors.nebulaA);
        material.SetVector("_SkyNebulaB", colors.nebulaB);
        material.SetVector("_SkyLit", colors.lit);
        material.SetFloat("_SkyBreath", palette.breath);
        material.SetVector("_SpaceView", new Vector4(37f, target.width / (float)target.height, 0f, 1.1f));
        material.SetVector("_SpaceOffset", Vector4.zero);

        var commands = new CommandBuffer { name = "Sky Palette Preview" };
        commands.SetRenderTarget(target);
        commands.ClearRenderTarget(false, true, Color.black);
        commands.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.identity, false));
        commands.DrawMesh(quad, Matrix4x4.identity, material, 0, 0);
        Graphics.ExecuteCommandBuffer(commands);
        commands.Release();
        return true;
    }

    void OnDisable()
    {
        if (preview != null) { preview.Release(); DestroyImmediate(preview); }
        if (material != null) DestroyImmediate(material);
        if (quad != null) DestroyImmediate(quad);
    }
}
