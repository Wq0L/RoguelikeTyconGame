using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Kategorili ayarlar paneli (Görüntü / Ses / Oynanış / Kontroller). Görseller sahnede durur;
// kurmak/yenilemek için: Tools > Comic UI > Build Options Menu. Değerler GameSettings'te tutulur:
// her değişiklik anında uygulanır, panel kapanınca diske yazılır.
public class OptionsUI : MonoBehaviour
{
    [Serializable]
    public class Tab
    {
        public Button button;
        public GameObject page;
    }

    [SerializeField] private ComicUITheme theme;
    [SerializeField] private List<Tab> tabs = new List<Tab>();
    [SerializeField] private TMP_Text description;
    [SerializeField, TextArea] private string defaultDescription = "Bir ayarın üzerine gel, ne işe yaradığı burada görünsün.";
    [SerializeField] private Button resetButton;

    [Header("Görüntü")]
    [SerializeField] private OptionStepper resolution;
    [SerializeField] private OptionStepper windowMode;
    [SerializeField] private OptionToggle vSync;
    [SerializeField] private OptionStepper fpsLimit;
    [SerializeField] private OptionStepper quality;
    [SerializeField] private OptionToggle backgroundEffects;

    [Header("Ses")]
    [SerializeField] private OptionSlider masterVolume;
    [SerializeField] private OptionSlider musicVolume;
    [SerializeField] private OptionSlider effectsVolume;
    [SerializeField] private OptionToggle muteInBackground;

    [Header("Oynanış")]
    [SerializeField] private OptionSlider screenShake;
    [SerializeField] private OptionToggle cameraMotion;
    [SerializeField] private OptionToggle damageNumbers;
    [SerializeField] private OptionToggle gridCoordinates;
    [SerializeField] private OptionToggle rarityAuras;
    [SerializeField] private OptionToggle roundEndSlowMotion;
    [SerializeField] private OptionToggle screenFlashes;

    private static readonly FullScreenMode[] WindowModes =
        { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
    private static readonly string[] WindowModeNames = { "Tam Ekran", "Kenarlıksız", "Pencere" };

    private List<Vector2Int> resolutions = new List<Vector2Int>();
    private int selectedTab;

    private void Awake()
    {
#if UNITY_EDITOR
        if (tabs.Count == 0)
            Debug.LogWarning("Ayarlar menüsü sahneye henüz kurulmamış: Unity menüsünden Tools > Comic UI > Build Options Menu'yü çalıştır.", this);
#endif
        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i;
            if (tabs[i].button != null) tabs[i].button.onClick.AddListener(() => SelectTab(index));
        }
        if (resetButton != null) resetButton.onClick.AddListener(ResetToDefaults);
        foreach (OptionRow row in GetComponentsInChildren<OptionRow>(true)) row.HoverChanged = ShowDescription;
        Bind();
    }

    private void OnEnable()
    {
        Refresh();
        SelectTab(selectedTab);
        ShowDescription(null);
    }

    // Menüden çıkarken kalıcı hale getir.
    private void OnDisable() => GameSettings.Save();

    private void Bind()
    {
        if (resolution != null) resolution.Changed = i =>
        {
            if (i < 0 || i >= resolutions.Count) return;
            GameSettings.ResolutionWidth = resolutions[i].x;
            GameSettings.ResolutionHeight = resolutions[i].y;
            GameSettings.Commit();
        };
        Step(windowMode, i => GameSettings.WindowMode = WindowModes[Mathf.Clamp(i, 0, WindowModes.Length - 1)]);
        Toggle(vSync, on => GameSettings.VSync = on);
        Step(fpsLimit, i => GameSettings.FpsLimit = GameSettings.FpsOptions[Mathf.Clamp(i, 0, GameSettings.FpsOptions.Length - 1)]);
        Step(quality, i => GameSettings.QualityLevel = i);
        Toggle(backgroundEffects, on => GameSettings.BackgroundEffects = on);

        Slide(masterVolume, v => GameSettings.MasterVolume = v);
        Slide(musicVolume, v => GameSettings.MusicVolume = v);
        Slide(effectsVolume, v => GameSettings.EffectsVolume = v);
        Toggle(muteInBackground, on => GameSettings.MuteInBackground = on);

        Slide(screenShake, v => GameSettings.ScreenShake = v);
        Toggle(cameraMotion, on => GameSettings.CameraMotion = on);
        Toggle(damageNumbers, on => GameSettings.DamageNumbers = on);
        Toggle(gridCoordinates, on => GameSettings.GridCoordinates = on);
        Toggle(rarityAuras, on => GameSettings.RarityAuras = on);
        Toggle(roundEndSlowMotion, on => GameSettings.RoundEndSlowMotion = on);
        Toggle(screenFlashes, on => GameSettings.ScreenFlashes = on);
    }

