using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GoldUI : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private TMP_Text amountText;
    [Header("Harvest Flyout")]
    [SerializeField] private bool animateHarvest = true;
    [SerializeField, Min(0.1f)] private float flightDuration = 0.65f;
    [SerializeField, Min(0f)] private float iconSpacing = 0.06f;
    [SerializeField, Range(1, 24)] private int maxIconsPerHarvest = 12;
    [SerializeField, Range(1, 96)] private int maxActiveIcons = 48;
    [SerializeField, Min(8f)] private float iconSize = 22f;

    private sealed class Flight
    {
        public RectTransform icon;
        public Vector2 start, control;
        public float elapsed;
        public int amount;
    }

    private ResourceManager resources;
    private readonly List<Flight> flights = new List<Flight>();
    private readonly Stack<RectTransform> pool = new Stack<RectTransform>();
    private readonly System.Random visualRandom = new System.Random();
    private Canvas canvas;
    private RectTransform canvasRect;
    private Texture2D iconTexture;
    private Sprite iconSprite;
    private long pendingAmount;
    private int lastBalance;
    private float pulse;
    private Vector3 restingScale;

    private void Awake()
    {
        if (amountText != null) restingScale = amountText.rectTransform.localScale;
    }

    private void OnEnable() => Subscribe();
    private void Start() => Subscribe();

    private void Subscribe()
    {
        if (resources == null && ResourceManager.Instance != null)
        {
            resources = ResourceManager.Instance;
            resources.OnResourceAmountChanged += UpdateGoldUI;
            resources.OnHarvestResourceAdded += OnHarvest;
        }
        if (resources != null)
        {
            lastBalance = resources.GetResourceAmount(resourceType);
            RenderAmount(lastBalance);
        }
    }

    private void OnDisable()
    {
        if (resources != null)
        {
            resources.OnResourceAmountChanged -= UpdateGoldUI;
            resources.OnHarvestResourceAdded -= OnHarvest;
        }
        ClearFlights();
        resources = null;
        pulse = 0f;
        if (amountText != null) amountText.rectTransform.localScale = restingScale;
    }

    private void OnDestroy()
    {
        // Icons live on the root canvas, so explicitly release them with this counter.
        ClearFlights();
        while (pool.Count > 0)
        {
            var icon = pool.Pop();
            if (icon != null) Destroy(icon.gameObject);
        }
        if (iconSprite != null) Destroy(iconSprite);
        if (iconTexture != null) Destroy(iconTexture);
    }

    public void UpdateGoldUI(ResourceType changedType, int newAmount)
    {
        if (changedType != resourceType) return;
        // Spending reconciles immediately; canceled particles cannot credit it again.
        if (newAmount < lastBalance) ClearFlights();
        lastBalance = newAmount;
        RenderAmount(newAmount);
    }

    private void RenderAmount(int balance)
    {
        if (amountText == null) return;
        string tint = resourceType == ResourceType.Gold ? "FFD36A" :
            resourceType == ResourceType.Iron ? "B9DDED" : "C8C3BD";
        long displayed = System.Math.Max(0L, (long)balance - pendingAmount);
        amountText.text = $"<size=60%><color=#{tint}>{resourceType}</color></size>\n{displayed:N0}";
    }

    private bool ToCanvas(Vector2 screen, out Vector2 point)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out point);
    }

    private void OnHarvest(ResourceType type, int amount, Vector3 position)
    {
        if (type != resourceType || !animateHarvest || amountText == null || amount <= 0) return;
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvas = canvas.rootCanvas;
            if (canvas != null) canvasRect = canvas.transform as RectTransform;
        }
        Camera worldCamera = Camera.main;
        if (canvasRect == null || worldCamera == null) return;
        Vector3 screen = worldCamera.WorldToScreenPoint(position);
        if (screen.z <= 0 || !ToCanvas(screen, out Vector2 start)) return;
        int count = Mathf.Min(amount, Mathf.Min(maxIconsPerHarvest, maxActiveIcons - flights.Count));
        if (count <= 0) return; // At the cap, show this reward immediately.
        Vector2 target = TargetPosition();
        for (int i = 0; i < count; i++)
        {
            RectTransform icon = AcquireIcon();
            // Cosmetic randomness must not advance the gameplay's Unity Random state.
            float angle = (float)visualRandom.NextDouble() * Mathf.PI * 2f;
            float radius = Mathf.Sqrt((float)visualRandom.NextDouble()) * 28f;
            Vector2 spread = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            var flight = new Flight
            {
                icon = icon,
                start = start,
                control = Vector2.Lerp(start, target, 0.25f) + spread + Vector2.up * 90f,
                elapsed = -i * Mathf.Max(0f, iconSpacing),
                amount = amount / count + (i < amount % count ? 1 : 0)
            };
            icon.localPosition = new Vector3(start.x, start.y, 0f);
            icon.gameObject.SetActive(i == 0);
            flights.Add(flight);
            pendingAmount += flight.amount;
        }
    }

    private Vector2 TargetPosition()
    {
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera,
            amountText.rectTransform.TransformPoint(amountText.rectTransform.rect.center));
        ToCanvas(screen, out Vector2 target);
        return target;
    }

    private void Update()
    {
        bool arrived = false;
        if (flights.Count > 0 && canvasRect != null && amountText != null)
        {
            Vector2 target = TargetPosition();
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                Flight flight = flights[i];
                flight.elapsed += Time.unscaledDeltaTime;
                if (flight.elapsed < 0f) continue;
                flight.icon.gameObject.SetActive(true);
                float t = Mathf.Clamp01(flight.elapsed / Mathf.Max(0.1f, flightDuration));
                float eased = t * t;
                Vector2 point = (1 - eased) * (1 - eased) * flight.start +
                    2 * (1 - eased) * eased * flight.control + eased * eased * target;
                flight.icon.localPosition = new Vector3(point.x, point.y, 0f);
                flight.icon.localScale = Vector3.one * Mathf.Lerp(1f, 0.65f, t);
                if (t < 1f) continue;
                pendingAmount -= flight.amount;
                Release(flight.icon);
                flights.RemoveAt(i);
                pulse = 1f;
                arrived = true;
            }
        }
        if (arrived && resources != null) RenderAmount(resources.GetResourceAmount(resourceType));
        if (amountText != null && pulse > 0f)
        {
            pulse = Mathf.Max(0f, pulse - Time.unscaledDeltaTime * 5f);
            amountText.rectTransform.localScale = restingScale * (1f + pulse * 0.1f);
        }
    }

    private void ClearFlights()
    {
        foreach (Flight flight in flights) Release(flight.icon);
        flights.Clear();
        pendingAmount = 0;
    }

    private void Release(RectTransform icon)
    {
        if (icon == null) return;
        icon.gameObject.SetActive(false);
        pool.Push(icon);
    }

    private RectTransform AcquireIcon()
    {
        if (iconSprite == null) CreateIconSprite();
        RectTransform rect = pool.Count > 0 ? pool.Pop() : null;
        if (rect == null)
        {
            var obj = new GameObject(resourceType + " Pickup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.layer = gameObject.layer;
            rect = obj.GetComponent<RectTransform>();
            var image = obj.GetComponent<Image>();
            image.sprite = iconSprite;
            image.raycastTarget = false;
        }
        rect.SetParent(canvasRect, false);
        rect.SetAsLastSibling();
        rect.sizeDelta = Vector2.one * iconSize;
        rect.localScale = Vector3.one;
        return rect;
    }

    private void CreateIconSprite()
    {
        const int size = 32;
        iconTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        iconTexture.name = resourceType + " Pickup Icon";
        iconTexture.filterMode = FilterMode.Bilinear;
        iconTexture.wrapMode = TextureWrapMode.Clamp;
        Color baseColor = resourceType == ResourceType.Gold ? new Color(1f, 0.76f, 0.2f) :
            resourceType == ResourceType.Iron ? new Color(0.65f, 0.83f, 0.94f) : new Color(0.65f, 0.62f, 0.58f);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - 15.5f) / 14f, dy = (y - 15.5f) / 14f;
            bool inside = resourceType == ResourceType.Gold ? dx * dx + dy * dy <= 1f :
                resourceType == ResourceType.Iron ? Mathf.Abs(dx) + Mathf.Abs(dy) * 0.3f <= 1f && Mathf.Abs(dy) <= 0.65f :
                Mathf.Abs(dx) + Mathf.Abs(dy) <= 1.2f && Mathf.Abs(dx) <= 0.85f && Mathf.Abs(dy) <= 0.9f;
            Color shade = baseColor * (dy > 0.2f ? 1.15f : dy < -0.4f ? 0.65f : 0.9f);
            shade.a = inside ? 1f : 0f;
            pixels[y * size + x] = shade;
        }
        iconTexture.SetPixels(pixels);
        iconTexture.Apply(false, true);
        iconSprite = Sprite.Create(iconTexture, new Rect(0, 0, size, size),
            Vector2.one * 0.5f, size, 0, SpriteMeshType.FullRect);
    }
}
