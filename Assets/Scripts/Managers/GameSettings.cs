using System;
using System.Collections.Generic;
using UnityEngine;

// Oyuncu ayarları. PlayerPrefs'te saklanır ve oyun açılırken (ilk sahneden önce) uygulanır.
// Görüntü ve ses burada uygulanır; oynanış ayarlarını ilgili sistemler doğrudan okur
// (ör. CameraFeel sarsıntı gücü, VFXManager hasar yazıları).
// Menü değişiklikleri Commit ile anında uygulanır, diske ise panel kapanınca Save ile yazılır.
public static class GameSettings
{
    // ---------- Görüntü ----------
    public static int ResolutionWidth;   // 0: dokunma (oyuncu seçene kadar sistemin çözünürlüğü)
    public static int ResolutionHeight;
    public static FullScreenMode WindowMode;
    public static bool VSync;
    public static int FpsLimit;          // 0: sınırsız (VSync kapalıyken geçerli)
    public static int QualityLevel;
    public static bool BackgroundEffects;

    // ---------- Ses ----------
    public static float MasterVolume;
    public static float MusicVolume;
    public static float EffectsVolume;
    public static bool MuteInBackground;

    // ---------- Oynanış ----------
    public static float ScreenShake;
    public static bool CameraMotion;
    public static bool DamageNumbers;
    public static bool GridCoordinates;
    public static bool RarityAuras;
    public static bool RoundEndSlowMotion;
    public static bool ScreenFlashes;

    public static readonly int[] FpsOptions = { 30, 60, 120, 144, 165, 240, 0 };
    public static event Action Changed;

