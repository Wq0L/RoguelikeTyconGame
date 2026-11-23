using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AÇIK / KAPALI düğmesi. Açıkken yeşil, kapalıyken kırmızı. Comic kiti (20 Kasım 2026): kitin buton sprite'ları ve SpriteSwap
// hâlleri (kurucu SetKitSprites ile yazar); boşsa eski tema paleti (ComicButtonVisual).
public sealed class OptionToggle : OptionRow
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text state;
    [SerializeField] private ComicUITheme theme;
    // Çeviri anahtarı (eski sahnelerde düz yazı da olabilir: GameText.Resolve).
    [SerializeField] private string onText = "ui.options.on";
    [SerializeField] private string offText = "ui.options.off";

    [SerializeField] private Sprite onSprite;
    [SerializeField] private Sprite offSprite;
    [SerializeField] private SpriteState onStates;
    [SerializeField] private SpriteState offStates;

    public Action<bool> Changed;
    private bool isOn;
    public bool IsOn => isOn;

#if UNITY_EDITOR
    public void SetKitSprites(Sprite on, SpriteState onState, Sprite off, SpriteState offState)
    {
        onSprite = on; onStates = onState; offSprite = off; offStates = offState;
    }
#endif

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
        state.text = GameText.Resolve(isOn ? onText : offText);
        if (onSprite != null && offSprite != null && button.targetGraphic is Image image)
        {
            image.sprite = isOn ? onSprite : offSprite;
            button.spriteState = isOn ? onStates : offStates;
            OptionsUI.RefreshState(button);
            return;
        }
        if (theme == null || !button.TryGetComponent(out ComicButtonVisual visual)) return;
        Material[] palette = isOn ? theme.green : theme.red;
        if (palette == null || palette.Length < 4) return;
        visual.normal = palette[0];
        visual.hover = palette[1];
        visual.pressed = palette[2];
        visual.disabled = palette[3];
    }
}
