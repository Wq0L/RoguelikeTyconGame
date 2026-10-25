using UnityEngine;

// Pure presentation. Presenter owns scaled time, pooling and lifetime; never writes gameplay state.
[DisallowMultipleComponent]
public sealed class LateGameContent : MonoBehaviour
{
    public Transform visual, topPoint;
    public LateGamePetals petals;
    public LateGameShell shell;
    public LateGameBounty bounty;
    public LateGameAntenna antenna;
    public LateGameCritBurst critBurst;
    public ParticleSystem[] onShow, onHit, onHide;
    public Animator animator;
    public string showTrigger = "Show", hitTrigger = "Hit", hideTrigger = "Hide";
    [Range(0f, 2f)] public float hideSeconds;
    [Tooltip("Visual-only underground reveal; root and hit area remain fixed.")]
    [Range(0f, 1f)] public float emergeDepth;
    public bool groundRumble;
    private bool preparing, originCaptured;
    private Vector3 visualOrigin;
    public void SetPreparing(bool value) => preparing = value;
    public GameObject[] damageStages;
    public Transform[] fragments;
    public Vector3[] fragmentVelocities;
    private Vector3[] fragmentOrigins;
    private Quaternion[] fragmentRotations;
    private const float HitSeconds = .32f;
    private Renderer[] hitRenderers;
    private float hitRemaining;
    private bool hiding;
    // Güçlü vuruş (24 Ekim 2026, kesilecek hedefler): darbe yönünden geri tepme + eğilme, daha sert basılma, hasar arttıkça öfkeli
    // titreme. Yalnız Hit(away, strength) ile açılır; düz Hit() eski davranıştır. Kök döndürülmez: yön dünya = yerel.
    private Vector3 recoil;
    private float hitStrength = 1f, damage;
    private bool angry;
    private Quaternion visualRotation = Quaternion.identity;
    // Kuruma (25 Ekim 2026, İstilacı Filiz ölüm dalgası): toon gövde renkleri kuru kahverengi-griye karışır (shader'ın _ToonFlash
    // karışımı; mürekkep kabuk değişmez). Paylaşılan tek property block; renderer listesi önbellekli; değer değişmedikçe yazılmaz.
    public static readonly Color WitherColor = new Color32(107, 90, 74, 255);
    private static readonly int ToonFlashId = Shader.PropertyToID("_ToonFlash"), ToonFlashColorId = Shader.PropertyToID("_ToonFlashColor"), InkModeId = Shader.PropertyToID("_InkMode");
    private static MaterialPropertyBlock witherBlock;
    private float withered;
    public float Withered => withered;
    public void SetWither(float amount)
    {
        amount = Mathf.Clamp01(amount);
        if (Mathf.Abs(amount - withered) < .01f) return;
        if (withered == 0f) ClearHitFlash();
        withered = amount;
        if (hitRenderers == null) hitRenderers = GetComponentsInChildren<Renderer>(true);
        witherBlock ??= new MaterialPropertyBlock();
        foreach (var renderer in hitRenderers)
        {
            if (renderer == null || renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(ToonFlashId) || renderer.sharedMaterial.GetFloat(InkModeId) >= .5f) continue;
            witherBlock.Clear();
            witherBlock.SetFloat(ToonFlashId, amount);
            witherBlock.SetColor(ToonFlashColorId, WitherColor);
            renderer.SetPropertyBlock(witherBlock);
        }
    }
    private void ClearWither()
    {
        if (withered == 0f || hitRenderers == null) { withered = 0f; return; }
        withered = 0f;
        foreach (var renderer in hitRenderers) if (renderer != null) renderer.SetPropertyBlock(null);
    }

