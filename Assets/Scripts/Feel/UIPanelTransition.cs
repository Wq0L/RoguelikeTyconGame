using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Panel açılışı: kök fade + hafif scale (OutBack), ardından doğrudan çocuk butonlar soldan sağa
// sırayla pop olur. Kapanış anlıktır; UIManager'ın state akışı değişmez. Unscaled time kullanır.
[DisallowMultipleComponent]
public class UIPanelTransition : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float scaleDuration = 0.34f;
    [SerializeField, Range(0.5f, 1f)] private float startScale = 0.94f;
    [SerializeField] private bool popButtons = true;
    [SerializeField, Min(0f)] private float popDelay = 0.06f;
    [SerializeField, Min(0f)] private float popStagger = 0.06f;
    [SerializeField, Min(0.01f)] private float popDuration = 0.3f;

    private CanvasGroup group;
    private Vector3 restScale;
    private Coroutine routine;
    private readonly List<UIPop> popTargets = new();

    private static int holdFrame = -1;
    private static float holdSeconds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        holdFrame = -1;
        holdSeconds = 0f;
    }

    // Bu karede açılacak panel verilen süre görünmez ve tıklanmaz bekler (ör. saksı yere otururken).
    public static void HoldNextOpen(float seconds)
    {
        holdFrame = Time.frameCount;
        holdSeconds = seconds;
    }

    public static UIPanelTransition Attach(GameObject panel, bool popButtons)
    {
        if (panel == null) return null;
        IsolateCanvas(panel);
        if (!panel.TryGetComponent(out UIPanelTransition transition))
            transition = panel.AddComponent<UIPanelTransition>();
        transition.popButtons = popButtons;
        return transition;
    }

    // Tüm oyun UI'ı tek Canvas'ta: panel her karede ölçeklenip solarken (veya içindeki detay paneli
    // kayarken) Unity bütün UI'ı (harita, sayaçlar...) yeniden paketliyordu. Alt Canvas sadece bu
    // paneli yeniden paketler. Sıralama değişmez (overrideSorting kapalı); tıklama için kendi raycaster'ı.
    private static void IsolateCanvas(GameObject panel)
    {
        if (panel.TryGetComponent(out Canvas _)) return;
        Canvas parent = panel.transform.parent != null ? panel.transform.parent.GetComponentInParent<Canvas>() : null;
        var canvas = panel.AddComponent<Canvas>();
        if (parent != null) canvas.additionalShaderChannels = parent.additionalShaderChannels;
        if (!panel.TryGetComponent(out GraphicRaycaster _)) panel.AddComponent<GraphicRaycaster>();
    }

    private void Awake()
    {
        if (!TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
        restScale = transform.localScale;
    }

    private void OnEnable()
    {
        // İlk kare görünmez: çocukların Awake/Start'ı bitsin, sonra animasyon başlasın.
        group.alpha = 0f;
        transform.localScale = restScale * startScale;
        float hold = holdFrame == Time.frameCount ? holdSeconds : 0f;
        if (hold > 0f)
        {
            holdFrame = -1;
            group.blocksRaycasts = false;
        }
        routine = StartCoroutine(PlayOpen(hold));
    }

    private IEnumerator PlayOpen(float hold)
    {
        yield return null;
        if (hold > 0f) yield return new WaitForSecondsRealtime(hold);
        group.blocksRaycasts = true;
        routine = null;

        group.DOKill();
        transform.DOKill();
        group.DOFade(1f, fadeDuration).SetEase(Ease.OutQuad).SetUpdate(true);
        transform.DOScale(restScale, scaleDuration).SetEase(Ease.OutBack, 1.4f).SetUpdate(true);

        if (!popButtons) yield break;
        CollectPopTargets();
        for (int i = 0; i < popTargets.Count; i++)
            popTargets[i].Play(popDelay + i * popStagger, popDuration);
    }

    private void CollectPopTargets()
    {
        popTargets.Clear();
        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeSelf || !child.TryGetComponent(out Selectable _)) continue;
            popTargets.Add(UIPop.For(child));
        }
        // Soldan sağa dalga.
        popTargets.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
    }

    private void OnDisable()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        group.DOKill();
        transform.DOKill();
        group.alpha = 1f;
        group.blocksRaycasts = true;
        transform.localScale = restScale;
        foreach (UIPop pop in popTargets)
            if (pop != null) pop.ResetToRest();
    }
}
