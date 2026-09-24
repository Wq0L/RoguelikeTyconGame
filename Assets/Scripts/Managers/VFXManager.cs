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
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameStates.Round)
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
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameStates.Round) return false;
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

    public void ClearPendingResonances()
    {
        pendingResonances.Clear();
        foreach (var effect in resonancePool) if (effect != null) effect.Stop();
    }

    public void PlayResonance(Bounds footprint, Color color, string message)
    {
        if (resonanceMaterial == null) resonanceMaterial = Resources.Load<Material>("ResonanceBurst");
        if (resonanceMaterial == null) return;
        ResonanceBurst effect = resonancePool.Find(item => item != null && !item.IsPlaying);
        if (effect == null && resonancePool.Count < 16)
        {
            var obj = new GameObject("Resonance Unlock"); obj.transform.SetParent(transform, false);
            effect = obj.AddComponent<ResonanceBurst>(); effect.Configure(resonanceMaterial, floatingTextPrefab);
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

    [Header("Hit Feedback")]
    [SerializeField, Min(1)] private int hitParticleCount = 15;
    [SerializeField] private AudioClip hitSound;
    private readonly AudioSource[] hitVoices = new AudioSource[8];
    private int nextHitVoice;
    private int lastSoundFrame = -1;
    private bool criticalSoundPlayed;

    [Header("Camera Shake")]
    [SerializeField] private float shakeDuration = 0.01f;

    [Header("Optimization")]
    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ToonFlashId = Shader.PropertyToID("_ToonFlash");
    private static readonly int ToonFlashColorId = Shader.PropertyToID("_ToonFlashColor");

    // [Header("Death Particle")]
    // [SerializeField] private ParticleSystem deathParticlePrefab;
    // [SerializeField] private int deathPoolSize = 15;

    [Header("Explosion VFX")]
    [SerializeField] private Transform explosionVfxPrefab;
    [SerializeField, Min(1)] private int explosionPoolSize = 16;
    [SerializeField, Min(0.01f)] private float explosionMainScale = 1f;
    [SerializeField, Min(0.01f)] private float explosionSecondaryScale = 0.5f;
    [SerializeField, Range(0.01f, 1f)] private float explosionStartScaleRatio = 0.5f;
    [SerializeField, Min(0.01f)] private float explosionGrowDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float explosionEmissionDuration = 0.2f;
    private VFXPool<Transform> explosionPool;
    private readonly Dictionary<Transform, ParticleSystem[]> explosionSystems = new();

    // Pool'lar
    private VFXPool<FloatingText> textPool;
    private VFXPool<ParticleSystem> hitPool;
    private sealed class Flash
    {
        public Renderer renderer;
        public readonly MaterialPropertyBlock original = new();
        public readonly MaterialPropertyBlock tinted = new();
        public float remaining;
    }
    private readonly List<Flash> flashes = new();
    private readonly Stack<Flash> spareFlashes = new();
    private sealed class AttackRing
    {
        public LineRenderer line;
        public float age, radius;
        public Color color;
    }
    private readonly AttackRing[] rings = new AttackRing[4];
    private Material ringMaterial;
    private int nextRing;
    // private VFXPool<ParticleSystem> deathPool;
    // private VFXPool<ParticleSystem> explosionPool;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        if (floatingTextPrefab != null)
            textPool = new VFXPool<FloatingText>(floatingTextPrefab, textPoolSize, transform);
        InitializeAttackRings();
        for (int i = 0; i < hitVoices.Length; i++)
        {
            var voice = new GameObject("Pooled Hit Voice " + i);
            voice.transform.SetParent(transform, false);
            var source = voice.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.clip = hitSound;
            hitVoices[i] = source;
        }

        if (hitParticlePrefab != null)
            hitPool = new VFXPool<ParticleSystem>(hitParticlePrefab, hitPoolSize, transform);

        // if (deathParticlePrefab != null)
        //     deathPool = new VFXPool<ParticleSystem>(deathParticlePrefab, deathPoolSize, transform);

        if (explosionVfxPrefab != null)
            explosionPool = new VFXPool<Transform>(explosionVfxPrefab, explosionPoolSize, transform);
    }

    // ========== DIŞ ARAYÜZ (facade) ==========

    public void PlayHit(Vector3 position, int damage, bool isCrit)
    {
        SpawnDamageText(position, damage, isCrit);
        PlayHitSound(isCrit);
    }

    public void PlayHitParticle(Vector3 position, Color color, bool isCrit = false)
    {
        SpawnHitParticle(position, isCrit ? new Color(1f, 0.65f, 0.12f) : color, isCrit);
    }

    public void PlayHitFlash(Renderer renderer, Color flashColor)
    {
        if (renderer == null) return;
        Flash flash = null;
        for (int i = 0; i < flashes.Count; i++)
            if (flashes[i].renderer == renderer) { flash = flashes[i]; break; }
        if (flash == null)
        {
            flash = spareFlashes.Count > 0 ? spareFlashes.Pop() : new Flash();
            flash.renderer = renderer;
            renderer.GetPropertyBlock(flash.original);
            renderer.GetPropertyBlock(flash.tinted);
            flashes.Add(flash);
        }
        flash.remaining = flashDuration;
        flash.tinted.SetColor(ColorId, flashColor);
        flash.tinted.SetFloat(ToonFlashId, 1f);
        flash.tinted.SetColor(ToonFlashColorId, flashColor);
        renderer.SetPropertyBlock(flash.tinted);
    }

    public void CancelHitFlash(Renderer renderer)
    {
        for (int i = flashes.Count - 1; i >= 0; i--)
            if (flashes[i].renderer == renderer) ReleaseFlash(i);
    }

    private void ReleaseFlash(int index)
    {
        var flash = flashes[index];
        if (flash.renderer != null)
            flash.renderer.SetPropertyBlock(flash.original.isEmpty ? null : flash.original);
        flash.renderer = null;
        flash.original.Clear(); flash.tinted.Clear();
        flashes[index] = flashes[flashes.Count - 1];
        flashes.RemoveAt(flashes.Count - 1);
        spareFlashes.Push(flash);
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

    public void PlayExplosion(Vector3 position, bool isSource = true)
    {
        if (explosionPool == null) return;
        Transform effect = explosionPool.Get();
        if (!explosionSystems.TryGetValue(effect, out var systems))
        {
            systems = effect.GetComponentsInChildren<ParticleSystem>(true);
            explosionSystems.Add(effect, systems);
        }
        float targetScale = isSource ? explosionMainScale : explosionSecondaryScale;
        effect.SetPositionAndRotation(position, Quaternion.identity);
        effect.localScale = Vector3.one * (targetScale * explosionStartScaleRatio);
        foreach (var particle in systems)
        {
            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particle.main;
            main.stopAction = ParticleSystemStopAction.None;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.prewarm = false;
            particle.Play(false);
        }
        StartCoroutine(ExplosionRoutine(effect, systems, targetScale));
    }

    private IEnumerator ExplosionRoutine(Transform effect, ParticleSystem[] systems, float targetScale)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(explosionGrowDuration, explosionEmissionDuration);
        bool stopped = false;
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, explosionGrowDuration));
            float eased = 1f - (1f - t) * (1f - t);
            effect.localScale = Vector3.one * Mathf.Lerp(targetScale * explosionStartScaleRatio, targetScale, eased);
            if (!stopped && elapsed >= explosionEmissionDuration)
            {
                foreach (var particle in systems)
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                stopped = true;
            }
        }
        bool alive;
        do
        {
            alive = false;
            foreach (var particle in systems) alive |= particle.IsAlive(false);
            if (alive) yield return null;
        } while (alive);
        foreach (var particle in systems)
            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        effect.localScale = Vector3.one;
        explosionPool.Return(effect);
    }

    private void SpawnDamageText(Vector3 position, int damage, bool isCrit)
    {
        if (textPool == null) return;
        FloatingText text = textPool.Get();
        text.transform.position = position + Vector3.up;
        text.Show(damage, isCrit);
    }

    private void SpawnHitParticle(Vector3 position, Color color, bool isCrit)
    {
        if (hitPool == null) return;

        ParticleSystem p = hitPool.Get();
        p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        p.transform.position = position;

        var main = p.main;
        main.startColor = color;
        main.maxParticles = Mathf.Max(main.maxParticles, hitParticleCount * 3);
        var emission = p.emission;
        emission.enabled = false; // Emit exactly one burst; no duplicate prefab burst.
        p.Play();
        p.Emit(hitParticleCount * (isCrit ? 3 : 1));
        StartCoroutine(ReturnHitParticleWhenFinished(p));
    }

    private IEnumerator ReturnHitParticleWhenFinished(ParticleSystem particle)
    {
        while (particle != null && particle.IsAlive(true)) yield return null;
        if (particle != null) hitPool.Return(particle);
    }

    private void PlayHitSound(bool isCrit)
    {
        if (hitSound == null) return;
        if (lastSoundFrame != Time.frameCount)
        {
            lastSoundFrame = Time.frameCount;
            criticalSoundPlayed = false;
        }
        else if (!isCrit || criticalSoundPlayed) return;
        criticalSoundPlayed |= isCrit;
        var voice = hitVoices[nextHitVoice++ % hitVoices.Length];
        voice.Stop();
        voice.pitch = isCrit ? Random.Range(0.72f, 0.8f) : Random.Range(1.2f, 1.35f);
        voice.volume = isCrit ? 0.55f : 0.22f;
        voice.Play();
    }

    private IEnumerator ShakeRoutine(float magnitude)
    {
        if (Camera.main == null) yield break;
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
        if (text != null && text.gameObject.activeSelf) textPool.Return(text);
    }

    //player Attack
    public void PlayAttackRing(Vector3 center, float maxRadius, bool hasCrit = false)
    {
        var ring = rings[nextRing];
        nextRing = (nextRing + 1) % rings.Length;
        if (ring == null) return;
        ring.age = 0f; ring.radius = maxRadius;
        ring.color = hasCrit ? new Color(1f, 0.3f, 0f) : Color.yellow;
        ring.line.transform.position = center + Vector3.up * .1f;
        ring.line.transform.localScale = new Vector3(.3f, 1f, .3f);
        ring.line.startWidth = ring.line.endWidth = hasCrit ? .15f : .1f;
        ring.line.startColor = ring.color;
        ring.line.endColor = new Color(ring.color.r, ring.color.g, ring.color.b, 0f);
        ring.line.gameObject.SetActive(true);
        ShakeCamera(hasCrit ? 0.05f : 0.02f); // crit varsa daha güçlü shake
    }

    private void InitializeAttackRings()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return;
        ringMaterial = new Material(shader) { name = "Shared Attack Ring (runtime)" };
        var circle = new Vector3[32];
        for (int i = 0; i < circle.Length; i++)
        {
            float angle = i * Mathf.PI * 2f / circle.Length;
            circle[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
        for (int i = 0; i < rings.Length; i++)
        {
            var obj = new GameObject("Pooled AttackRing " + i);
            obj.transform.SetParent(transform, false);
            var line = obj.AddComponent<LineRenderer>();
            line.sharedMaterial = ringMaterial;
            line.loop = true; line.useWorldSpace = false;
            line.positionCount = circle.Length; line.SetPositions(circle);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            rings[i] = new AttackRing { line = line };
            obj.SetActive(false);
        }
    }

    private void Update()
    {
        for (int i = flashes.Count - 1; i >= 0; i--)
        {
            var flash = flashes[i];
            flash.remaining -= Time.deltaTime;
            if (flash.renderer == null || flash.remaining <= 0f) ReleaseFlash(i);
        }
        foreach (var ring in rings)
        {
            if (ring == null || !ring.line.gameObject.activeSelf) continue;
            ring.age += Time.deltaTime;
            if (ring.age >= .15f) { ring.line.gameObject.SetActive(false); continue; }
            float t = ring.age / .15f;
            float radius = Mathf.Lerp(.3f, ring.radius, t);
            ring.line.transform.localScale = new Vector3(radius, 1f, radius);
            ring.line.startColor = new Color(ring.color.r, ring.color.g, ring.color.b, 1f - t);
        }
    }

    private void OnDisable()
    {
        for (int i = flashes.Count - 1; i >= 0; i--) ReleaseFlash(i);
        foreach (var ring in rings) if (ring?.line != null) ring.line.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (ringMaterial != null) Destroy(ringMaterial);
        if (Instance == this) Instance = null;
    }
}
