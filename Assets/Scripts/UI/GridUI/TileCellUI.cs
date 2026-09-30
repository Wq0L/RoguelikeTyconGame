using UnityEngine;

public class TileCellUI : MonoBehaviour, ITooltipProvider
{
    [SerializeField] private GameObject tooltipPrefab;

    private GroundCell groundCell;
    private FreshTileRing freshRing;
    private UnityEngine.UI.Image eventZone;

    public bool InEventZone => eventZone != null && eventZone.gameObject.activeSelf;

    public void Setup(GroundCell cell)
    {
        groundCell = cell;
    }

    // Bu round'un kart seçimi bu tile'a düştüyse parlar. Hücre round'lar arasında tekrar kullanılır.
    public void SetFresh(bool fresh, float delay, string label = "YENİ")
    {
        if (!fresh)
        {
            if (freshRing != null) freshRing.gameObject.SetActive(false);
            return;
        }
        if (freshRing == null) freshRing = FreshTileRing.Create(transform);
        freshRing.Show(delay, label);
    }

    // Segment olayı (Don Cephesi) bölgesi: yaklaşırken açık, aktifken koyu buz örtüsü. Tıklamayı engellemez.
    public void SetEventZone(bool inZone, bool active, Color color)
    {
        if (!inZone)
        {
            if (eventZone != null) eventZone.gameObject.SetActive(false);
            return;
        }
        if (eventZone == null)
        {
            var go = new GameObject("Event Zone", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(3f, 3f);
            rect.offsetMax = new Vector2(-3f, -3f);
            eventZone = go.GetComponent<UnityEngine.UI.Image>();
            eventZone.raycastTarget = false;
        }
        color.a = active ? 0.62f : 0.34f;
        eventZone.color = color;
        eventZone.gameObject.SetActive(true);
    }

    public bool ShouldShowTooltip()
    {
        return groundCell != null
            && !groundCell.IsLocked
            && groundCell.CurrentModifier != null;
    }

    public GameObject GetTooltipPrefab()
    {
        return tooltipPrefab;
    }

    public Vector3 GetTooltipPosition()
    {
        return Input.mousePosition + new Vector3(-10f, 30f, 0f);
    }

    public void FillTooltip(GameObject instance)
    {
        GridTileToolTipContent content = instance.GetComponent<GridTileToolTipContent>();  // ← senin ismin
        if (content == null) return;

        TileModifierSO modifier = groundCell.CurrentModifier;

        content.SetName(modifier.modifierName);
        content.SetRarity(modifier.rarity.ToString());
        content.SetCell(groundCell);
    }

}
