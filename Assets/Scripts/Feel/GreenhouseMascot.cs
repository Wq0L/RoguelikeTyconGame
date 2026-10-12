using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Cosmetic only: no gameplay Random calls, input interception or time-scale changes.
[DefaultExecutionOrder(1100)]
public sealed class GreenhouseMascot : MonoBehaviour
{
    private readonly System.Random random = new System.Random();
    private SpaceBackground space;
    private GameManager game;
    private RectTransform canvas, reveal, portrait, bubble;
    private CanvasGroup visibility;
    private TMP_Text caption;
    private readonly Sprite[] frames = new Sprite[16];
    private Image picture;
    private int shownFrame = -1;
    // Measured alpha bounds in the 1254px source atlas; generated sheets are not
    // perfectly uniform grids. Keep 2px padding and a common feet pivot.
    private static readonly Vector4[] FrameBounds = {
        new Vector4(19,23,329,321),new Vector4(353,35,617,318),new Vector4(654,27,925,319),new Vector4(956,27,1235,319),
        new Vector4(25,339,324,622),new Vector4(345,346,633,620),new Vector4(670,330,945,623),new Vector4(970,333,1247,625),
        new Vector4(22,638,313,923),new Vector4(331,638,629,923),new Vector4(648,637,941,923),new Vector4(960,638,1244,925),
        new Vector4(26,939,331,1230),new Vector4(366,938,629,1224),new Vector4(651,933,931,1230),new Vector4(947,937,1245,1227)
    };
    private float nextPeek, elapsed, duration, speechCooldown;
    private bool active, speaking;
    private string pendingSpeech;
    private GameStates observedState;
    private int lastRound = -1;

    private void Start()
    {
        space = FindAnyObjectByType<SpaceBackground>();
        var texture = Resources.Load<Texture2D>("GreenhouseMascotFrames");
        if (texture == null || space == null) { enabled = false; return; }
        for(int i=0;i<frames.Length;i++){
            Vector4 b=FrameBounds[i];
            var rect=new Rect((b.x-2)*texture.width/1254f,(1254-b.w-2)*texture.height/1254f,
                (b.z-b.x+4)*texture.width/1254f,(b.w-b.y+4)*texture.height/1254f);
            frames[i]=Sprite.Create(texture,rect,new Vector2(.5f,0),100,0,SpriteMeshType.FullRect);
            frames[i].name="Mascot frame "+i;
        }
        canvas = FeelOverlay.CreateCanvas("Mascot Presentation", -1);
        visibility = canvas.gameObject.AddComponent<CanvasGroup>();
        visibility.blocksRaycasts = false;
        visibility.interactable = false;
        visibility.alpha = 0;
        reveal = Rect("Planet Rim Reveal", canvas, new Vector2(150, 115));
        reveal.pivot = new Vector2(.5f, 0);
        reveal.gameObject.AddComponent<RectMask2D>();
        portrait = Rect("Mascot", reveal, new Vector2(108, 108));
        portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, 0);
        portrait.pivot = new Vector2(.5f, 0);
        picture = portrait.gameObject.AddComponent<Image>();
        picture.sprite = frames[8];
        picture.preserveAspect = false;
        picture.raycastTarget = false;

