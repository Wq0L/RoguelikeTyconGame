using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

// Açılış videosu (Docs/AcilisVideosu-Brief.md): ilk "Oyna"da, seçim ekranından önce bir kez oynar.
// Space basılı tutulursa sağ alttaki çubuk dolar ve video geçilir. Video karanlığa indiğinde (14,75 sn) seçim ekranı açılır,
// sesin kuyruğu (kask, kapak) seçim ekranının üstünde biter. Bitince ya da geçilince DialogueProgress.MarkIntroSeen.
// Dosya: StreamingAssets/Intro/ErigoneIntro.webm (Tools/Art/IntroAnimatic/export_intro_unity.py; VP8 + Vorbis, telifsiz).
// Kaynaklar tek nesnede: RenderTexture bir kez ayrılır, kapanışta bırakılır; karede bellek ayırma yok.
public sealed class IntroVideoPlayer : MonoBehaviour
{
    public const string FileName = "Intro/ErigoneIntro.webm";
    public const float HoldToSkipSeconds = 1f;
    private const double RevealTime = 354 / 24.0;                 // son kare mürekkebe karardı: seçim ekranı açılır
    private const float RevealFade = .25f, SkipFade = .2f, AudioFadeOnSkip = .35f, PrepareTimeout = 8f, HintDelay = .8f;
    private static readonly Color Ink = new Color32(54, 39, 54, 255), InkDark = new Color32(40, 29, 43, 220);
    private static readonly Color Paper = new Color32(255, 242, 210, 255), Amber = new Color32(247, 192, 93, 255);
    private static readonly Color Backdrop = new Color32(26, 19, 36, 255);   // videonun karardığı mürekkep (0x1A1324)

    private static bool failedThisSession;
    public static bool IsPlaying { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { failedThisSession = false; IsPlaying = false; }

    private VideoPlayer video;
    private AudioSource audioSource;
    private RenderTexture target;
    private CanvasGroup visuals, hint;
    private RectTransform track, fill;
    private RawImage screen;
    private Action onReveal;
    private EventSystem navigationOwner;
    private bool navigationWas, prepared, revealed, skipping;
    private float age, hold, fadeAge, audioFade = 1f;

    public float SkipProgress => Mathf.Clamp01(hold / HoldToSkipSeconds);
    public bool Revealed => revealed;

    public static string FilePath => Path.Combine(Application.streamingAssetsPath, FileName);

    // Döndürür: video başladı mı. false ise çağıran hemen kendi yoluna devam eder (dosya yok, önceden hata verdi, zaten oynuyor).
    public static bool Play(Action onReveal)
    {
        if (IsPlaying || failedThisSession) return false;
#if !UNITY_WEBGL || UNITY_EDITOR
        if (!File.Exists(FilePath)) { Debug.LogWarning("Açılış videosu bulunamadı: " + FilePath); return false; }
#endif
        RectTransform root = FeelOverlay.CreateCanvas("Intro Video", 30000);
        root.gameObject.AddComponent<GraphicRaycaster>();   // video sürerken alttaki menü tıklanmaz
        var player = root.gameObject.AddComponent<IntroVideoPlayer>();
        player.onReveal = onReveal;
        player.Build(root);
        return true;
    }

    private void Build(RectTransform root)
    {
        IsPlaying = true;
        visuals = root.gameObject.AddComponent<CanvasGroup>();
        var back = Child<Image>(root, "Backdrop"); FeelOverlay.Stretch(back.rectTransform); back.color = Backdrop;
        target = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32) { name = "Intro Video (runtime)" };
        screen = Child<RawImage>(root, "Video"); FeelOverlay.Stretch(screen.rectTransform);
        screen.texture = target; screen.raycastTarget = false;
        screen.enabled = false;   // ilk kare gelene kadar boş doku görünmesin
        var fit = screen.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 16f / 9f;
        BuildHint(root);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false; audioSource.spatialBlend = 0f;
        video = gameObject.AddComponent<VideoPlayer>();
        video.playOnAwake = false; video.isLooping = false; video.waitForFirstFrame = true; video.skipOnDrop = true;
        video.source = VideoSource.Url; video.url = FilePath;
        video.renderMode = VideoRenderMode.RenderTexture; video.targetTexture = target;
        video.audioOutputMode = VideoAudioOutputMode.AudioSource;
        video.controlledAudioTrackCount = 1; video.EnableAudioTrack(0, true); video.SetTargetAudioSource(0, audioSource);
        video.prepareCompleted += OnPrepared;
        video.errorReceived += OnError;
        video.loopPointReached += OnEnded;
        ApplyVolume();
        SuspendNavigation();
        video.Prepare();
    }

