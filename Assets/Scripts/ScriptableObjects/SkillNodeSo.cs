using System.Collections.Generic;
using UnityEngine;

public enum UnlockType
{
    None = 0,
    Planter_2x2 = 1,
    Planter_2x3 = 2,
    TileBehavior_Explosive = 3,
    TileBehavior_Duplicate = 4,
    TileBehavior_Tornado = 5,
    Planter_1x3 = 6,
    TileBehavior_Boomerang = 7,
    TileBehavior_Electric = 8
}

[System.Serializable]
public class SkillPrerequisite
{
    public SkillNodeSO node;
    [Min(1)] public int level = 1;
}

[System.Serializable]
public class SkillNodeTier
{
    public ResourceType costType = ResourceType.Gold; // hangi kaynakla alınıyor
    public int cost;
    public List<StatModifier> effects = new();
}

[CreateAssetMenu(menuName = "Game/SkillNode")]
public class SkillNodeSO : ScriptableObject
{
    [Header("Kimlik")]
    public string nodeName;
    public Sprite icon;
    public bool explicitPrerequisites;
    public List<SkillPrerequisite> prerequisites = new();
    public Vector2Int targetRounds;

    [Header("Konum — 8 yönlü komşuluk buradan hesaplanır")]
    public Vector2Int gridPosition;

    [Header("Seviyeler (sırayla 1, 2, 3...)")]
    public List<SkillNodeTier> tiers = new();

    [Header("Mekanik Açma (opsiyonel, max seviyede tetiklenir)")]
    public UnlockType unlockType = UnlockType.None;

    // Oyuncuya görünen ad: çeviri tablosundan. Sondaki sıra eki (" – 1", " - 2") çevrilmez; adın gövdesi anahtarı verir
    // ("Keskin Başlangıç – 1" → data.skill.keskin_baslangic.name + " – 1"). İki denge sürümündeki aynı adlı düğüm tek satırdır.
    // Anahtar yoksa asset'teki yazı.
    public string DisplayName
    {
        get
        {
            if (string.IsNullOrEmpty(nodeName)) return nodeName;
            int cut = OrderSuffixStart(nodeName);
            string stem = cut < 0 ? nodeName : nodeName.Substring(0, cut);
            string name = GameText.Data(NameKey(stem), stem);
            return cut < 0 ? name : name + nodeName.Substring(cut);
        }
    }

    public string DisplayNameKey
    {
        get
        {
            if (string.IsNullOrEmpty(nodeName)) return null;
            int cut = OrderSuffixStart(nodeName);
            return NameKey(cut < 0 ? nodeName : nodeName.Substring(0, cut));
        }
    }

    public static string NameKey(string stem) => "data.skill." + GameText.Slug(stem) + ".name";

    // Sıra ekinin başladığı yer ("Akıcı Kesim - 2" → 11); ek yoksa -1.
    public static int OrderSuffixStart(string name)
    {
        int i = name.Length - 1, digits = 0;
        while (i >= 0 && char.IsDigit(name[i])) { i--; digits++; }
        if (digits == 0) return -1;
        while (i >= 0 && name[i] == ' ') i--;
        if (i < 0 || name[i] != '-' && name[i] != '–') return -1;
        i--;
        while (i >= 0 && name[i] == ' ') i--;
        return i < 0 ? -1 : i + 1;
    }
}
