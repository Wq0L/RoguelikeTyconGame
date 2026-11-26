using UnityEngine;
using UnityEngine.Rendering;

// Toon gökyüzünün nebula gürültüsü (SpaceToonBackground shader'ı okur). SpaceNoiseBaker ile aynı düzen; ayrı doku çünkü toon nebulanın sert
// eşikleri yüksek çözünürlük ister (640 px'lik eski dokuda bilineer süzgeç kenarları köşeli yapar) ve yetenek ağacının panosu eski dokuyu
// okumaya devam eder (görünümü değişmesin). 1280×688 RG16F ≈ 3,5 MB; bir kez çizilir, doku kaybolursa yeniden çizilir.
public static class SpaceToonNoiseBaker
{
    // SpaceToonNoiseBake / SpaceToonBackground shader'larıyla aynı olmalı.
    public const float DomainX = 5.6f;
    public const float DomainY = 3.0f;
    private const int Width = 1280;
    private const int Height = 688;

    private static readonly int NoiseTexId = Shader.PropertyToID("_SpaceToonNoiseTex");
    private static RenderTexture texture;
    private static Material material;
    private static Mesh quad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        texture = null;
        material = null;
        quad = null;
    }

    // Materyale dokuyu bağlar; doku yoksa / kaybolduysa çizer. Her kare çağrılabilir (geçerliyse yalnız bir IsCreated kontrolü).
    public static void Bind(Material target)
    {
        if (target == null) return;
        if (texture == null || !texture.IsCreated())
        {
            if (!Bake()) return;
            target.SetTexture(NoiseTexId, texture);
            return;
        }
        if (target.GetTexture(NoiseTexId) != texture) target.SetTexture(NoiseTexId, texture);
    }

    private static bool Bake()
    {
        if (material == null)
        {
            var shader = Resources.Load<Shader>("SpaceToonNoiseBake");
            if (shader == null || !shader.isSupported) return false;
            material = new Material(shader) { name = "Space Toon Noise Bake (runtime)", hideFlags = HideFlags.DontSave };
        }
        if (texture == null)
        {
            RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf) ? RenderTextureFormat.RGHalf
                : SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
            texture = new RenderTexture(Width, Height, 0, format, RenderTextureReadWrite.Linear)
            {
                name = "Space Toon Nebula Noise (runtime)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                hideFlags = HideFlags.DontSave
            };
        }
        if (!texture.IsCreated()) texture.Create();

        var commands = new CommandBuffer { name = "Bake Space Toon Nebula" };
        commands.SetRenderTarget(texture);
        commands.ClearRenderTarget(false, true, Color.clear);
        commands.DrawMesh(GetQuad(), Matrix4x4.identity, material, 0, 0);
        Graphics.ExecuteCommandBuffer(commands);
        commands.Release();
        return true;
    }

    private static Mesh GetQuad()
    {
        if (quad != null) return quad;
        quad = new Mesh { name = "Space Toon Noise Bake Quad", hideFlags = HideFlags.DontSave };
        quad.vertices = new[]
        {
            new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f),
            new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f)
        };
        quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quad.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
        return quad;
    }
}