        bubble = Rect("Speech", canvas, new Vector2(260, 78));
        var paper = bubble.gameObject.AddComponent<Image>();
        paper.color = new Color(1f, .96f, .81f);
        var theme = FeelOverlay.Theme;
        if (theme != null) paper.sprite = theme.cardSprite;
        paper.type = Image.Type.Sliced;
        paper.raycastTarget = false;
        var shadow = bubble.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(.025f, .025f, .06f, .9f);
        shadow.effectDistance = new Vector2(3, -4);
        RectTransform text = Rect("Caption", bubble, new Vector2(232, 62));
        caption = text.gameObject.AddComponent<TextMeshProUGUI>();
        if (theme != null) theme.StylePopupText(caption);
        caption.fontSize = 22;
        caption.alignment = TextAlignmentOptions.Center;
        caption.raycastTarget = false;
        caption.enableAutoSizing = true;
        caption.fontSizeMin = 17;
        caption.fontSizeMax = 22;
        bubble.gameObject.SetActive(false);
        SchedulePeek();
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        return rect;
    }

    private void SchedulePeek() => nextPeek = Time.unscaledTime + 18f + (float)random.NextDouble() * 25f;
    private bool CanShow => game != null && (game.CurrentState == GameStates.RunSetup
        || game.CurrentState == GameStates.Round || game.CurrentState == GameStates.RoundEnd)
        && !RoundPreviewUI.IsOpen && (game.CurrentState != GameStates.Round || Time.timeScale > 0);

    private void ObserveState()
    {
        if (game == null) game = GameManager.Instance;
        if (game == null) return;
        if (observedState == game.CurrentState) return;
        observedState = game.CurrentState;
        pendingSpeech = null;
        if (observedState == GameStates.RunSetup) pendingSpeech = "Önce biraz su. Sonra hasat.";
        if (observedState == GameStates.Round && RoundManager.Instance != null
            && lastRound != RoundManager.Instance.CurrentRound)
        {
            lastRound = RoundManager.Instance.CurrentRound;
            pendingSpeech = random.Next(2) == 0 ? "Bir hasat daha. Dayanırız." : "Kökler hâlâ tutuyor.";
        }
        if (observedState == GameStates.RoundEnd)
            pendingSpeech = random.Next(2) == 0 ? "Bir nefes... Bir de biraz su." : "Sıradaki hasada yer açalım!";
    }

    private void LateUpdate()
    {
        if (canvas == null) return;
        ObserveState();
        if (!CanShow || space == null || !Application.isFocused
            // Keep the entire supporting foot inside the disc, not balanced at its outermost pixel.
            || !space.TryGetPlanetScreenPoint(new Vector2(-.52f, .78f), out Vector2 screen)
            || !space.TryGetPlanetScreenPoint(Vector2.zero, out Vector2 centerScreen)
            || !space.TryGetPlanetScreenPoint(new Vector2(-.20f, .55f), out Vector2 destinationScreen))
        {
            visibility.alpha = 0;
            if (active) { active = false; SchedulePeek(); }
            pendingSpeech = null;
            return;
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out Vector2 point);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, destinationScreen, null, out Vector2 destination);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, centerScreen, null, out Vector2 center);
        // Preserve the actual rim anchor; shrink at small resolutions instead of detaching from the planet.
        float room = canvas.rect.yMax - point.y - 8;
        if (room < 45 || point.x < canvas.rect.xMin + 65 || point.x > canvas.rect.xMax - 65)
        { visibility.alpha = 0; return; }
        float size = Mathf.Min(78, room);
        if (!active)
        {
            visibility.alpha = 0;
            if (pendingSpeech != null && Time.unscaledTime >= speechCooldown)
            {
                caption.text = pendingSpeech;
                pendingSpeech = null;
                speaking = true;
                duration = 11.8f;
                speechCooldown = Time.unscaledTime + 14;
            }
            else if (Time.unscaledTime >= nextPeek) { speaking = false; duration = 11.8f; }
            else return;
            active = true;
            elapsed = 0;
            bubble.gameObject.SetActive(false);
        }
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= duration) { active = false; visibility.alpha = 0; SchedulePeek(); return; }
        visibility.alpha = 1;
        // Emerge (1.4s), waddle down (2.4s), look right then left (3s),
        // turn (0.6s), walk home (2.4s), pass behind the rim feet-first (2s).
        float progress;
        int frame;
        if(elapsed < 1.4f) { progress=0; frame=8; }
        else if(elapsed < 3.8f) { progress=(elapsed-1.4f)/2.4f; frame=(int)((elapsed-1.4f)*6)%4; }
        else if(elapsed < 6.8f) {
            progress=1;
            frame=elapsed<4.1f ? 8 : elapsed<5.1f ? 9 : elapsed<5.35f ? 11 : 10;
        }
        else if(elapsed < 7.4f) { progress=1; frame=12+Mathf.Min(3,(int)((elapsed-6.8f)/.15f)); }
        else if(elapsed < 9.8f) { progress=1-(elapsed-7.4f)/2.4f; frame=4+(int)((elapsed-7.4f)*6)%4; }
        else { progress=0; frame=4+(int)((elapsed-9.8f)*6)%4; }
        if(shownFrame!=frame){picture.sprite=frames[frame];shownFrame=frame;}
        Vector4 bounds=FrameBounds[frame];
        portrait.sizeDelta=new Vector2(bounds.z-bounds.x+4,bounds.w-bounds.y+4)*(size/320f);
        Vector2 foot=Vector2.Lerp(point,destination,Mathf.SmoothStep(0,1,progress));
        float scale= Mathf.Lerp(.80f,1,progress);
        // The feet pivot follows the planet. At the rim the body's up direction
        // follows its normal, so wide feet lie along the surface tangent.
        Vector2 normal=(foot-center).normalized;
        float surfaceAngle=-Mathf.Atan2(normal.x,normal.y)*Mathf.Rad2Deg;
        float angle=surfaceAngle*Mathf.Lerp(1f,.25f,progress);
        float clipping=0;
        float hidden=elapsed<1.4f ? 1-Mathf.SmoothStep(0,1,elapsed/1.4f) : 0;
        if(elapsed>=9.8f) {
            float hide=Mathf.SmoothStep(0,1,(elapsed-9.8f)/2f);
            hidden=hide;
            // Shrink about the planted feet; never move the pivot out into space.
            scale*=Mathf.Lerp(1,.40f,hide);
            angle+=hide*10f;
        }
        float drawnHeight=portrait.sizeDelta.y*scale;
        clipping=(drawnHeight+3)*hidden;
        Quaternion surfaceRotation=Quaternion.Euler(0,0,angle);
        Vector2 surfaceUp=surfaceRotation*Vector3.up;
        // A sorting-order change hides the entire sprite at once. This moving
        // tangent mask instead occludes feet, then torso, then head behind the planet.
        reveal.localRotation=surfaceRotation;
        reveal.anchoredPosition=foot+surfaceUp*clipping;
        reveal.sizeDelta=new Vector2(150,Mathf.Max(0,drawnHeight+8-clipping));
        portrait.anchoredPosition=new Vector2(0,-clipping-2f*size/320f*scale);
        // Walking still uses distinct drawings; only perspective scale/exit tilt are transforms.
        portrait.localScale=Vector3.one*scale;
        portrait.localRotation=Quaternion.identity;
        bubble.gameObject.SetActive(speaking && elapsed>=3.8f && elapsed<6.8f);
        bubble.anchoredPosition = new Vector2(
            Mathf.Clamp(foot.x - 185, canvas.rect.xMin + 138, canvas.rect.xMax - 138),
            Mathf.Clamp(foot.y + 34, canvas.rect.yMin + 47, canvas.rect.yMax - 47));
    }

    private void OnDisable()
    {
        if (canvas != null) canvas.gameObject.SetActive(false);
        active = false;
    }
    private void OnEnable()
    {
        if (canvas != null) canvas.gameObject.SetActive(true);
        SchedulePeek();
    }
    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        foreach(var frame in frames) if(frame!=null)Destroy(frame);
    }
}
