using System;
using System.Collections.Generic;
using UnityEngine;

// Yetenek ikonları (12 Kasım 2026; kullanıcı 19 ikonu yaptırdı, "A seçeneği"). SkillIconCatalog düğümün etkisinden bir kimlik
// seçer ("skill-attack" …); bu set kimliği çizilmiş sprite'a bağlar. Sette sprite yoksa ağaç eski kodla çizilen simgeyi gösterir.
// Asset: Assets/Resources/SkillIconSet.asset (Inspector'dan değiştirilebilir). Kuran / güncelleyen: Tools > Oyun > Yetenek İkonları.
// Yetenek verisine (SkillNodeSO) dokunmaz.
[CreateAssetMenu(menuName = "ClickerGame/Skill Icon Set", fileName = "SkillIconSet")]
public sealed class SkillIconSetSO : ScriptableObject
{
    public const string ResourcePath = "SkillIconSet";

    [Serializable]
    public struct Entry
    {
        [Tooltip("SkillIconCatalog kimliği, 'skill-' öneki olmadan: attack, speed, area …")]
        public string id;
        public Sprite sprite;
    }

    public List<Entry> entries = new();

    private Dictionary<string, Sprite> lookup;
    private static SkillIconSetSO active;
    private static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { active = null; loaded = false; }

    private void OnValidate() => lookup = null;   // Inspector'da değişince yeniden kurulsun

    // Katalog tarifi ("skill-attack") ya da yalın kimlik ("attack") için sprite; yoksa null. Bellek ayırmaz (ilk çağrıdan sonra).
    public static Sprite For(string recipe)
    {
        if (string.IsNullOrEmpty(recipe)) return null;
        if (!loaded) { active = Resources.Load<SkillIconSetSO>(ResourcePath); loaded = true; }
        if (active == null) return null;
        if (active.lookup == null)
        {
            active.lookup = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (Entry e in active.entries)
                if (!string.IsNullOrEmpty(e.id) && e.sprite != null) active.lookup["skill-" + e.id] = e.sprite;
        }
        string key = recipe.StartsWith("skill-", StringComparison.Ordinal) ? recipe : "skill-" + recipe;
        return active.lookup.TryGetValue(key, out Sprite sprite) ? sprite : null;
    }
}