    public void Show()
    {
        hiding = false;
        preparing = false;
        if (visual != null)
        {
            if (!originCaptured) { visualOrigin = visual.localPosition; visualRotation = visual.localRotation; originCaptured = true; }
            visual.localPosition = visualOrigin;
            visual.localRotation = visualRotation;
        }
        recoil = Vector3.zero; hitStrength = 1f; damage = 0f; angry = false;
        ClearHitFlash();
        ClearWither();
        hitRemaining = 0f;
        Stop(onShow); Stop(onHit); Stop(onHide);
        if (animator != null) { animator.Rebind(); animator.Update(0f); }
        SetDamage(0f);
        if (fragments != null)
        {
            if (fragmentOrigins == null)
            {
                fragmentOrigins = new Vector3[fragments.Length];
                fragmentRotations = new Quaternion[fragments.Length];
                for (int i = 0; i < fragments.Length; i++) if (fragments[i] != null)
                { fragmentOrigins[i] = fragments[i].localPosition; fragmentRotations[i] = fragments[i].localRotation; }
            }
            for (int i = 0; i < fragments.Length; i++) if (fragments[i] != null)
            { fragments[i].localPosition = fragmentOrigins[i]; fragments[i].localRotation = fragmentRotations[i]; }
        }
        petals?.ResetPose();
        shell?.ResetPose();
        bounty?.ResetPose();
        antenna?.ResetPose();
        critBurst?.ResetPose();
        Play(onShow, showTrigger);
    }

    public void SetDamage(float fraction)
    {
        damage = Mathf.Clamp01(fraction);
        if (damageStages == null) return;
        for (int i = 0; i < damageStages.Length; i++) if (damageStages[i] != null)
            damageStages[i].SetActive(fraction >= (i + 1f) / (damageStages.Length + 1f));
    }
    // away: vuruş merkezinden hedefe (yatay); strength > 1 basılmayı büyütür ve öfkeli titremeyi açar.
    public void Hit(Vector3 away, float strength)
    {
        away.y = 0f;
        recoil = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.zero;
        hitStrength = Mathf.Max(1f, strength);
        angry = strength > 1f;
        Hit();
    }
    public void Hit()
    {
        hitRemaining = HitSeconds;
        // Same pooled colour flash as ordinary plants. Cache once; no material instances or new geometry.
        if (hitRenderers == null) hitRenderers = GetComponentsInChildren<Renderer>(true);
        var vfx = VFXManager.Instance;
        if (vfx != null) foreach (var renderer in hitRenderers)
            if (renderer != null && renderer.gameObject.activeInHierarchy && renderer.sharedMaterial != null &&
                renderer.sharedMaterial.HasProperty("_ToonFlash") && renderer.sharedMaterial.GetFloat("_InkMode") < .5f)
                vfx.PlayHitFlash(renderer, Color.white);
        Play(onHit, hitTrigger);
    }
    private void ClearHitFlash()
    {
        if (hitRenderers == null || VFXManager.Instance == null) return;
        foreach (var renderer in hitRenderers) if (renderer != null) VFXManager.Instance.CancelHitFlash(renderer);
    }
    public void Hide() { shell?.SetState(2); hiding = true; Play(onHide, hideTrigger); }
    public void ResetForPool()
    {
        ClearHitFlash();
        ClearWither();
        Stop(onShow); Stop(onHit); Stop(onHide); hiding = preparing = false; hitRemaining = 0f;
        recoil = Vector3.zero; hitStrength = 1f; damage = 0f; angry = false;
        if (visual != null && originCaptured) { visual.localPosition = visualOrigin; visual.localRotation = visualRotation; }
        petals?.ResetPose();
        shell?.ResetPose();
        bounty?.ResetPose();
        antenna?.ResetPose();
        critBurst?.ResetPose();
    }

