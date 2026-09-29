using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// Saksı yerleştirme ve satış hissi. Placing/Selling'de timeScale 0: hepsi unscaled.
// - Yerleştirme: yukarıdan düşer, yere oturunca ezilip yaylanır; toz halkası, "tok" sesi, hafif sarsıntı.
//   Shop paneli iniş bitene kadar kısa süre bekletilir ki oyuncu saksının yerine oturduğunu görsün.
// - Satış: hafifçe şişip küçülerek kaybolur, altın kıvılcımı; iade altınları saksıdan sayaca uçar.
// Oyun mantığı (grid, iade, spawner) her zamanki gibi anında çalışır; burada sadece görsel var.
public static class PlanterFeel
{
    private const float DropHeight = 3f;
    private const float DropTime = 0.26f;

    public static void PlayLanding(GameObject planter, IReadOnlyList<GridObject> cells)
    {
        if (planter == null || Application.isBatchMode) return;
        Transform root = planter.transform;
        Vector3 target = root.position;
        Vector3 restScale = root.localScale;
        FootprintOf(cells, target, out Vector3 center, out float radius);

        root.DOKill();
        root.position = target + Vector3.up * DropHeight;
        Sequence sequence = DOTween.Sequence().SetTarget(root).SetUpdate(true);
        sequence.Append(root.DOMove(target, DropTime).SetEase(Ease.InQuad));
        sequence.AppendCallback(() => Land(center, radius));
        sequence.Append(root.DOScale(new Vector3(restScale.x * 1.12f, restScale.y * 0.78f, restScale.z * 1.12f), 0.07f)
            .SetEase(Ease.OutQuad));
        sequence.Append(root.DOScale(restScale, 0.32f).SetEase(Ease.OutBack, 2.2f));
        sequence.OnKill(() =>
        {
            if (root == null) return;
            root.position = target;
            root.localScale = restScale;
        });

        UIPanelTransition.HoldNextOpen(0.45f);
    }

    private static void Land(Vector3 center, float radius)
    {
        VFXManager.Instance?.PlayLandingDust(center, radius);
        FeelAudio.Play(FeelSound.Thump, 0.7f);
        CameraFeel.Shake(0.26f, 0.35f);
    }

    // Satış animasyonunu oynatır ve bitince saksıyı yok eder. false: çağıran klasik yolla yok etsin.
    public static bool PlaySell(GameObject planter)
    {
        if (planter == null || Application.isBatchMode) return false;
        Transform root = planter.transform;
        Vector3 restScale = root.localScale;
        Bounds bounds = BoundsOf(planter);
        VFXManager.Instance?.PlaySellPoof(bounds.center - Vector3.up * bounds.extents.y * 0.5f,
            Mathf.Max(bounds.extents.x, bounds.extents.z));
        FeelAudio.Play(FeelSound.Chime, 0.45f, 1.35f);

        root.DOKill();
        Sequence sequence = DOTween.Sequence().SetTarget(root).SetUpdate(true);
        sequence.Append(root.DOScale(restScale * 1.08f, 0.06f).SetEase(Ease.OutQuad));
        sequence.Append(root.DOScale(Vector3.zero, 0.22f).SetEase(Ease.InBack));
        // OnKill tamamlanınca da çağrılır; yarıda kesilse bile saksı mutlaka yok edilir.
        sequence.OnKill(() => { if (planter != null) Object.Destroy(planter); });
        return true;
    }

    // İade altınlarının uçacağı başlangıç noktası (saksının görsel merkezi).
    public static Vector3 CenterOf(GameObject planter) => planter == null ? Vector3.zero : BoundsOf(planter).center;

    private static Bounds BoundsOf(GameObject planter)
    {
        var renderers = planter.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(planter.transform.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static void FootprintOf(IReadOnlyList<GridObject> cells, Vector3 fallback, out Vector3 center, out float radius)
    {
        center = fallback;
        radius = 1f;
        if (cells == null || cells.Count == 0) return;
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (GridObject cell in cells)
        {
            GroundCell ground = cell?.GetGroundCellCached();
            if (ground == null) continue;
            sum += ground.transform.position;
            count++;
        }
        if (count == 0) return;
        center = sum / count;
        float farthest = 0f;
        foreach (GridObject cell in cells)
        {
            GroundCell ground = cell?.GetGroundCellCached();
            if (ground != null) farthest = Mathf.Max(farthest, Vector3.Distance(center, ground.transform.position));
        }
        float half = GridManager.Instance != null ? GridManager.Instance.GetCellSize() * 0.55f : 1.1f;
        radius = farthest + half;
    }
}
