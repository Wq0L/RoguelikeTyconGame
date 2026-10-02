using UnityEngine;

public class TileCellUI : MonoBehaviour, ITooltipProvider
{
    [SerializeField] private GameObject tooltipPrefab;

    private GroundCell groundCell;
    private FreshTileRing freshRing;
    private BossZoneFrame eventZone;

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

    // Boss bölgesi (Don Cephesi, Sert Kabuk): tile'ın çevresinde çerçeve ve köşede küçük bir işaret. Yaklaşırken kesikli,
    // aktifken düz. Tile'ın kendi rengi (kart türü) örtülmez; merkez boş kalır. Tıklamayı engellemez.
    public bool EventZoneActive => InEventZone && eventZone.Solid;
    public Color EventZoneColor => eventZone != null ? eventZone.color : Color.clear;

    public void SetEventZone(bool inZone, bool active, Color color)
    {
        if (!inZone)
        {
            if (eventZone != null) eventZone.gameObject.SetActive(false);
            return;
        }
        if (eventZone == null)
        {
            var go = new GameObject("Event Zone", typeof(RectTransform), typeof(CanvasRenderer));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);
            eventZone = go.AddComponent<BossZoneFrame>();
            eventZone.raycastTarget = false;
        }
        color.a = 1f;
        eventZone.color = color;
        eventZone.Solid = active;
        eventZone.gameObject.SetActive(true);
        eventZone.SetVerticesDirty();
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

// Harita hücresindeki boss bölgesi işareti: yalnız çerçeve (kesikli ya da düz) ve sol üst köşede küçük bir üçgen.
// Hücrenin ortası çizilmez; tile rengi olduğu gibi görünür. Koyu alt çizgi her tile renginin üstünde okunurluk sağlar.
public sealed class BossZoneFrame : UnityEngine.UI.MaskableGraphic
{
    public const float Thickness = 5f, InkPad = 1.5f, CornerShare = 0.26f;
    private const int DashesPerSide = 4;
    private static readonly Color Ink = new Color32(40, 29, 43, 235);

    // true: aktif boss (düz çerçeve). false: yaklaşan boss (kesikli çerçeve).
    public bool Solid;

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= Thickness * 2f || r.height <= Thickness * 2f) return;
        Frame(vh, r, Thickness + InkPad, Ink);
        Rect inner = new Rect(r.xMin + InkPad * .5f, r.yMin + InkPad * .5f, r.width - InkPad, r.height - InkPad);
        Frame(vh, inner, Thickness, color);
        // Köşe işareti: sol üstte küçük üçgen (tile'ın merkezine girmez).
        float size = Mathf.Min(r.width, r.height) * CornerShare;
        Triangle(vh, new Vector2(r.xMin, r.yMax), new Vector2(r.xMin + size + InkPad * 2f, r.yMax), new Vector2(r.xMin, r.yMax - size - InkPad * 2f), Ink);
        Triangle(vh, new Vector2(r.xMin, r.yMax), new Vector2(r.xMin + size, r.yMax), new Vector2(r.xMin, r.yMax - size), color);
    }

    private void Frame(UnityEngine.UI.VertexHelper vh, Rect r, float t, Color c)
    {
        Side(vh, new Vector2(r.xMin, r.yMax - t), new Vector2(r.xMax, r.yMax), true, c);    // üst
        Side(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin + t), true, c);    // alt
        Side(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin + t, r.yMax), false, c);   // sol
        Side(vh, new Vector2(r.xMax - t, r.yMin), new Vector2(r.xMax, r.yMax), false, c);   // sağ
    }

    // Düz: tek dikdörtgen. Kesikli: kenar boyunca eşit aralıklı parçalar.
    private void Side(UnityEngine.UI.VertexHelper vh, Vector2 min, Vector2 max, bool horizontal, Color c)
    {
        if (Solid) { Quad(vh, min, max, c); return; }
        float length = horizontal ? max.x - min.x : max.y - min.y;
        float period = length / (DashesPerSide - 0.4f), dash = period * 0.6f;
        for (int i = 0; i < DashesPerSide; i++)
        {
            float from = i * period, to = Mathf.Min(length, from + dash);
            if (horizontal) Quad(vh, new Vector2(min.x + from, min.y), new Vector2(min.x + to, max.y), c);
            else Quad(vh, new Vector2(min.x, min.y + from), new Vector2(max.x, min.y + to), c);
        }
    }

    private static void Quad(UnityEngine.UI.VertexHelper vh, Vector2 min, Vector2 max, Color c)
    {
        int i = vh.currentVertCount;
        vh.AddVert(new Vector3(min.x, min.y), c, Vector2.zero);
        vh.AddVert(new Vector3(min.x, max.y), c, Vector2.zero);
        vh.AddVert(new Vector3(max.x, max.y), c, Vector2.zero);
        vh.AddVert(new Vector3(max.x, min.y), c, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }

    private static void Triangle(UnityEngine.UI.VertexHelper vh, Vector2 a, Vector2 b, Vector2 d, Color c)
    {
        int i = vh.currentVertCount;
        vh.AddVert(a, c, Vector2.zero); vh.AddVert(b, c, Vector2.zero); vh.AddVert(d, c, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
    }
}
