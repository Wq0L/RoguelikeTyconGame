using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 0-1 arası kaydırıcı (ses seviyeleri, ekran sarsıntısı). Değer yüzde olarak yazılır.
public sealed class OptionSlider : OptionRow
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text value;

    public Action<float> Changed;
    private bool silent;

    private void Awake()
    {
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    public void Setup(float amount)
    {
        silent = true;
        slider.value = Mathf.Clamp01(amount);
        silent = false;
        Refresh();
    }

    private void OnSliderChanged(float amount)
    {
        Refresh();
        if (!silent) Changed?.Invoke(amount);
    }

    private void Refresh() => value.text = Mathf.RoundToInt(slider.value * 100f) + "%";
}
