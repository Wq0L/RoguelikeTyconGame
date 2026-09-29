using DG.Tweening;
using UnityEngine;

// Tek bir UI elemanını sıfırdan "pop" ederek gösterir (OutBack + isteğe bağlı dönüş).
// Dinlenme ölçeğini ComicHoverMotion'dan alır; pop bitince hover durumunu yeniden uygular.
// Tween'ler transform'u hedefler: dışarıdan transform.DOKill() çağrısı pop'u da durdurur.
[DisallowMultipleComponent]
public sealed class UIPop : MonoBehaviour
{
    private Vector3 restScale = Vector3.one;
    private Quaternion restRotation = Quaternion.identity;
    private bool captured;
    private CanvasGroup inputGroup;
    private ComicHoverMotion hover;

    public static UIPop For(Component target)
    {
        if (!target.TryGetComponent(out UIPop pop)) pop = target.gameObject.AddComponent<UIPop>();
        return pop;
    }

    private void Capture()
    {
        if (captured) return;
        TryGetComponent(out hover);
        if (hover != null && gameObject.activeInHierarchy)
        {
            restScale = hover.RestScale;
            restRotation = hover.RestRotation;
        }
        else
        {
            restScale = transform.localScale;
            restRotation = transform.localRotation;
        }
        captured = true;
    }

    // lockInput: pop'un ilk yarısında tıklamayı engeller (yanlışlıkla kart seçmeyi önler).
    public void Play(float delay, float duration = 0.3f, float fromAngle = 0f, bool lockInput = false)
    {
        Capture();
        transform.DOKill();
        transform.localScale = Vector3.zero;
        transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, fromAngle);
        SetInputBlocked(lockInput);

        Sequence sequence = DOTween.Sequence().SetTarget(transform).SetUpdate(true);
        sequence.Insert(delay, transform.DOScale(restScale, duration).SetEase(Ease.OutBack, 1.7f));
        if (!Mathf.Approximately(fromAngle, 0f))
            sequence.Insert(delay, transform.DOLocalRotateQuaternion(restRotation, duration * 1.15f).SetEase(Ease.OutBack));
        if (lockInput) sequence.InsertCallback(delay + duration * 0.55f, () => SetInputBlocked(false));
        sequence.OnComplete(RefreshHover);
    }

    public void ResetToRest()
    {
        if (!captured) return;
        transform.DOKill();
        transform.localScale = restScale;
        transform.localRotation = restRotation;
        SetInputBlocked(false);
    }

    private void SetInputBlocked(bool blocked)
    {
        if (!blocked && inputGroup == null) return;
        if (inputGroup == null && !TryGetComponent(out inputGroup))
            inputGroup = gameObject.AddComponent<CanvasGroup>();
        inputGroup.blocksRaycasts = !blocked;
    }

    private void RefreshHover()
    {
        if (hover != null && hover.isActiveAndEnabled) hover.Refresh();
    }

    private void OnDisable() => ResetToRest();
}
