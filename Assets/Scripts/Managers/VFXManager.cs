using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }
    private readonly List<ResonanceBurst> resonancePool = new();
    private Material resonanceMaterial;
    private int resonanceRecycleIndex;
    private readonly Dictionary<PlanterBrain, List<ActiveResonance>> pendingResonances = new();
    public bool HasPendingResonances => pendingResonances.Count > 0;

    public void RequestResonance(PlanterBrain planter, IReadOnlyList<ActiveResonance> previous)
    {
        if (planter == null || !planter.TryGetResonancePresentation(previous, out var bounds,
            out var color, out var message)) return;
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameStates.CardSelection)
        {
            // Keep the first baseline; subsequent cards are presented as one final upgrade per planter.
            if (!pendingResonances.ContainsKey(planter))
                pendingResonances.Add(planter, new List<ActiveResonance>(previous));
            return;
        }
        PlayResonance(bounds, color, message);
    }

    public bool PlayPendingResonances()
    {
        bool played = false;
        foreach (var pending in pendingResonances)
        {
            if (pending.Key == null || !pending.Key.TryGetResonancePresentation(pending.Value,
                out var bounds, out var color, out var message)) continue;
            PlayResonance(bounds, color, message);
            played = true;
        }
        pendingResonances.Clear();
        return played;
    }

    public void ClearPendingResonances() => pendingResonances.Clear();

    public void PlayResonance(Bounds footprint, Color color, string message)
    {
        if (resonanceMaterial == null) resonanceMaterial = Resources.Load<Material>("ResonanceBurst");
        if (resonanceMaterial == null) return;
        ResonanceBurst effect = resonancePool.Find(item => item != null && !item.IsPlaying);
        if (effect == null && resonancePool.Count < 16)
        {
            var obj = new GameObject("Resonance Unlock"); obj.transform.SetParent(transform, false);
            effect = obj.AddComponent<ResonanceBurst>(); effect.Configure(resonanceMaterial);
            resonancePool.Add(effect);
        }
        if (effect == null) effect = resonancePool[resonanceRecycleIndex++ % resonancePool.Count];
        effect.Play(footprint, color, message);
    }

    [Header("Floating Text")]
    [SerializeField] private FloatingText floatingTextPrefab;
    [SerializeField] private int textPoolSize = 20;

    [Header("Hit Flash")]
    [SerializeField] private float flashDuration = 0.1f;

    [Header("Hit Particle")]
    [SerializeField] private ParticleSystem hitParticlePrefab;
    [SerializeField] private int hitPoolSize = 15;

    [Header("Camera Shake")]
    [SerializeField] private float shakeDuration = 0.2f;

    [Header("Optimization")]
    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    // [Header("Death Particle")]
    // [SerializeField] private ParticleSystem deathParticlePrefab;
    // [SerializeField] private int deathPoolSize = 15;

    // [Header("Explosion Particle")]
    // [SerializeField] private ParticleSystem explosionParticlePrefab;
    // [SerializeField] private int explosionPoolSize = 10;

    // Pool'lar
    private VFXPool<FloatingText> textPool;
    private VFXPool<ParticleSystem> hitPool;
    // private VFXPool<ParticleSystem> deathPool;
    // private VFXPool<ParticleSystem> explosionPool;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        textPool = new VFXPool<FloatingText>(floatingTextPrefab, textPoolSize, transform);

        if (hitParticlePrefab != null)
            hitPool = new VFXPool<ParticleSystem>(hitParticlePrefab, hitPoolSize, transform);

        // if (deathParticlePrefab != null)
        //     deathPool = new VFXPool<ParticleSystem>(deathParticlePrefab, deathPoolSize, transform);

        // if (explosionParticlePrefab != null)
        //     explosionPool = new VFXPool<ParticleSystem>(explosionParticlePrefab, explosionPoolSize, transform);
    }

    // ========== DIŞ ARAYÜZ (facade) ==========

    public void PlayHit(Vector3 position, int damage, bool isCrit)
    {
        SpawnDamageText(position, damage, isCrit);
    }

    public void PlayHitParticle(Vector3 position, Color color)
    {
        SpawnHitParticle(position, color);
    }

    public void PlayHitFlash(Renderer renderer, Color flashColor)
    {
        if (renderer != null)
            StartCoroutine(FlashRoutine(renderer, flashColor));
    }

    public void ShakeCamera(float magnitude = 0.3f)
    {
        StartCoroutine(ShakeRoutine(magnitude));
    }

    // public void PlayDeath(Vector3 position, Color color)
    // {
    //     SpawnDeathParticle(position, color);
    // }

    // public void PlayExplosion(Vector3 position)
    // {
    //     SpawnExplosionParticle(position);
    //     ShakeCamera(0.4f);
    // }

    // ========== İÇ İŞLER ==========

    private void SpawnDamageText(Vector3 position, int damage, bool isCrit)
    {
        FloatingText text = textPool.Get();
        text.transform.position = position + Vector3.up;
        text.Show(damage, isCrit);
    }

    private void SpawnHitParticle(Vector3 position, Color color)
    {
        Debug.Log($"SpawnHitParticle çağrıldı | hitPool null mı: {hitPool == null}");
        if (hitPool == null) return;

        ParticleSystem p = hitPool.Get();
        p.transform.position = position;

        var main = p.main;
        main.startColor = color;

        p.Play();
        StartCoroutine(ReturnParticleAfter(p, hitPool, main.duration));
    }

    private IEnumerator FlashRoutine(Renderer renderer, Color flashColor)
    {
        if (renderer == null) yield break;

        // Her coroutine kendi local MPB'sini kullanır — paylaşım yok
        MaterialPropertyBlock localMpb = new MaterialPropertyBlock();

        renderer.GetPropertyBlock(localMpb);
        localMpb.SetColor(ColorId, flashColor);
        renderer.SetPropertyBlock(localMpb);

        yield return new WaitForSeconds(flashDuration);

        if (renderer != null)
        {
            localMpb.Clear();
            renderer.SetPropertyBlock(localMpb);
        }
    }

    private IEnumerator ShakeRoutine(float magnitude)
    {
        Transform cam = Camera.main.transform;
        Vector3 originalPos = cam.localPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            cam.localPosition = originalPos + new Vector3(x, y, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }

        cam.localPosition = originalPos;
    }

    private IEnumerator ReturnParticleAfter(
        ParticleSystem p, VFXPool<ParticleSystem> pool, float delay)
    {
        yield return new WaitForSeconds(delay);
        pool.Return(p);
    }

    // private void SpawnDeathParticle(Vector3 position, Color color)
    // {
    //     if (deathPool == null) return;
    //     ParticleSystem p = deathPool.Get();
    //     p.transform.position = position;
    //     var main = p.main;
    //     main.startColor = color;
    //     p.Play();
    //     StartCoroutine(ReturnParticleAfter(p, deathPool, main.duration));
    // }

    // private void SpawnExplosionParticle(Vector3 position)
    // {
    //     if (explosionPool == null) return;
    //     ParticleSystem p = explosionPool.Get();
    //     p.transform.position = position;
    //     p.Play();
    //     StartCoroutine(ReturnParticleAfter(p, explosionPool, p.main.duration));
    // }

    // ========== POOL GERİ DÖNÜŞ ==========

    public void ReturnText(FloatingText text)
    {
        textPool.Return(text);
    }

    //player Attack
    public void PlayAttackRing(Vector3 center, float maxRadius, bool hasCrit = false)
    {
        StartCoroutine(AttackRingRoutine(center, maxRadius, hasCrit));
        ShakeCamera(hasCrit ? 0.15f : 0.05f); // crit varsa daha güçlü shake
    }

    private IEnumerator AttackRingRoutine(Vector3 center, float maxRadius, bool hasCrit)
    {
        Color ringColor = hasCrit ? new Color(1f, 0.3f, 0f) : Color.yellow;

        GameObject ringObj = new GameObject("AttackRing");
        LineRenderer ring = ringObj.AddComponent<LineRenderer>();

        ring.loop = true;
        ring.useWorldSpace = true;
        ring.positionCount = 32;
        ring.startWidth = hasCrit ? 0.15f : 0.1f;
        ring.endWidth = hasCrit ? 0.15f : 0.1f;
        ring.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float currentRadius = Mathf.Lerp(0.3f, maxRadius, t);

            for (int i = 0; i < 32; i++)
            {
                float angle = (float)i / 32 * Mathf.PI * 2f;
                float x = center.x + Mathf.Cos(angle) * currentRadius;
                float z = center.z + Mathf.Sin(angle) * currentRadius;
                ring.SetPosition(i, new Vector3(x, center.y + 0.1f, z));
            }

            float alpha = 1f - t;
            ring.startColor = new Color(ringColor.r, ringColor.g, ringColor.b, alpha);
            ring.endColor = new Color(ringColor.r, ringColor.g, ringColor.b, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(ringObj);
    }
}