    // Sağ alt: comic plaka, Space tuşu ikonu + "basılı tut: geç", altında dolan kehribar çubuk.
    private void BuildHint(RectTransform root)
    {
        var plate = new GameObject("Skip Hint", typeof(RectTransform)).GetComponent<RectTransform>();
        plate.SetParent(root, false);
        plate.anchorMin = plate.anchorMax = plate.pivot = new Vector2(1f, 0f);
        plate.anchoredPosition = new Vector2(-56f, 52f); plate.sizeDelta = new Vector2(360f, 96f);
        plate.localRotation = Quaternion.Euler(0f, 0f, 1.2f);   // kesik kâğıt: hafif eğik
        hint = plate.gameObject.AddComponent<CanvasGroup>(); hint.alpha = 0f;
        hint.blocksRaycasts = false; hint.interactable = false;
        Plate(plate, "Shadow", InkDark, new Vector2(7f, -8f), new Vector2(7f, -8f));
        Plate(plate, "Ink", Ink, Vector2.zero, Vector2.zero);
        Plate(plate, "Paper", Paper, new Vector2(5f, 5f), new Vector2(-5f, -5f));

        var label = Child<TextMeshProUGUI>(plate, "Label");
        FeelOverlay.Theme?.StylePopupText(label);
        KeyPrompts.Apply(label);
        label.color = Ink; label.fontSize = 28f; label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.raycastTarget = false;
        label.text = KeyPrompts.Tag(KeyPrompts.Space) + "  basılı tut: geç";
        var lr = label.rectTransform; lr.anchorMin = new Vector2(0f, 1f); lr.anchorMax = Vector2.one; lr.pivot = new Vector2(.5f, 1f);
        lr.anchoredPosition = new Vector2(0f, -12f); lr.sizeDelta = new Vector2(-28f, 52f);

        track = Plate(plate, "Track", Ink, new Vector2(22f, 16f), new Vector2(-22f, 0f)).rectTransform;
        track.anchorMax = new Vector2(1f, 0f); track.offsetMax = new Vector2(-22f, 32f);
        fill = Plate(track, "Fill", Amber, Vector2.zero, Vector2.zero).rectTransform;
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.pivot = new Vector2(0f, .5f);
        fill.offsetMin = new Vector2(3f, 3f); fill.offsetMax = new Vector2(3f, -3f);
    }

    private void OnPrepared(VideoPlayer source)
    {
        prepared = true;
        source.Play();
    }

    private void OnError(VideoPlayer source, string message)
    {
        Debug.LogWarning("Açılış videosu oynatılamadı: " + message);
        failedThisSession = true;
        Reveal(markSeen: false);
        Close();
    }

    private void OnEnded(VideoPlayer source)
    {
        Reveal(markSeen: true);
        Close();
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
        age += dt;
        if (!prepared && age > PrepareTimeout) { OnError(video, "hazırlık zaman aşımı"); return; }
        ApplyVolume();
        // Arka plan sessizliği (GameSettings.MuteInBackground) sesi dondurur; görüntü de beklesin, kayma olmasın.
        if (prepared && video != null)
        {
            if (AudioListener.pause && video.isPlaying) video.Pause();
            else if (!AudioListener.pause && video.isPaused && !skipping) video.Play();
        }

        if (!revealed && !skipping)
        {
            bool held = Input.GetKey(KeyCode.Space);
            hold = held ? hold + dt : Mathf.Max(0f, hold - dt * 2f);
            if (hold >= HoldToSkipSeconds) Skip();
        }
        if (!screen.enabled && prepared && video.frame > 0) screen.enabled = true;
        fill.sizeDelta = new Vector2(Mathf.Max(0f, track.rect.width - 6f) * SkipProgress, fill.sizeDelta.y);
        if (hint != null && !revealed && !skipping) hint.alpha = Mathf.Clamp01((age - HintDelay) / .35f) * (hold > 0f ? 1f : .9f);

        if (!revealed && prepared && video.time >= RevealTime) Reveal(markSeen: true);
        if (revealed || skipping)
        {
            fadeAge += dt;
            visuals.alpha = 1f - Mathf.Clamp01(fadeAge / (skipping ? SkipFade : RevealFade));
            if (visuals.alpha <= 0f) visuals.blocksRaycasts = false;   // ses kuyruğu sürerken seçim ekranı tıklanır
            // Güvenlik: bitiş olayı gelmezse ses kuyruğundan (1,5 sn) sonra kapan.
            if (!skipping && fadeAge > 2.5f) { Close(); return; }
        }
        if (skipping)
        {
            audioFade = 1f - Mathf.Clamp01(fadeAge / AudioFadeOnSkip);
            if (audioFade <= 0f) Close();
        }
    }

    public void Skip()
    {
        if (skipping) return;
        skipping = true;
        fadeAge = 0f;
        Reveal(markSeen: true);
    }

    private void Reveal(bool markSeen)
    {
        if (revealed) return;
        revealed = true;
        if (markSeen) DialogueProgress.MarkIntroSeen();
        RestoreNavigation();
        Action next = onReveal; onReveal = null;
        next?.Invoke();
    }

    private void ApplyVolume()
    {
        if (audioSource != null) audioSource.volume = Mathf.Clamp01(GameSettings.MusicVolume) * audioFade;
    }

    private void Close()
    {
        if (this != null && gameObject != null) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        IsPlaying = false;
        RestoreNavigation();
        if (video != null)
        {
            video.prepareCompleted -= OnPrepared;
            video.errorReceived -= OnError;
            video.loopPointReached -= OnEnded;
            video.Stop();
            video.targetTexture = null;
        }
        if (target != null) { target.Release(); Destroy(target); target = null; }
        onReveal = null;   // her kapanış yolu önce Reveal'dan geçer; çıkışta yok olmuş menüye dokunulmasın
    }

    // Space, video sürerken seçili menü düğmesine "gönder" olmasın (DialogueBox ile aynı yol).
    private void SuspendNavigation()
    {
        EventSystem system = EventSystem.current;
        if (system == null) return;
        navigationOwner = system;
        navigationWas = system.sendNavigationEvents;
        system.sendNavigationEvents = false;
        system.SetSelectedGameObject(null);
    }

    private void RestoreNavigation()
    {
        if (navigationOwner != null) navigationOwner.sendNavigationEvents = navigationWas;
        navigationOwner = null;
    }

    private static T Child<T>(RectTransform parent, string name) where T : Component
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go.GetComponent<T>();
    }

    private static ComicPopupPlate Plate(RectTransform parent, string name, Color color, Vector2 offsetMin, Vector2 offsetMax)
    {
        var plate = Child<ComicPopupPlate>(parent, name);
        var rect = plate.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        plate.color = color; plate.raycastTarget = false;
        return plate;
    }
}
