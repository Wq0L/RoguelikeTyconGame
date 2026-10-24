using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MechanicsTestLab
{
    // Mechanics Test Lab'ın sabitleri. Araç yalnız Editor derlemesindedir: oyuncu build'ine girmez.
    public static class Lab
    {
        public const string Menu = "Tools/Mechanics Test Lab";
        public const string ScenePath = "Assets/Scenes/MechanicsTestScene.unity";
        public const string GameScenePath = "Assets/Scenes/GameScene.unity";
        public const string DefaultProfile = "Run50_GecOyunV1";
        public static readonly int[] GridSizes = { 3, 5, 7, 9, 11 };
    }

    // Bir test komutunun sonucu: panel mesajı aynen gösterir, senaryo kurulumu listeler.
    public readonly struct Result
    {
        public readonly bool Ok;
        public readonly string Message;
        private Result(bool ok, string message) { Ok = ok; Message = message; }
        public static Result Success(string message) => new Result(true, message);
        public static Result Fail(string message) => new Result(false, message);
        public override string ToString() => (Ok ? "✓ " : "✗ ") + Message;
    }

    // Oyunun gerçek içerik kataloglarını okur (asset'ler ve aktif havuzlar). Hiçbir içerik burada elle listelenmez.
    public static class LabCatalog
    {
        private static List<TileModifierSO> tiles;
        private static List<PlanterSO> planters;
        private static List<PlantSO> plants;
        private static List<RunProfileSO> profiles;

        public static void Invalidate() { tiles = null; planters = null; plants = null; profiles = null; }

        private static List<T> LoadAll<T>() where T : Object
        {
            var result = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/ScriptableObjects" }))
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && !result.Contains(asset)) result.Add(asset);
            }
            return result;
        }

        // Tile modifier kataloğu: tür, sonra nadirlik sırasıyla.
        public static IReadOnlyList<TileModifierSO> Tiles
        {
            get
            {
                if (tiles != null) return tiles;
                tiles = LoadAll<TileModifierSO>();
                tiles.Sort((a, b) => a.modifierType != b.modifierType ? a.modifierType.CompareTo(b.modifierType) : a.rarity.CompareTo(b.rarity));
                return tiles;
            }
        }

        public static TileModifierSO Tile(TileModifierType type, TileRarity rarity)
        {
            foreach (TileModifierSO tile in Tiles) if (tile.modifierType == type && tile.rarity == rarity) return tile;
            return null;
        }

        public static IReadOnlyList<PlanterSO> Planters
        {
            get
            {
                if (planters != null) return planters;
                planters = LoadAll<PlanterSO>();
                planters.Sort((a, b) => a.sizeX * a.sizeZ != b.sizeX * b.sizeZ ? (a.sizeX * a.sizeZ).CompareTo(b.sizeX * b.sizeZ) : string.CompareOrdinal(a.name, b.name));
                return planters;
            }
        }

        public static PlanterSO Planter(string name)
        {
            foreach (PlanterSO planter in Planters) if (planter.name == name) return planter;
            return null;
        }

        // 1×1 saksı (hazır senaryolar için): katalogdaki en küçük saksı.
        public static PlanterSO SmallestPlanter => Planters.Count > 0 ? Planters[0] : null;

        public static IReadOnlyList<PlantSO> Plants
        {
            get
            {
                if (plants != null) return plants;
                plants = LoadAll<PlantSO>();
                plants.Sort((a, b) => a.rarity != b.rarity ? a.rarity.CompareTo(b.rarity) : string.CompareOrdinal(a.name, b.name));
                return plants;
            }
        }

        public static PlantSO Plant(string name)
        {
            foreach (PlantSO plant in Plants) if (plant.name == name) return plant;
            return null;
        }

        // Saksının kendi üretim tablosundaki bitkiler (normal oyunda o saksıda çıkabilenler).
        public static void PlantsOf(PlanterSO planter, List<PlantSO> into)
        {
            into.Clear();
            if (planter == null || planter.spawnTable == null) return;
            foreach (PlantSpawnEntry entry in planter.spawnTable)
                if (entry != null && entry.plant != null && !into.Contains(entry.plant)) into.Add(entry.plant);
        }

        public static IReadOnlyList<RunProfileSO> Profiles
        {
            get
            {
                if (profiles != null) return profiles;
                profiles = LoadAll<RunProfileSO>();
                profiles.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                return profiles;
            }
        }

        public static RunProfileSO Profile(string name)
        {
            foreach (RunProfileSO profile in Profiles) if (profile.name == name) return profile;
            return null;
        }

        // Ödül havuzunun bütün ödülleri, havuzun kendi sırasıyla (aşamalı: aşama sırası; düz: liste + kırılma ödülleri). Asset
        // referansları havuzdakilerle aynıdır: eski profil kopyaları ayrı stack oluşturmaz.
        public static void Rewards(BossRewardPoolSO pool, List<BossRewardSO> into)
        {
            into.Clear();
            if (pool == null) return;
            void Add(BossRewardSO reward) { if (reward != null && !into.Contains(reward)) into.Add(reward); }
            if (pool.IsStaged)
                foreach (BossRewardStage stage in pool.stages)
                    foreach (BossRewardStageEntry entry in stage.rewards) Add(entry.reward);
            else
            {
                foreach (BossRewardSO reward in pool.rewards) Add(reward);
                if (pool.breakthroughs != null)
                    foreach (BossRewardSO reward in pool.breakthroughs) Add(reward);
            }
        }

        public static BossRewardSO Reward(BossRewardPoolSO pool, string id)
        {
            var all = new List<BossRewardSO>();
            Rewards(pool, all);
            foreach (BossRewardSO reward in all) if (reward.id == id) return reward;
            return null;
        }

        // Ön koşul zincirinin derinliği (anten → hat → devre): sıralama ve "önce şunu kur" için.
        public static int Depth(BossRewardSO reward)
        {
            int depth = 0;
            for (BossRewardSO r = reward != null ? reward.requires : null; r != null && depth < 8; r = r.requires) depth++;
            return depth;
        }

        public static void Bosses(RunProfileSO profile, List<SegmentEventSO> into)
        {
            into.Clear();
            if (profile == null || profile.bossPool == null) return;
            foreach (BossPoolSO.Entry entry in profile.bossPool.entries)
                if (entry.boss != null && !into.Contains(entry.boss)) into.Add(entry.boss);
        }

        public static SegmentEventSO Boss(RunProfileSO profile, string name)
        {
            var all = new List<SegmentEventSO>();
            Bosses(profile, all);
            foreach (SegmentEventSO boss in all) if (boss.name == name) return boss;
            return null;
        }
    }
}
