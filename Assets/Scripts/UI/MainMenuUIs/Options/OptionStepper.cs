using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "< değer >" seçici (çözünürlük, ekran modu, FPS sınırı...). Uçlarda başa sarar.
public sealed class OptionStepper : OptionRow
{
    [SerializeField] private Button previous;
    [SerializeField] private Button next;
    [SerializeField] private TMP_Text value;

    private string[] options = Array.Empty<string>();
    private int index;

    public Action<int> Changed;

    private void Awake()
    {
        previous.onClick.AddListener(() => Step(-1));
        next.onClick.AddListener(() => Step(1));
    }

    public void Setup(string[] items, int selected)
    {
        options = items ?? Array.Empty<string>();
        index = options.Length == 0 ? 0 : Mathf.Clamp(selected, 0, options.Length - 1);
        Refresh();
    }

    private void Step(int direction)
    {
        if (options.Length == 0) return;
        index = (index + direction + options.Length) % options.Length;
        Refresh();
        Changed?.Invoke(index);
    }

    private void Refresh() => value.text = options.Length > 0 ? options[index] : "-";
}
