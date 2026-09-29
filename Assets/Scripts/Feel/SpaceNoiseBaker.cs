using UnityEngine;
using UnityEngine.Rendering;

// Uzay arka planlarının (oyun sahnesi + skill tree) ortak nebula gürültüsü. Her piksel/her karede
// 3 katmanlı fbm hesaplamak yerine bir kez düşük çözünürlüklü bir texture'a çizilir; shader'lar
// sadece okur. Nebula yumuşak olduğu için çözünürlük farkı görünmez.
// İçerik: R = yoğunluk, G = renk karışımı. Alan: np ∈ [-DomainX/2, DomainX/2] x [-DomainY/2, DomainY/2].
public static class SpaceNoiseBaker
{
    // SpaceNoiseBake / SpaceBackground / SpaceUIBackground shader'larıyla aynı olmalı.
    public const float DomainX = 5.6f;
    public const float DomainY = 3.0f;
    private const int Width = 640;
    private const int Height = 344;

    private static readonly int NoiseTexId = Shader.PropertyToID("_SpaceNoiseTex");
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

    // Materyale texture'ı bağlar; texture kaybolduysa (cihaz sıfırlanması vb.) yeniden çizer.
    // Her kare çağrılabilir: geçerliyse sadece bir IsCreated kontrolü yapar.
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
            var shader = Resources.Load<Shader>("SpaceNoiseBake");
            if (shader == null || !shader.isSupported) return false;
            material = new Material(shader) { name = "Space Noise Bake (runtime)" };
        }
        if (texture == null)
        {
            RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
            texture = new RenderTexture(Width, Height, 0, format, RenderTextureReadWrite.Linear)
            {
                name = "Space Nebula Noise (runtime)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false
            };
        }
        if (!texture.IsCreated()) texture.Create();

        var commands = new CommandBuffer { name = "Bake Space Nebula" };
        commands.SetRenderTarget(texture);
        commands.ClearRenderTarget(false, true, Color.clear);
        commands.DrawMesh(GetQuad(), Matrix4x4.identity, material, 0, 0);
        Graphics.ExecuteCommandBuffer(commands);
        commands.Release();
        return true;
    }

    // Clip-space'i tam kaplayan quad ([-1, 1]).
    private static Mesh GetQuad()
    {
        if (quad != null) return quad;
        quad = new Mesh { name = "Space Noise Bake Quad" };
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
