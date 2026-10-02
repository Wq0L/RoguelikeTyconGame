using System.Collections.Generic;
using UnityEngine;

// Boss ödül havuzu: profil hangi ödüllerin sunulacağını ve bir ekranda kaç seçenek olacağını buradan alır.
[CreateAssetMenu(menuName = "ClickerGame/Boss Reward Pool", fileName = "BossRewardPool")]
public sealed class BossRewardPoolSO : ScriptableObject
{
    public List<BossRewardSO> rewards = new();
    [Tooltip("Kırılma ödülleri (Bölüm 3.6). Sunulabilir ve henüz alınmamış olan varsa teklifin bir slotu bunların arasından rastgele " +
             "seçilir; kalan slotlar 'rewards' listesinden gelir. Uygun kırılma ödülü kalmayınca teklif tamamen 'rewards' listesinden olur.")]
    public List<BossRewardSO> breakthroughs = new();
    [Tooltip("Bir boss sonrası gösterilen farklı seçenek sayısı. Uygun ödül daha azsa o kadar gösterilir.")]
    [Range(1, 4)] public int choices = 3;
}
