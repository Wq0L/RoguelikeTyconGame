using UnityEngine;

// Bitki hayattayken altında rarity rengini gösteren toon halka. Common'da yok (en sık bitki, kalabalık yapmasın);
// rarity arttıkça halka belirginleşir, Epic/Legendary nabız atar. Renkler hasat patlamasıyla aynı palet.
// Bitkiyle birlikte havuza döner; tekrar kiralanınca yeni rarity'ye göre güncellenir.
// Rarity başına tek paylaşılan materyal: SRP Batcher hepsini birlikte çizer.
public static class PlantRarityAura
{
    private const string ChildName = "Rarity Aura";
    private const float Diameter = 1.7f;
    private const float Lift = 0.08f;

    private struct Style
    {
        public Color color;
        public float strength, pulse, dashes;
    }

    // Common, Uncommon, Rare, Epic, Legendary
    private static readonly Style[] Styles =
    {
        new Style { color = Color.white, strength = 0f },
        new Style { color = new Color(0.42f, 0.9f, 0.36f), strength = 0.45f },
        new Style { color = new Color(0.3f, 0.62f, 1f), strength = 0.65f },
        new Style { color = new Color(0.72f, 0.42f, 1f), strength = 0.85f, pulse = 1f },
        new Style { color = new Color(1f, 0.74f, 0.2f), strength = 1f, pulse = 1f, dashes = 1f }
    };

    private static readonly Material[] materials = new Material[Styles.Length];
    private static Mesh quad;
    private static Shader shader;
    private static bool shaderLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        System.Array.Clear(materials, 0, materials.Length);
        quad = null;
        shader = null;
        shaderLoaded = false;
    }

    // Bitki aktif edildikten SONRA çağrılmalı: PlantHealth kendi model renderer'ını Awake'te bulur.
    public static void Apply(Transform plant, PlantRarity rarity)
    {
        if (plant == null || Application.isBatchMode) return;
        Transform aura = plant.Find(ChildName);
        Material material = GameSettings.RarityAuras ? MaterialFor(rarity) : null;
        if (material == null)
        {
            if (aura != null) aura.gameObject.SetActive(false);
            return;
        }

        if (aura == null) aura = Create(plant);
        Vector3 scale = plant.lossyScale;
        float uniform = Mathf.Max(0.0001f, Mathf.Abs(scale.x));
        aura.localScale = Vector3.one * (Diameter / uniform);
        aura.localPosition = new Vector3(0f, Lift / Mathf.Max(0.0001f, Mathf.Abs(scale.y)), 0f);
        aura.GetComponent<MeshRenderer>().sharedMaterial = material;
        aura.gameObject.SetActive(true);
    }

    private static Transform Create(Transform plant)
    {
        var auraObject = new GameObject(ChildName, typeof(MeshFilter), typeof(MeshRenderer));
        auraObject.layer = plant.gameObject.layer;
        Transform aura = auraObject.transform;
        aura.SetParent(plant, false);
        aura.SetAsLastSibling();
        // Quad XY düzleminde; yere yatır. Halka simetrik olduğu için bitkinin Y dönüşü önemli değil.
        aura.localRotation = Quaternion.Euler(90f, 0f, 0f);
        auraObject.GetComponent<MeshFilter>().sharedMesh = GetQuad();
        var meshRenderer = auraObject.GetComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return aura;
    }

    private static Material MaterialFor(PlantRarity rarity)
    {
        int index = Mathf.Clamp((int)rarity, 0, Styles.Length - 1);
        Style style = Styles[index];
        if (style.strength <= 0f) return null;
        if (materials[index] != null) return materials[index];

        if (!shaderLoaded)
        {
            shader = Resources.Load<Shader>("RarityAura");
            shaderLoaded = true;
        }
        if (shader == null || !shader.isSupported) return null;

        var material = new Material(shader) { name = "Rarity Aura " + (PlantRarity)index + " (runtime)" };
        material.SetColor("_Color", style.color);
        material.SetFloat("_Strength", style.strength);
        material.SetFloat("_Pulse", style.pulse);
        material.SetFloat("_Dashes", style.dashes);
        materials[index] = material;
        return material;
    }

    private static Mesh GetQuad()
    {
        if (quad != null) return quad;
        quad = new Mesh { name = "Rarity Aura Quad" };
        quad.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quad.RecalculateBounds();
        return quad;
    }
}
