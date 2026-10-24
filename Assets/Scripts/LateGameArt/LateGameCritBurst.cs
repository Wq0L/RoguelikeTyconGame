using UnityEngine;

// Only poses the three visual stages. Scaled age and pooling belong to LateGamePresenter.
public sealed class LateGameCritBurst : MonoBehaviour
{
    public Transform core, silhouette;
    public Transform[] chips;
    public void ResetPose() => Pose(0f);
    public void Pose(float age)
    {
        if (core != null) core.gameObject.SetActive(age < .05f);
        if (silhouette != null)
        {
            silhouette.gameObject.SetActive(age < .22f);
            float size = Mathf.Lerp(1f, .82f, Mathf.Clamp01((age - .16f) / .06f));
            silhouette.localScale = Vector3.one * size;
        }
        if (chips == null) return;
        float scale = 1f - Mathf.Clamp01(age / .32f);
        foreach (var chip in chips) if (chip != null) chip.localScale = Vector3.one * scale;
    }
}
