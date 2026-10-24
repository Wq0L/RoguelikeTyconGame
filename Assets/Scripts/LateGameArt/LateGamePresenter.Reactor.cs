using UnityEngine;

public sealed partial class LateGamePresenter
{
    private Item reactorItem;
    private RareReactorEffect reactorOwner;
    private PlayerController reactorCursor;
    private int reactorRefreshes;
    private float reactorPulse, reactorPulseCooldown;

    private void ClearReactor()
    {
        Release(reactorItem); reactorItem = null; reactorOwner = null;
        reactorRefreshes = 0; reactorPulse = reactorPulseCooldown = 0f;
    }

    private void SyncReactor()
    {
        var source = RunEffects.Instance != null ? RunEffects.Instance.Find<RareReactorEffect>() : null;
        if (source == null || RoundManager.Instance == null || !RoundManager.Instance.IsRoundActive)
        { ClearReactor(); return; }
        if (reactorOwner != source) { ClearReactor(); reactorOwner = source; }
        if (!source.IsActive)
        {
            if (reactorItem != null) { Retire(reactorItem); reactorItem = null; }
            return;
        }
        if (reactorCursor == null) reactorCursor = FindFirstObjectByType<PlayerController>();
        var camera = Camera.main;
        if (reactorCursor == null || camera == null) return;
        if (reactorItem == null)
        {
            reactorItem = Rent(LateGameSlot.ReactorIndicator, Moon, true, LateGameVariant.Reactor);
            if (reactorItem == null) return;
            reactorItem.content?.Show();
            reactorRefreshes = source.Refreshes;
        }
        // Only presentation pulse has a local clock. Duration/expiry always come from the effect.
        float delta = GameManager.Instance != null && GameManager.Instance.CurrentState == GameStates.Round ? Time.deltaTime : 0f;
        if (delta <= 0f) return;
        reactorItem.age += delta;
        reactorPulse = Mathf.Max(0f, reactorPulse - delta);
        reactorPulseCooldown = Mathf.Max(0f, reactorPulseCooldown - delta);
        if (source.Refreshes != reactorRefreshes)
        {
            reactorRefreshes = source.Refreshes;
            if (reactorPulseCooldown <= 0f) { reactorPulse = .16f; reactorPulseCooldown = .25f; }
        }
        float energy = Mathf.Clamp01(source.Remaining / .8f);
        float size = .55f * Mathf.Lerp(.2f, 1f, energy) *
            (1f + .035f * Mathf.Sin(reactorItem.age * 4f) + .2f * reactorPulse / .16f);
        var offset = camera.transform.right * (reactorCursor.CursorRadius + .35f) + Vector3.up * .65f;
        reactorItem.root.transform.SetPositionAndRotation(reactorCursor.CursorWorldPosition + offset, camera.transform.rotation);
        reactorItem.scale = Vector3.one * size;
        if (reactorItem.visual != null) reactorItem.visual.localScale = reactorItem.scale;
        reactorItem.content?.Step(delta, reactorItem.age);
    }
}