    private const string Prefix = "settings.";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Changed = null;
        Application.focusChanged -= OnFocusChanged;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // Doğrulama/performans testleri oyuncu tercihlerinden etkilenmesin.
        if (Application.isBatchMode)
        {
            SetDefaults();
            return;
        }
        Load();
        ApplyDisplay();
        ApplyAudio();
        Application.focusChanged -= OnFocusChanged;
        Application.focusChanged += OnFocusChanged;
    }

    public static void SetDefaults()
    {
        ResolutionWidth = ResolutionHeight = 0;
        WindowMode = Screen.fullScreenMode;
        VSync = true;
        FpsLimit = 0;
        QualityLevel = QualitySettings.GetQualityLevel();
        BackgroundEffects = true;

        MasterVolume = 1f;
        MusicVolume = 0.8f;
        EffectsVolume = 1f;
        MuteInBackground = true;

        ScreenShake = 1f;
        CameraMotion = true;
        DamageNumbers = true;
        GridCoordinates = true;
        RarityAuras = true;
        RoundEndSlowMotion = true;
        ScreenFlashes = true;
    }

    public static void Load()
    {
        SetDefaults();
        ResolutionWidth = PlayerPrefs.GetInt(Prefix + "resolutionWidth", ResolutionWidth);
        ResolutionHeight = PlayerPrefs.GetInt(Prefix + "resolutionHeight", ResolutionHeight);
        WindowMode = (FullScreenMode)PlayerPrefs.GetInt(Prefix + "windowMode", (int)WindowMode);
        VSync = GetBool("vsync", VSync);
        FpsLimit = PlayerPrefs.GetInt(Prefix + "fpsLimit", FpsLimit);
        QualityLevel = PlayerPrefs.GetInt(Prefix + "quality", QualityLevel);
        BackgroundEffects = GetBool("backgroundEffects", BackgroundEffects);

        MasterVolume = PlayerPrefs.GetFloat(Prefix + "masterVolume", MasterVolume);
        MusicVolume = PlayerPrefs.GetFloat(Prefix + "musicVolume", MusicVolume);
        EffectsVolume = PlayerPrefs.GetFloat(Prefix + "effectsVolume", EffectsVolume);
        MuteInBackground = GetBool("muteInBackground", MuteInBackground);

        ScreenShake = PlayerPrefs.GetFloat(Prefix + "screenShake", ScreenShake);
        CameraMotion = GetBool("cameraMotion", CameraMotion);
        DamageNumbers = GetBool("damageNumbers", DamageNumbers);
        GridCoordinates = GetBool("gridCoordinates", GridCoordinates);
        RarityAuras = GetBool("rarityAuras", RarityAuras);
        RoundEndSlowMotion = GetBool("roundEndSlowMotion", RoundEndSlowMotion);
        ScreenFlashes = GetBool("screenFlashes", ScreenFlashes);
    }

    public static void Save()
    {
        PlayerPrefs.SetInt(Prefix + "resolutionWidth", ResolutionWidth);
        PlayerPrefs.SetInt(Prefix + "resolutionHeight", ResolutionHeight);
        PlayerPrefs.SetInt(Prefix + "windowMode", (int)WindowMode);
        SetBool("vsync", VSync);
        PlayerPrefs.SetInt(Prefix + "fpsLimit", FpsLimit);
        PlayerPrefs.SetInt(Prefix + "quality", QualityLevel);
        SetBool("backgroundEffects", BackgroundEffects);

        PlayerPrefs.SetFloat(Prefix + "masterVolume", MasterVolume);
        PlayerPrefs.SetFloat(Prefix + "musicVolume", MusicVolume);
        PlayerPrefs.SetFloat(Prefix + "effectsVolume", EffectsVolume);
        SetBool("muteInBackground", MuteInBackground);

        PlayerPrefs.SetFloat(Prefix + "screenShake", ScreenShake);
        SetBool("cameraMotion", CameraMotion);
        SetBool("damageNumbers", DamageNumbers);
        SetBool("gridCoordinates", GridCoordinates);
        SetBool("rarityAuras", RarityAuras);
        SetBool("roundEndSlowMotion", RoundEndSlowMotion);
        SetBool("screenFlashes", ScreenFlashes);
        PlayerPrefs.Save();
    }

    // Değişikliği hemen uygular (diske yazmaz; menü kapanınca Save çağrılır).
    public static void Commit()
    {
        ApplyDisplay();
        ApplyAudio();
        Changed?.Invoke();
    }

    public static void ApplyDisplay()
    {
        if (QualityLevel >= 0 && QualityLevel < QualitySettings.names.Length && QualityLevel != QualitySettings.GetQualityLevel())
            QualitySettings.SetQualityLevel(QualityLevel, true);
        // Kalite seviyesi kendi VSync değerini getirir; oyuncunun seçimi ondan sonra uygulanır.
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = VSync || FpsLimit <= 0 ? -1 : FpsLimit;

        // Editörde pencere boyutu Game view'a aittir; çözünürlük sadece build'de uygulanır.
        if (Application.isEditor) return;
        if (ResolutionWidth > 0 && ResolutionHeight > 0)
        {
            if (Screen.width != ResolutionWidth || Screen.height != ResolutionHeight || Screen.fullScreenMode != WindowMode)
                Screen.SetResolution(ResolutionWidth, ResolutionHeight, WindowMode);
        }
        else if (Screen.fullScreenMode != WindowMode)
        {
            Screen.fullScreenMode = WindowMode;
        }
    }

    public static void ApplyAudio()
    {
        AudioListener.volume = Mathf.Clamp01(MasterVolume);
    }

    private static void OnFocusChanged(bool focused)
    {
        AudioListener.pause = MuteInBackground && !focused;
    }

    // Tekrarsız çözünürlükler (en yüksek tazeleme hızıyla), küçükten büyüğe.
    public static List<Vector2Int> ResolutionOptions()
    {
        var result = new List<Vector2Int>();
        foreach (Resolution resolution in Screen.resolutions)
        {
            var size = new Vector2Int(resolution.width, resolution.height);
            if (!result.Contains(size)) result.Add(size);
        }
        if (result.Count == 0) result.Add(new Vector2Int(Screen.width, Screen.height));
        result.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return result;
    }

    private static bool GetBool(string key, bool fallback) => PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) != 0;
    private static void SetBool(string key, bool value) => PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
}
