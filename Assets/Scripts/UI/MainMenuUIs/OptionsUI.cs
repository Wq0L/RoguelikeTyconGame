using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OptionsUI : MonoBehaviour
{
    [Header("Çözünürlük")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    [Header("Tam Ekran")]
    [SerializeField] private Toggle fullscreenToggle;

    private List<Resolution> filteredResolutions = new List<Resolution>();

    private void Start()
    {
        SetupResolution();
        SetupFullscreen();
    }

    // ---------- ÇÖZÜNÜRLÜK ----------

    private void SetupResolution()
    {
        Resolution[] allResolutions = Screen.resolutions;
        filteredResolutions.Clear();

        List<string> options = new List<string>();
        int currentIndex = 0;

        for (int i = 0; i < allResolutions.Length; i++)
        {
            Resolution res = allResolutions[i];

            bool alreadyAdded = filteredResolutions.Exists(
                r => r.width == res.width && r.height == res.height);
            if (alreadyAdded) continue;

            filteredResolutions.Add(res);
            options.Add($"{res.width} x {res.height}");

            if (res.width == Screen.currentResolution.width &&
                res.height == Screen.currentResolution.height)
            {
                currentIndex = filteredResolutions.Count - 1;
            }
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentIndex;
        resolutionDropdown.RefreshShownValue();

        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    private void OnResolutionChanged(int index)
    {
        Resolution res = filteredResolutions[index];
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);
    }

    // ---------- TAM EKRAN ----------

    private void SetupFullscreen()
    {
        fullscreenToggle.isOn = Screen.fullScreen;
        fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
    }

    private void OnFullscreenChanged(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }
}