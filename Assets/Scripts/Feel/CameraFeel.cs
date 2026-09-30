using UnityEngine;

// Kameranın tek sahibi. Shake, idle "nefes" ve zoom punch hepsi sabit bir base pozisyonun
// üstüne ofset olarak eklenir; üst üste binen efektler kamerayı asla kaydırmaz.
// Hepsi unscaled time kullanır: shop/menüde timeScale 0 iken de ekran canlı kalır.
[DefaultExecutionOrder(900)]
[RequireComponent(typeof(Camera))]
public class CameraFeel : MonoBehaviour
{
    public static CameraFeel Instance { get; private set; }

    [Header("Shake (trauma)")]
    [SerializeField, Range(0f, 1f)] private float shakeIntensity = 1f;
    [SerializeField, Min(0f)] private float maxShakeOffset = 0.45f;
    [SerializeField, Range(0f, 5f)] private float maxShakeRoll = 1.2f;
    [SerializeField, Min(0.1f)] private float traumaDecay = 1.8f;
    [SerializeField, Min(1f)] private float shakeFrequency = 22f;

    [Header("Idle Breathing")]
    [SerializeField, Min(0f)] private float idleOffset = 0.12f;
    [SerializeField, Range(0f, 2f)] private float idleRoll = 0.15f;
    [SerializeField, Range(0f, 0.05f)] private float idleZoom = 0.008f;
    [SerializeField, Min(0.01f)] private float idleSpeed = 0.18f;
    [Tooltip("Round sırasında idle hareketin gücü. Placing/Selling'de her zaman 0.")]
    [SerializeField, Range(0f, 1f)] private float idleWeightInRound = 0.35f;

    [Header("Zoom Spring")]
    [SerializeField, Min(1f)] private float zoomStiffness = 140f;
    [SerializeField, Range(0.1f, 1f)] private float zoomDampingRatio = 0.45f;
    [SerializeField, Range(0f, 0.2f)] private float roundStartPunch = 0.04f;
    [SerializeField, Range(0f, 0.2f)] private float roundEndFocus = 0.045f;
    [SerializeField, Range(0f, 0.2f)] private float roundEndPunch = 0.05f;
    [SerializeField, Range(0f, 1f)] private float roundEndTrauma = 0.5f;

    [Header("Round End Flash")]
    [SerializeField] private Color roundEndFlashColor = new Color(1f, 0.96f, 0.86f);
    [SerializeField, Range(0f, 1f)] private float roundEndFlashAlpha = 0.28f;

    private Camera cam;
    private Vector3 basePosition;
    private Quaternion baseRotation;
    private float baseSize;
    private bool hasBase;

    private float trauma;
    private float idleWeight = 1f;
    private float zoom, zoomVelocity, zoomTarget;
    private Vector2 shakeOffset, idleOffsetValue;
    private float seed;

    private GameManager subscribedGame;
    private GameStates lastState;

    // Arka plan paralaksı için: kamera düzlemindeki anlık ofset (dünya birimi) ve zoom oranı.
    public Vector2 ViewOffset => shakeOffset + idleOffsetValue;
    public float ZoomOffset { get; private set; }
    public Quaternion BaseWorldRotation =>
        transform.parent != null ? transform.parent.rotation * baseRotation : baseRotation;

    public static void Shake(float amount, float cap = 1f)
    {
        if (Instance != null) Instance.AddTrauma(amount, cap);
    }

    public static void PunchZoom(float amount)
    {
        if (Instance != null) Instance.Punch(amount);
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        // UnityEngine.Random kullanılmaz: oyunun RNG dizisi (crit, kart) görsel efektlerden etkilenmesin.
        seed = 37.1f;
    }

    private void OnEnable()
    {
        Instance = this;
        CaptureBase();
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        RestoreBase();
        if (Instance == this) Instance = null;
    }

    private void CaptureBase()
    {
        basePosition = transform.localPosition;
        baseRotation = transform.localRotation;
        baseSize = cam.orthographic ? cam.orthographicSize : cam.fieldOfView;
        hasBase = true;
    }

    private void RestoreBase()
    {
        if (!hasBase) return;
        transform.localPosition = basePosition;
        transform.localRotation = baseRotation;
        if (cam.orthographic) cam.orthographicSize = baseSize;
        else cam.fieldOfView = baseSize;
    }

    // Küçük olaylar (crit) cap ile sınırlanır: sık tetiklense bile sürekli sarsıntıya dönüşmez.
    public void AddTrauma(float amount, float cap = 1f)
    {
        if (amount <= 0f || trauma >= cap) return;
        trauma = Mathf.Min(cap, trauma + amount);
    }

    // amount: zirvedeki zoom oranı. Pozitif = uzaklaş, negatif = yaklaş.
    public void Punch(float amount)
    {
        float omega = Mathf.Sqrt(zoomStiffness);
        zoomVelocity += amount * omega / ImpulsePeakFactor(zoomDampingRatio);
    }

