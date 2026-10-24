using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CritBurstRemovalVerification
{
    public static void RunBatch()
    {
        try
        {
            int pools = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:BossRewardPoolSO"))
            {
                var pool = AssetDatabase.LoadAssetAtPath<BossRewardPoolSO>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var reward in RewardOfferLab.All(pool))
                    if (reward.id == "kritik_patlama") throw new Exception("Retired reward in " + pool.name);
                pools++;
            }
            var data = AssetDatabase.LoadAssetAtPath<CritBurstEffectSO>("Assets/ScriptableObjects/RunEffects/GecOyunV1/KritikPatlamaEffect_G1.asset");
            if (data == null || data.CanTake(null)) throw new Exception("Legacy effect must be unavailable");
            var effect = data.Create();
            if (effect.OnAdded(RunEffectSO.NoPlacement)) throw new Exception("Legacy effect can still be added");
            effect.AttackEnd(null); // The retired shell must never inspect attacks or emit damage/cues.
            File.WriteAllText("Logs/CritBurstRemovalVerification.txt", "PASS: " + pools + " pools exclude Kritik Patlama; legacy effect rejects grants and has no attack behavior.");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText("Logs/CritBurstRemovalVerification.txt", "FAIL " + e);
            EditorApplication.Exit(1);
        }
    }
}
