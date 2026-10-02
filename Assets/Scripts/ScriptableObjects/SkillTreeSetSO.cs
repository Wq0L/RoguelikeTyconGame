using System;
using System.Collections.Generic;
using UnityEngine;

// Bir run profilinin skill tree'si (Bölüm 3.5). Sahnedeki ağaç arayüzü ortak düğümlere göre dizilidir; set, her arayüz yuvasında
// (source) bu run'da hangi düğümün (node) gösterileceğini söyler. node boşsa ya da yuva listede yoksa o yuva bu run'da kapalıdır.
// Düğümler normal SkillNodeSO'dur: fiyat, ön koşul, etki ve konum kendi asset'lerindedir; önkoşullar aynı setin düğümlerini gösterir.
[CreateAssetMenu(menuName = "ClickerGame/Skill Tree Set", fileName = "SkillTreeSet")]
public sealed class SkillTreeSetSO : ScriptableObject
{
    [Serializable]
    public struct Slot
    {
        [Tooltip("Ortak ağaçtaki düğüm: sahnedeki arayüz yuvası bununla bulunur.")]
        public SkillNodeSO source;
        [Tooltip("Bu sette o yuvada duran düğüm. Boş: yuva kapalı.")]
        public SkillNodeSO node;
    }

    public List<Slot> slots = new();

    public List<SkillNodeSO> Nodes()
    {
        var result = new List<SkillNodeSO>();
        foreach (Slot slot in slots)
            if (slot.node != null && !result.Contains(slot.node)) result.Add(slot.node);
        return result;
    }

    // Arayüz yuvası için bu setin düğümü; yuva sette yoksa ya da kapalıysa null.
    public SkillNodeSO NodeFor(SkillNodeSO source)
    {
        if (source == null) return null;
        foreach (Slot slot in slots)
            if (slot.source == source) return slot.node;
        return null;
    }
}
