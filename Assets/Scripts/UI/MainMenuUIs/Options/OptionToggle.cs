using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AÇIK / KAPALI düğmesi. Açıkken yeşil, kapalıyken kırmızı tema paleti (ComicButtonVisual).
public sealed class OptionToggle : OptionRow
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text state;
    [SerializeField] private ComicUITheme theme;
    [SerializeField] private string onText = "AÇIK";
    [SerializeField] private string offText = "KAPALI";

    public Action<bool> Changed;
    private bool isOn;

    private void Awake() => button.onClick.AddListener(Toggle);

    public void Setup(bool on)
    {
        isOn = on;
        Refresh();
    }

    private void Toggle()
    {
        isOn = !isOn;
        Refresh();
        Changed?.Invoke(isOn);
    }

    private void Refresh()
    {
        state.text = isOn ? onText : offText;
        if (theme == null || !button.TryGetComponent(out ComicButtonVisual visual)) return;
        Material[] palette = isOn ? theme.green : theme.red;
        if (palette == null || palette.Length < 4) return;
        visual.normal = palette[0];
        visual.hover = palette[1];
        visual.pressed = palette[2];
        visual.disabled = palette[3];
    }
}
