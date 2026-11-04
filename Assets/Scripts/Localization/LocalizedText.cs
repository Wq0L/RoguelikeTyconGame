using TMPro;
using UnityEngine;

// Sahnedeki / prefab'daki bir yazıyı çeviri tablosuna bağlar (4 Kasım 2026): yazı anahtardan gelir (GameText), dil ya da
// tablo değişince kendiliğinden yenilenir. Bileşendeki TMP yazısının elle girilmiş metni yalnız editörde yer tutucudur.
// Yer tutuculu metin için kod SetKey(anahtar, değerler…) çağırır. İş yalnız açılışta ve dil / tablo değişince yapılır.
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class LocalizedText : MonoBehaviour
{
    [SerializeField] private string key;
    private TMP_Text label;
    private object[] args;

    public string Key => key;

    public void SetKey(string newKey, params object[] values)
    {
        key = newKey;
        args = values != null && values.Length > 0 ? values : null;
        if (isActiveAndEnabled) Apply();
    }

    private void OnEnable()
    {
        GameText.Changed += Apply;
        Apply();
    }

    private void OnDisable() => GameText.Changed -= Apply;

    private void Apply()
    {
        if (string.IsNullOrEmpty(key)) return;
        if (label == null) label = GetComponent<TMP_Text>();
        label.text = args == null ? GameText.Get(key) : GameText.Format(key, args);
    }
}