    // Called once by presenter; child-only squash and deterministic debris (no physics or per-piece Update).
    public void Step(float delta, float age, float emergence = 1f, float exitProgress = 0f)
    {
        if (visual != null)
        {
            Vector3 offset = Vector3.down * (emergeDepth * (1f - emergence));
            if (groundRumble && preparing && !hiding)
            {
                float strength = Mathf.Lerp(.35f, 1f, Mathf.Clamp01(age / 3f));
                float phase = transform.position.x * 1.7f + transform.position.z * 2.3f;
                offset += new Vector3(Mathf.Sin(age * 43f + phase) * .035f,
                    Mathf.Abs(Mathf.Sin(age * 27f + phase)) * .09f,
                    Mathf.Cos(age * 37f + phase) * .035f) * strength;
                visual.localScale = Vector3.Scale(visual.localScale,
                    new Vector3(1f, 1f + strength * .45f * Mathf.Abs(Mathf.Sin(age * 14f + phase)), 1f));
            }
            // Öfkeli titreme: güçlü hedef yarı hasardan sonra sabit desenle (Random yok) giderek sertleşen titreme.
            if (angry && damage > .3f && !hiding)
            {
                float phase = transform.position.x * 1.3f + transform.position.z * 2.1f;
                offset += new Vector3(Mathf.Sin(age * 61f + phase), 0f, Mathf.Cos(age * 53f + phase)) * (.06f * Mathf.InverseLerp(.3f, 1f, damage));
            }
            // Geri tepme: darbe yönünden hızla savrulur, yaylanarak döner; tepe darbeden uzağa eğilir.
            Quaternion tilt = Quaternion.identity;
            if (hitRemaining > 0f && !hiding && recoil != Vector3.zero)
            {
                float e = HitSeconds - hitRemaining;
                float kick = e < .05f ? e / .05f : Mathf.Exp(-(e - .05f) * 11f) * Mathf.Cos((e - .05f) * 24f);
                offset += recoil * (.22f * hitStrength * kick);
                tilt = Quaternion.AngleAxis(14f * hitStrength * kick, Vector3.Cross(Vector3.up, recoil));
            }
            visual.localPosition = visualOrigin + offset;
            if (originCaptured) visual.localRotation = tilt * visualRotation;
        }
        if (visual != null && hitRemaining > 0f && !hiding)
        {
            hitRemaining = Mathf.Max(0f, hitRemaining - delta);
            float elapsed = HitSeconds - hitRemaining;
            // Firm compression followed by a smaller rebound, like PlantJuice. Güçlü vuruşta daha derin (en çok %55).
            float squash = elapsed < .20f ? Mathf.Sin(elapsed / .20f * Mathf.PI)
                : -.25f * Mathf.Sin((elapsed - .20f) / .12f * Mathf.PI);
            visual.localScale = Vector3.Scale(visual.localScale, new Vector3(1f + squash * .22f * hitStrength, Mathf.Max(.45f, 1f - squash * .32f * hitStrength), 1f + squash * .22f * hitStrength));
        }
        petals?.Pose(age, hitRemaining / HitSeconds, exitProgress);
        shell?.Step(delta, age);
        bounty?.Step(age);
        antenna?.Step(delta,age);
        critBurst?.Pose(age);
        if (fragments == null || fragmentOrigins == null) return;
        for (int i = 0; i < fragments.Length; i++)
        {
            if (fragments[i] == null || fragmentVelocities == null || i >= fragmentVelocities.Length) continue;
            fragments[i].localPosition = fragmentOrigins[i] + fragmentVelocities[i] * age + Vector3.down * (2.4f * age * age);
            fragments[i].localRotation = fragmentRotations[i] * Quaternion.Euler(age * (100f + i * 23f), age * 70f, age * 110f);
        }
    }

    private static void Stop(ParticleSystem[] systems)
    {
        if (systems == null) return;
        foreach (var system in systems) if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    private void Play(ParticleSystem[] systems, string trigger)
    {
        if (systems != null) foreach (var system in systems) if (system != null) system.Play(true);
        if (animator != null && !string.IsNullOrEmpty(trigger)) animator.SetTrigger(trigger);
    }
}



