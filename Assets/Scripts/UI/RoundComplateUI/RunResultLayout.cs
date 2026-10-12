using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation only: the existing result controller still owns outcomes and button actions.
public sealed class RunResultLayout : MonoBehaviour
{
    ScrollRect scroll;

    public void Prepare(TextMeshProUGUI label)
    {
        if (scroll == null)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            var shade = Rect("Result backdrop", transform, Vector2.zero, Vector2.one);
            shade.SetAsFirstSibling();
            shade.gameObject.AddComponent<Image>().color = new Color(.025f, .035f, .055f, .94f);

            var plate = Rect("Result paper", transform, new Vector2(.08f, .18f), new Vector2(.92f, .94f));
            plate.gameObject.AddComponent<Image>().color = new Color(.09f, .11f, .17f, 1f);
            scroll = plate.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 45f;
            var viewport = Rect("Result viewport", plate, Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(28f, 22f);
            viewport.offsetMax = new Vector2(-40f, -22f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            scroll.viewport = viewport;

            var content = label.rectTransform;
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            content.localScale = Vector3.one;
            label.enableAutoSizing = false;
            label.fontSize = 30f;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = Color.white;
            label.margin = Vector4.zero;
            label.raycastTarget = false;
            var fit = label.GetComponent<ContentSizeFitter>() ?? label.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            var bar = Rect("Result scrollbar", plate, new Vector2(1f, 0f), Vector2.one);
            bar.offsetMin = new Vector2(-24f, 22f);
            bar.offsetMax = new Vector2(-10f, -22f);
            bar.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, .12f);
            var handle = Rect("Handle", bar, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            handle.color = new Color(.9f, .72f, .35f, 1f);
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            foreach (Button button in buttons)
            {
                var rect = (RectTransform)button.transform;
                float x = rect.anchoredPosition.x;
                rect.SetParent(transform, false);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .085f);
                rect.anchoredPosition = new Vector2(x, 0f);
                rect.SetAsLastSibling();
            }
        }
        scroll.StopMovement();
        scroll.content.anchoredPosition = Vector2.zero;
        scroll.verticalNormalizedPosition = 1f;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
}
