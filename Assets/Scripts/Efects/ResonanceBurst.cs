using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

// Reusable effect whose readable lifetime is spent only in the visible round.
public class ResonanceBurst : MonoBehaviour
{
    private LineRenderer ring;
    private ParticleSystem sparks;
    private TextMeshPro label;
    private Vector3 halfSize;
    private Color tint;
    private float elapsed;
    public const float Duration = 3f;
    private bool presentationPaused;
    private Vector3 textTravel;
    private float textTilt;
    private static bool scatterRight;
    public bool IsPlaying => gameObject.activeSelf;

    public void Configure(Material material, FloatingText damageStyle = null)
    {
        ring = gameObject.AddComponent<LineRenderer>();
        ring.sharedMaterial = material;
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = 48;
        ring.shadowCastingMode = ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.lightProbeUsage = LightProbeUsage.Off;
        ring.reflectionProbeUsage = ReflectionProbeUsage.Off;
        var particles = new GameObject("Unlock sparks");
        particles.transform.SetParent(transform, false);
        sparks = particles.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sparks.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = .7f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .7f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(.045f, .09f);
        main.gravityModifier = .15f;
        main.useUnscaledTime = true;
        main.maxParticles = 24;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = sparks.emission; emission.enabled = false;
        var shape = sparks.shape; shape.enabled = false;
        var color = sparks.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var renderer = sparks.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        var text = new GameObject("Resonance label"); text.transform.SetParent(transform, false);
        label = text.AddComponent<TextMeshPro>();
        var theme = Resources.Load<ComicUITheme>("ComicUITheme");
        if (theme != null) { label.font = theme.headingFont; label.fontSharedMaterial = theme.outlinedText; }
        label.fontSize = 3.6f;
        if (damageStyle != null) damageStyle.CopyStyleTo(label);
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(9f, 4f);
        label.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        gameObject.SetActive(false);
    }

    public void Play(Bounds footprint, Color color, string message)
    {
        transform.position = footprint.center + Vector3.up * .22f;
        halfSize = new Vector3(Mathf.Max(.5f, footprint.extents.x + .45f), 0f,
            Mathf.Max(.5f, footprint.extents.z + .45f));
        tint = color;
        scatterRight = !scatterRight;
        var camera = Camera.main;
        textTilt = scatterRight ? 4f : -4f;
        textTravel = (camera != null ? camera.transform.right : Vector3.right) * (scatterRight ? .45f : -.45f)
            + (camera != null ? camera.transform.up : Vector3.up) * 1.35f;
        label.text = message;
        int lines = message.Split('\n').Length;
        label.enableAutoSizing = true;
        label.fontSizeMin = 1.4f;
        label.fontSizeMax = 3.6f;
        label.rectTransform.sizeDelta = new Vector2(11f, Mathf.Max(4f, lines * .45f));
        label.color = Color.white;
        elapsed = 0f;
        presentationPaused = false;
        ring.enabled = true;
        label.GetComponent<Renderer>().enabled = true;
        sparks.GetComponent<Renderer>().enabled = true;
        gameObject.SetActive(true);
        sparks.Clear(true);
        sparks.Play();
        for (int i = 0; i < 20; i++)
        {
            float angle = i * Mathf.PI * 2f / 20;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var emit = new ParticleSystem.EmitParams {
                position = transform.position + Vector3.Scale(direction, halfSize) * .65f,
                velocity = direction * .8f + Vector3.up * (1.2f + (i % 3) * .25f),
                startColor = color
            };
            sparks.Emit(emit, 1);
        }
        Draw(0f);
    }

    private void Update()
    {
        bool pause = GameManager.Instance != null && GameManager.Instance.CurrentState != GameStates.Round;
        if (pause != presentationPaused)
        {
            presentationPaused = pause;
            ring.enabled = !pause;
            label.GetComponent<Renderer>().enabled = !pause;
            sparks.GetComponent<Renderer>().enabled = !pause;
            if (pause) sparks.Pause(true); else sparks.Play(true);
        }
        if (pause) return;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= Duration) { Stop(); return; }
        Draw(elapsed / Duration);
    }

    private void Draw(float t)
    {
        float expansion = Mathf.Lerp(.65f, 1.3f, 1f - Mathf.Pow(1f - t, 3f));
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / ring.positionCount;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * halfSize.x * expansion, 0,
                Mathf.Sin(angle) * halfSize.z * expansion));
        }
        ring.startWidth = ring.endWidth = Mathf.Lerp(.16f, .025f, t);
        Color color = tint; color.a = (1f - t) * .9f;
        ring.startColor = ring.endColor = color;
        label.transform.position = transform.position + Vector3.up * 1.65f + textTravel * (1f - Mathf.Pow(1f - t, 3f));
        float enter = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / .055f), 3f);
        float punch = Mathf.Clamp01((elapsed - .055f) / .22f);
        float pop = enter * (1f + .3f * Mathf.Sin(punch * Mathf.PI));
        label.transform.localScale = Vector3.one * pop;
        label.alpha = 1f - Mathf.InverseLerp(.8f, 1f, t);
        if (Camera.main != null) label.transform.rotation = Camera.main.transform.rotation * Quaternion.Euler(0, 0, textTilt);
    }

    public void Stop()
    {
        if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        gameObject.SetActive(false);
    }
}
