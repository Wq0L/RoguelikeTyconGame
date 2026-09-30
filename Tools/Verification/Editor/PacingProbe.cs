using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClickerGame.EconomyAnalysis;
using UnityEditor;
using UnityEngine;

// Tempo taslağı için gerçek ekonomi hesaplayıcısından tek saksı (2x3) geliri: round başına XP ve ağırlıklı kaynak.
public static class PacingProbe
{
    static TileModifierSO Mod(string type, string rarity) =>
        AssetDatabase.LoadAssetAtPath<TileModifierSO>($"Assets/ScriptableObjects/GridModifiers/{type}/{type}-{rarity}.asset");

    static EconomyTile Tile(int x, int y, TileModifierSO so)
    {
        // Aralığın ortası: şanslı/şanssız zar değil, tipik değer.
        var mods = so.modifierRanges.Select(r => new StatModifier { statType = r.statType, target = r.target, operation = r.operation, value = (r.minValue + r.maxValue) * .5f }).ToList();
        return new EconomyTile { cell = new Vector2Int(x, y), tile = so, rolledModifiers = mods };
    }

    static List<EconomyTile> Tiles(string rarity, params string[] types)
    {
        var list = new List<EconomyTile>();
        for (int i = 0; i < types.Length; i++) list.Add(Tile(i % 2, i / 2, Mod(types[i], rarity)));
        return list;
    }

    public static void RunBatch()
    {
        var sb = new StringBuilder();
        try
        {
            var profile = EconomyEditorData.Defaults();
            int spawners = EconomyEditorData.Spawners(profile, out _);
            // Skill ağacının yarısı: hedef round'u küçük olan node'lar önce alınır (oyuncunun tipik sırası).
            var ordered = profile.skillTree.Where(n => n != null).OrderBy(n => n.targetRounds.x + n.targetRounds.y).ToList();
            List<SkillPurchase> Part(double fraction) => ordered.Take((int)Math.Round(ordered.Count * fraction))
                .Select(n => new SkillPurchase { node = n, level = n.tiers.Count }).ToList();
            var mixes = new (string rarity, string[] types)[]
            {
                ("Common", new[] { "Fertile","Water","Crystal","Energy","Damage","Fertile" }),
                ("Rare", new[] { "Fertile","Water","Crystal","Energy","Damage","Fertile" }),
                ("Epic", new[] { "Damage","Damage","Damage","Energy","Energy","Fertile" }),
            };
            sb.AppendLine("fraction,rarity,round,xp,weighted,duration,harvests,damage");
            for (int step = 0; step <= 10; step++)
                foreach (var mix in mixes)
                {
                    var row = new EconomyRoundAssumption { build = EconomyBuild.Custom, purchases = Part(step / 10.0), tiles = Tiles(mix.rarity, mix.types) };
                    for (int r = 1; r <= 130; r++)
                    {
                        var snap = EconomyCalculator.Analyze(EconomyCalculator.Resolve(profile, row, r, spawners, true));
                        var e = snap.Estimate;
                        sb.AppendLine(string.Join(";", step / 10.0, mix.rarity, r, Math.Round(e.Xp), Math.Round(e.Weighted(snap.Input)), snap.Input.Duration, Math.Round(e.Harvests, 1), Math.Round(snap.Input.Damage, 1)));
                    }
                }
            sb.AppendLine("# spawners per 2x3 planter: " + spawners + ", skill nodes: " + ordered.Count);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/PacingProbe2.csv", sb.ToString());
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/PacingProbe.csv", sb + "\nFAIL: " + ex);
            EditorApplication.Exit(1);
        }
    }
}