    // Sönümlü yayın birim darbeye verdiği tepe cevabı (v0/omega cinsinden).
    private static float ImpulsePeakFactor(float zeta)
    {
        zeta = Mathf.Clamp(zeta, 0.05f, 0.99f);
        float root = Mathf.Sqrt(1f - zeta * zeta);
        return Mathf.Exp(-zeta / root * Mathf.Atan2(root, zeta));
    }

    private void TrySubscribe()
    {
        if (subscribedGame != null || GameManager.Instance == null) return;
        subscribedGame = GameManager.Instance;
        subscribedGame.OnGameStateChanged += HandleStateChanged;
        lastState = subscribedGame.CurrentState;
    }

    private void Unsubscribe()
    {
        if (subscribedGame != null) subscribedGame.OnGameStateChanged -= HandleStateChanged;
        subscribedGame = null;
    }

    private void HandleStateChanged(GameStates state)
    {
        GameStates previous = lastState;
        lastState = state;

        if (state == GameStates.Round)
        {
            zoomTarget = 0f;
            Punch(-roundStartPunch);
            return;
        }

        zoomTarget = 0f;
        bool roundJustEnded = previous == GameStates.Round &&
            (state == GameStates.RoundEnd || state == GameStates.CardSelection || state == GameStates.RunComplete || state == GameStates.RoundChoice);
        if (!roundJustEnded) return;

        // Zaman durduğu an: odaklanmış zoom bırakılır, küçük bir sarsıntı ve flaş.
        Punch(roundEndPunch);
        AddTrauma(roundEndTrauma, 0.7f);
        ScreenFlash.Play(roundEndFlashColor, roundEndFlashAlpha, 0.3f);
    }

    private void LateUpdate()
    {
        if (subscribedGame == null) TrySubscribe();

        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
        float time = Time.unscaledTime;
        GameStates state = subscribedGame != null ? subscribedGame.CurrentState : GameStates.Round;

        UpdateZoomTarget(state);
        StepZoomSpring(dt);
        UpdateIdle(state, time, dt);
        UpdateShake(time, dt);
        Apply();
    }

    private void UpdateZoomTarget(GameStates state)
    {
        // Round'un son anlarında zaman yavaşlarken kamera hafifçe odaklanır.
        if (state != GameStates.Round || RoundManager.Instance == null) return;
        zoomTarget = -roundEndFocus * RoundManager.Instance.EndSlowdownProgress;
    }

    private void StepZoomSpring(float dt)
    {
        float omega = Mathf.Sqrt(zoomStiffness);
        float damping = 2f * zoomDampingRatio * omega;
        float acceleration = -zoomStiffness * (zoom - zoomTarget) - damping * zoomVelocity;
        zoomVelocity += acceleration * dt;
        zoom += zoomVelocity * dt;
    }

    private void UpdateIdle(GameStates state, float time, float dt)
    {
        float targetWeight = !GameSettings.CameraMotion || state == GameStates.Placing || state == GameStates.Selling ? 0f
            : state == GameStates.Round ? idleWeightInRound : 1f;
        idleWeight = Mathf.MoveTowards(idleWeight, targetWeight, dt * 1.5f);

        float t = time * idleSpeed;
        idleOffsetValue = new Vector2(
            (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f,
            (Mathf.PerlinNoise(seed + 7.3f, t) - 0.5f) * 2f) * (idleOffset * idleWeight);
    }

    private void UpdateShake(float time, float dt)
    {
        trauma = Mathf.Max(0f, trauma - traumaDecay * dt);
        float shake = trauma * trauma * shakeIntensity * GameSettings.ScreenShake;
        if (shake <= 0f) { shakeOffset = Vector2.zero; return; }

        float t = time * shakeFrequency;
        shakeOffset = new Vector2(
            (Mathf.PerlinNoise(seed + 13.1f, t) - 0.5f) * 2f,
            (Mathf.PerlinNoise(t, seed + 29.7f) - 0.5f) * 2f) * (maxShakeOffset * shake);
    }

    private void Apply()
    {
        float time = Time.unscaledTime;
        float idleRollValue = (Mathf.PerlinNoise(time * idleSpeed * 0.7f, seed + 3.3f) - 0.5f) * 2f * idleRoll * idleWeight;
        float shakeRoll = trauma > 0f
            ? (Mathf.PerlinNoise(seed + 41.9f, time * shakeFrequency) - 0.5f) * 2f * maxShakeRoll * trauma * trauma * shakeIntensity * GameSettings.ScreenShake
            : 0f;
        float idleZoomValue = Mathf.Sin(time * idleSpeed * Mathf.PI) * idleZoom * idleWeight;
        ZoomOffset = zoom + idleZoomValue;

        Vector2 offset = ViewOffset;
        transform.localPosition = basePosition + baseRotation * new Vector3(offset.x, offset.y, 0f);
        transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, idleRollValue + shakeRoll);

        float size = baseSize * Mathf.Max(0.5f, 1f + ZoomOffset);
        if (cam.orthographic) cam.orthographicSize = size;
        else cam.fieldOfView = size;
    }
}
