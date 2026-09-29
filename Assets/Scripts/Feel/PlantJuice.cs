using UnityEngine;

// Bitkinin görsel esnekliği: topraktan esneyerek fırlama (spawn) ve her vuruşta ezilip yaylanarak geri gelme.
// Sadece görsel modelin ölçeğine dokunur; root, collider ve aura değişmez. Oyun zamanıyla çalışır
// (dünya donunca durur). Boştayken Update kapalıdır: yüzlerce bitkide maliyet yok.
[DisallowMultipleComponent]
public sealed class PlantJuice : MonoBehaviour
{
    private const float SpawnDuration = 0.42f;
    private const float HitDuration = 0.32f;

    private Transform visual;
    private Vector3 rest;
    private int upAxis = 1;
    private PlantHealth health;
    private float spawnStart, hitStart, hitStrength;
    private bool spawning, hitting;

    // Bitki aktif edildikten sonra, aura eklenmeden önce çağrılmalı (görsel model doğru bulunsun).
    public static PlantJuice Attach(GameObject plant)
    {
        if (!plant.TryGetComponent(out PlantJuice juice)) juice = plant.AddComponent<PlantJuice>();
        return juice;
    }

    private void Awake()
    {
        visual = FindVisualRoot();
        rest = visual.localScale;
        upAxis = FindUpAxis(visual);
        if (TryGetComponent(out health)) health.OnDamaged += Hit;
        enabled = false;
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDamaged -= Hit;
    }

    // Model renderer'ını içeren en üst çocuk: çok parçalı modellerde hepsi birlikte esner.
    private Transform FindVisualRoot()
    {
        foreach (Renderer candidate in GetComponentsInChildren<Renderer>(true))
        {
            Transform node = candidate.transform;
            if (node.name == "Rarity Aura") continue;
            if (node == transform) return transform;
            while (node.parent != transform) node = node.parent;
            return node;
        }
        return transform;
    }

    // Modelin hangi yerel ekseni dünyada yukarı bakıyor (FBX'ler çoğu zaman döndürülmüş gelir).
    private static int FindUpAxis(Transform node)
    {
        float x = Mathf.Abs(Vector3.Dot(node.right, Vector3.up));
        float y = Mathf.Abs(Vector3.Dot(node.up, Vector3.up));
        float z = Mathf.Abs(Vector3.Dot(node.forward, Vector3.up));
        return y >= x && y >= z ? 1 : x >= z ? 0 : 2;
    }

    public void PlaySpawn()
    {
        spawning = true;
        spawnStart = Time.time;
        Apply(0.05f, 0.05f); // ilk karede tam boy görünmesin
        enabled = true;
    }

    private void Hit(bool crit)
    {
        hitting = true;
        hitStart = Time.time;
        hitStrength = crit ? 1.45f : 1f;
        enabled = true;
    }

    private void Update()
    {
        float vertical = 1f, horizontal = 1f;
        if (spawning)
        {
            float t = (Time.time - spawnStart) / SpawnDuration;
            if (t >= 1f) spawning = false;
            else
            {
                // Hızla büyür, önce uzar sonra ezilir, yerine oturur.
                float grow = OutBack(t);
                float wobble = Mathf.Sin(t * Mathf.PI * 2.4f) * (1f - t) * 0.22f;
                vertical *= grow * (1f + wobble);
                horizontal *= grow * (1f - wobble * 0.55f);
            }
        }
        if (hitting)
        {
            float t = (Time.time - hitStart) / HitDuration;
            if (t >= 1f) hitting = false;
            else
            {
                // Sönümlü yay: ezilir (alçalıp genişler), sonra hafifçe uzayarak durulur.
                float spring = Mathf.Exp(-t * 6f) * Mathf.Cos(t * 21f) * hitStrength;
                vertical *= 1f - 0.2f * spring;
                horizontal *= 1f + 0.12f * spring;
            }
        }

        Apply(vertical, horizontal);
        if (!spawning && !hitting)
        {
            visual.localScale = rest;
            enabled = false;
        }
    }

    private void Apply(float vertical, float horizontal)
    {
        Vector3 scale = rest * horizontal;
        scale[upAxis] = rest[upAxis] * vertical;
        visual.localScale = scale;
    }

    private static float OutBack(float k)
    {
        const float c1 = 1.9f, c3 = c1 + 1f;
        float x = k - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    // Havuza dönerken veya bileşen kapanırken daima dinlenme ölçeğine döner.
    private void OnDisable()
    {
        spawning = hitting = false;
        if (visual != null) visual.localScale = rest;
    }
}