    private static void Step(OptionStepper row, Action<int> set)
    {
        if (row != null) row.Changed = i => { set(i); GameSettings.Commit(); };
    }

    private static void Toggle(OptionToggle row, Action<bool> set)
    {
        if (row != null) row.Changed = on => { set(on); GameSettings.Commit(); };
    }

    private static void Slide(OptionSlider row, Action<float> set)
    {
        if (row != null) row.Changed = v => { set(v); GameSettings.Commit(); };
    }

    // Satırları GameSettings'teki güncel değerlerle doldurur.
    private void Refresh()
    {
        if (resolution != null)
        {
            resolutions = GameSettings.ResolutionOptions();
            int width = GameSettings.ResolutionWidth > 0 ? GameSettings.ResolutionWidth : Screen.width;
            int height = GameSettings.ResolutionHeight > 0 ? GameSettings.ResolutionHeight : Screen.height;
            var names = new string[resolutions.Count];
            int current = resolutions.Count - 1;
            for (int i = 0; i < resolutions.Count; i++)
            {
                names[i] = resolutions[i].x + " x " + resolutions[i].y;
                if (resolutions[i].x == width && resolutions[i].y == height) current = i;
            }
            resolution.Setup(names, current);
        }
        windowMode?.Setup(WindowModeNames, Mathf.Max(0, Array.IndexOf(WindowModes, GameSettings.WindowMode)));
        vSync?.Setup(GameSettings.VSync);
        if (fpsLimit != null)
        {
            var names = new string[GameSettings.FpsOptions.Length];
            for (int i = 0; i < names.Length; i++)
                names[i] = GameSettings.FpsOptions[i] > 0 ? GameSettings.FpsOptions[i] + " FPS" : "Sınırsız";
            fpsLimit.Setup(names, Mathf.Max(0, Array.IndexOf(GameSettings.FpsOptions, GameSettings.FpsLimit)));
        }
        if (quality != null)
        {
            string[] names = QualitySettings.names;
            var shown = new string[names.Length];
            for (int i = 0; i < names.Length; i++) shown[i] = QualityName(names[i]);
            quality.Setup(shown, GameSettings.QualityLevel);
        }
        backgroundEffects?.Setup(GameSettings.BackgroundEffects);

        masterVolume?.Setup(GameSettings.MasterVolume);
        musicVolume?.Setup(GameSettings.MusicVolume);
        effectsVolume?.Setup(GameSettings.EffectsVolume);
        muteInBackground?.Setup(GameSettings.MuteInBackground);

        screenShake?.Setup(GameSettings.ScreenShake);
        cameraMotion?.Setup(GameSettings.CameraMotion);
        damageNumbers?.Setup(GameSettings.DamageNumbers);
        gridCoordinates?.Setup(GameSettings.GridCoordinates);
        rarityAuras?.Setup(GameSettings.RarityAuras);
        roundEndSlowMotion?.Setup(GameSettings.RoundEndSlowMotion);
        screenFlashes?.Setup(GameSettings.ScreenFlashes);
    }

    private static string QualityName(string level) => level switch
    {
        "Mobile" => "Düşük",
        "PC" => "Yüksek",
        _ => level
    };

    private void SelectTab(int index)
    {
        if (tabs.Count == 0) return;
        selectedTab = Mathf.Clamp(index, 0, tabs.Count - 1);
        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i].page != null) tabs[i].page.SetActive(i == selectedTab);
            if (theme != null) SetPalette(tabs[i].button, i == selectedTab ? theme.green : theme.blue);
        }
        ShowDescription(null);
    }

    private static void SetPalette(Button button, Material[] palette)
    {
        if (button == null || palette == null || palette.Length < 4 || !button.TryGetComponent(out ComicButtonVisual visual)) return;
        visual.normal = palette[0];
        visual.hover = palette[1];
        visual.pressed = palette[2];
        visual.disabled = palette[3];
    }

    private void ResetToDefaults()
    {
        GameSettings.SetDefaults();
        GameSettings.Commit();
        Refresh();
    }

    private void ShowDescription(string text)
    {
        if (description != null) description.text = string.IsNullOrEmpty(text) ? defaultDescription : text;
    }
}
