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

    public void Show()
    {
        hiding = false;
        preparing = false;
        if (visual != null)
        {
            if (!originCaptured) { visualOrigin = visual.localPosition; originCaptured = true; }
            visual.localPosition = visualOrigin;
        }
        ClearHitFlash();
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
        Play(onShow, showTrigger);
    }

    public void SetDamage(float fraction)
    {
        if (damageStages == null) return;
        for (int i = 0; i < damageStages.Length; i++) if (damageStages[i] != null)
            damageStages[i].SetActive(fraction >= (i + 1f) / (damageStages.Length + 1f));
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
        Stop(onShow); Stop(onHit); Stop(onHide); hiding = preparing = false; hitRemaining = 0f;
        if (visual != null && originCaptured) visual.localPosition = visualOrigin;
        petals?.ResetPose();
        shell?.ResetPose();
        bounty?.ResetPose();
        antenna?.ResetPose();
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
            visual.localPosition = visualOrigin + offset;
        }
        if (visual != null && hitRemaining > 0f && !hiding)
        {
            hitRemaining = Mathf.Max(0f, hitRemaining - delta);
            float elapsed = HitSeconds - hitRemaining;
            // Firm compression followed by a smaller rebound, like PlantJuice.
            float squash = elapsed < .20f ? Mathf.Sin(elapsed / .20f * Mathf.PI)
                : -.25f * Mathf.Sin((elapsed - .20f) / .12f * Mathf.PI);
            visual.localScale = Vector3.Scale(visual.localScale, new Vector3(1f + squash * .22f, 1f - squash * .32f, 1f + squash * .22f));
        }
        petals?.Pose(age, hitRemaining / HitSeconds, exitProgress);
        shell?.Step(delta, age);
        bounty?.Step(age);
        antenna?.Step(delta,age);
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



