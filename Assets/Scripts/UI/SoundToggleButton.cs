using UnityEngine;
using UnityEngine.UI;

// Hazırlık ekranındaki ses karesi (22 Kasım 2026; kullanıcı: "ayarların yanına aynı şekilde bir ses aç / kapa; kapalıyken kırmızı
// çizgili gri tonda, açıkken canlı, kendi renginde"). Bütün sesleri açar / kapatır: GameSettings.SoundMuted (kalıcı).
// Açık: lacivert kare + krem hoparlör (ayarlar dişlisiyle aynı aile). Kapalı: gri kare (gri = pasif) + gri hoparlör + kırmızı
// çapraz çizgi (kırmızı = kapalı). Ayar başka yerden de değişebilir (ayarlar penceresi > Ses > Tüm Sesler): GameSettings.Changed
// ile güncellenir. Görünüm sahnede (Tools > Comic UI > Oyun İçi Ayarlar); kod yalnız hâli değiştirir. Karede iş yok.
public sealed class SoundToggleButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private Sprite onSquare, offSquare, onIcon, offIcon;
    [SerializeField] private SpriteState onStates, offStates;

    public bool ShowsOn { get; private set; }

    private void Awake()
    {
        if (button != null) button.onClick.AddListener(Toggle);
    }

    private void OnEnable()
    {
        GameSettings.Changed += Refresh;
        Refresh();
    }

    private void OnDisable() => GameSettings.Changed -= Refresh;

    public void Toggle()
    {
        GameSettings.SoundMuted = !GameSettings.SoundMuted;
        GameSettings.Commit();   // hemen uygular (SoundManager) ve haber verir (Refresh)
        GameSettings.Save();     // oyuncunun tercihi kalıcı
    }

    private void Refresh()
    {
        bool on = !GameSettings.SoundMuted;
        if (on == ShowsOn && icon != null && icon.sprite == (on ? onIcon : offIcon)) return;
        ShowsOn = on;
        if (button != null && button.targetGraphic is Image square)
        {
            square.sprite = on ? onSquare : offSquare;
            button.spriteState = on ? onStates : offStates;
            OptionsUI.RefreshState(button);
        }
        if (icon != null) icon.sprite = on ? onIcon : offIcon;
    }

#if UNITY_EDITOR
    // Kurucu (OptionsMenuBuilder) için.
    public void SetSprites(Button target, Image iconImage, Sprite squareOn, SpriteState statesOn, Sprite squareOff, SpriteState statesOff, Sprite iconOn, Sprite iconOff)
    {
        button = target; icon = iconImage;
        onSquare = squareOn; onStates = statesOn; offSquare = squareOff; offStates = statesOff;
        onIcon = iconOn; offIcon = iconOff;
    }
#endif
}
