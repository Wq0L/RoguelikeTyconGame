using UnityEngine;
using TMPro;
using DG.Tweening;

public class FloatingText : MonoBehaviour
{
    [SerializeField] private TextMeshPro textMesh;
    [SerializeField, Min(0.4f)] private float lifetime = 0.9f;
    [SerializeField] private float normalFontSize = 5f;
    [SerializeField] private float criticalFontSize = 8f;
    [SerializeField] private Color normalColor = new Color(1f, 0.96f, 0.8f);
    [SerializeField] private Color criticalColor = new Color(1f, 0.65f, 0.12f);
    private Sequence animationSequence;
    private Transform cam;
    private Vector3 origin, travel;
    private float progress, alpha, tilt;
    private bool leased;
    private static bool scatterRight;

    public void CopyStyleTo(TextMeshPro target)
    {
        target.font = textMesh.font;
        target.fontSharedMaterial = textMesh.fontSharedMaterial;
        target.fontSize = normalFontSize;
        target.fontStyle = FontStyles.Bold;
        target.color = normalColor;
    }

    private void Awake()
    {
        // One sequence per pooled object: Restart reuses the tweeners.
        transform.localScale = Vector3.zero;
        textMesh.transform.localScale = Vector3.one;
        SetAlpha(1f);
        float duration = Mathf.Max(0.4f, lifetime);
        animationSequence = DOTween.Sequence().SetAutoKill(false).Pause();
        animationSequence.Append(transform.DOScale(Vector3.one, 0.055f).From(Vector3.zero, false).SetEase(Ease.OutCubic));
        // Punch the visual child: its baseline stays 1 while the root enters from 0.
        animationSequence.Append(textMesh.transform.DOPunchScale(Vector3.one * 0.3f, 0.22f, 1, 0f));
        animationSequence.Insert(0f, DOTween.To(() => progress, SetProgress, 1f, duration).From(0f, false).SetEase(Ease.OutCubic));
        animationSequence.Insert(duration * 0.65f,
            DOTween.To(() => alpha, SetAlpha, 0f, duration * 0.35f).From(1f, false).SetEase(Ease.OutCubic));
        animationSequence.OnComplete(ReturnToPool);
    }

    public void Show(int damage, bool isCrit)
    {
        Vector3 spawnPosition = transform.position;
        animationSequence.Rewind();
        cam = Camera.main != null ? Camera.main.transform : null;
        leased = true;
        textMesh.text = damage.ToString();
        textMesh.fontSize = isCrit ? criticalFontSize : normalFontSize;
        textMesh.color = isCrit ? criticalColor : normalColor;
        textMesh.fontStyle = FontStyles.Bold;
        SetAlpha(1f);
        transform.localScale = Vector3.zero;
        tilt = Random.Range(-15f, 15f);
        scatterRight = !scatterRight;
        Vector3 right = cam != null ? cam.right : Vector3.right;
        Vector3 up = cam != null ? cam.up : Vector3.up;
        origin = spawnPosition + right * Random.Range(-0.15f, 0.15f);
        travel = right * Random.Range(0.35f, 1.05f) * (scatterRight ? 1f : -1f)
            + up * Random.Range(0.9f, 1.8f) * (isCrit ? 1.15f : 1f);
        SetProgress(0f);
        FaceCamera();
        animationSequence.Restart();
    }

    private void SetProgress(float value)
    {
        progress = value;
        transform.position = origin + travel * value;
    }

    private void SetAlpha(float value)
    {
        alpha = value;
        textMesh.alpha = value;
    }

    private void LateUpdate() => FaceCamera();
    private void FaceCamera()
    {
        transform.rotation = (cam != null ? cam.rotation : Quaternion.identity)
            * Quaternion.Euler(0f, 0f, tilt);
    }

    private void ReturnToPool()
    {
        if (!leased) return;
        leased = false;
        if (VFXManager.Instance != null) VFXManager.Instance.ReturnText(this);
        else gameObject.SetActive(false);
    }
    private void OnDisable()
    {
        leased = false;
        animationSequence?.Pause();
    }
    private void OnDestroy() => animationSequence?.Kill();
}
