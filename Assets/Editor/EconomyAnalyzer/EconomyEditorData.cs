using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ClickerGame.EconomyAnalysis
{
    public static class EconomyEditorData
    {
        public static EconomyBalanceProfileSO Defaults()
        {
            var profile = ScriptableObject.CreateInstance<EconomyBalanceProfileSO>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            profile.planter = AssetDatabase.LoadAssetAtPath<PlanterSO>("Assets/ScriptableObjects/Planters/GrassPlanter 2x3.asset");
            profile.coreStats = AssetDatabase.LoadAssetAtPath<CoreStatsSO>("Assets/ScriptableObjects/Stats/CoreStat/CoreStat.asset");
            profile.healthScaling = Resources.Load<PlantHealthScalingSO>("PlantHealthScaling");
            profile.resonanceRules = Resources.Load<ResonanceRulesSO>("ResonanceRules");
            profile.currentXpCurve = AssetDatabase.FindAssets("t:ProgressionSO").Select(g =>
                AssetDatabase.LoadAssetAtPath<ProgressionSO>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
            profile.skillTree = AssetDatabase.FindAssets("t:SkillNodeSO", new[] { "Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree" })
                .Select(g => AssetDatabase.LoadAssetAtPath<SkillNodeSO>(AssetDatabase.GUIDToAssetPath(g)))
                .OrderBy(n => AssetDatabase.GetAssetPath(n), StringComparer.Ordinal).ToList();
            return profile;
        }

        public static int Spawners(EconomyBalanceProfileSO profile, out string source)
        {
            if (profile.spawnerCountOverride > 0) { source = "Explicit analysis override"; return profile.spawnerCountOverride; }
            var planter = profile.planter;
            if (planter == null || planter.prefab == null) throw new ArgumentException("Planter prefab missing; provide an explicit spawner count.");
            var brains = planter.prefab.GetComponentsInChildren<PlanterBrain>(true);
            if (brains.Length != 1) throw new ArgumentException("Expected exactly one PlanterBrain in prefab; use a count override for other configurations.");
            var serialized = new SerializedObject(brains[0]);
            var points = serialized.FindProperty("spawnPoints");
            if (points == null) throw new ArgumentException("PlanterBrain spawnPoints field not found.");
            if (points.arraySize == 0)
            {
                source = "Prefab: automatic occupied-cell spawners";
                return planter.sizeX * planter.sizeZ;
            }
            var seen = new HashSet<UnityEngine.Object>();
            for (int i = 0; i < points.arraySize; i++)
            {
                var point = points.GetArrayElementAtIndex(i).objectReferenceValue;
                if (point == null || !seen.Add(point)) throw new ArgumentException("Null/duplicate spawnPoint in prefab.");
            }
            source = "Prefab: explicit spawnPoints (placement validity remains a gameplay check)";
            return points.arraySize;
        }

        public static List<EconomySnapshot> History(EconomyBalanceProfileSO profile, bool resonance)
        {
            if (profile.history == null || profile.history.Count == 0) throw new ArgumentException("Add a history row beginning at round 1.");
            var rows = profile.history.OrderBy(r => r.fromRound).ToArray();
            if (rows[0].fromRound != 1 || rows.Any(r => r.fromRound < 1 || r.fromRound > 130) ||
                rows.Select(r => r.fromRound).Distinct().Count() != rows.Length)
                throw new ArgumentException("History must begin at 1 and have unique starts in 1–130.");
            int count = Spawners(profile, out _), active = 0;
            var result = new List<EconomySnapshot>();
            for (int round = 1; round <= 130; round++)
            {
                while (active + 1 < rows.Length && rows[active + 1].fromRound <= round) active++;
                result.Add(EconomyCalculator.Analyze(EconomyCalculator.Resolve(profile, rows[active], round, count, resonance)));
            }
            return result;
        }
    }
}
