using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MechanicsTestLab
{
    // Tekrar kurulabilir test düzeni. Oyuncu kaydı DEĞİLDİR: yalnız kurulum verisi taşır (profil adı, seed, grid, tile, saksı,
    // bitki, test stat katkıları, ödüller, round, başlangıç koşulları). Canlı nesne referansı, süren davranış, yarım zincir ya da
    // sayaç saklanmaz; içerik adla / kimlikle anılır ve kurulurken oyunun gerçek asset'lerinden çözülür.
    [Serializable]
    public sealed class Scenario
    {
        public int version = 1;
        public string name = "Adsız senaryo";
        // "Ne yapacağım, ne görmeliyim, hangi sonucu kontrol edeceğim?"
        public string whatToDo = "";
        public string whatToSee = "";
        public string whatToCheck = "";

        public string profile = Lab.DefaultProfile;
        // Boss, ödül teklifi ve tarla olayı seçimlerinin seed'i (profilin çalışma zamanı kopyasına yazılır). 0 verilirse 1 kullanılır.
        public int seed = 1;
        public int round = 1;
        public int gridSize = 5;
        // Boş: kataloğun varsayılan çiftçisi / tırpanı (gerçek kayıttaki seçim kullanılmaz).
        public string farmer = "";
        public string scythe = "";
        // −1: profilin başlangıç değeri.
        public int gold = -1, iron = -1, stone = -1;
        // 0: dokunma (level 1).
        public int level;

        public bool freezeRoundTimer = true;
        public bool pauseProduction;
        public bool autoAttack = true;
        // Sabit nişan: fare yerine bu hücrenin merkezi (aimFixed false: normal fare kontrolü).
        public bool aimFixed;
        public int aimX, aimZ;

        public List<ScenarioTile> tiles = new();
        public List<ScenarioPlanter> planters = new();
        // Kurulumdan sonra üretim noktalarının durumu: "roll" (saksının normal bitki zarı), "empty" (boş) ya da bir bitki adı.
        public string plantFill = "roll";
        // 0: bitkinin normal canı. >0: kurulan bütün bitkilerin mevcut canı (azami canla sınırlı).
        public int plantHealth;
        public List<ScenarioPlant> plants = new();
        public List<ScenarioStat> stats = new();
        public List<ScenarioReward> rewards = new();
        // Boş: oyunun normal akışı (round bir boss round'uysa boss havuzdan, seed'e göre seçilir ve aktif olur).
        // "none": round'un boss etkisi kurulumdan sonra temizlenir. Başka bir değer: o boss bu round'a atanır (boss round'u olmalı).
        public string boss = "";
        public const string NoBoss = "none";

        public Scenario Clone() => JsonUtility.FromJson<Scenario>(JsonUtility.ToJson(this));
    }

    [Serializable]
    public sealed class ScenarioTile
    {
        public int x, z;
        public TileModifierType type;
        public TileRarity rarity;
        [Range(0, GroundCell.MaxLevel)] public int level;
        // Tile'ın stat aralığındaki yeri: 0 en düşük, 1 en yüksek zar. values doluysa kullanılmaz.
        [Range(0f, 1f)] public float roll = 0.5f;
        // Yakalanan düzenlerde: tile'ın seviye 0'daki kesin değerleri (stat aralığı sırasıyla).
        public List<float> values = new();
    }

    [Serializable]
    public sealed class ScenarioPlanter
    {
        public string planter;
        public int x, z;
        public int rotation;
    }

    [Serializable]
    public sealed class ScenarioPlant
    {
        public int x, z;
        public string plant;
        // 0: normal can.
        public int health;
    }

    [Serializable]
    public sealed class ScenarioStat
    {
        public StatType stat;
        public StatTarget target;
        public ModifierOperation operation;
        public float value;

        public StatModifier ToModifier() => new StatModifier { statType = stat, target = target, operation = operation, value = value };
        public static ScenarioStat From(StatModifier m) => new ScenarioStat { stat = m.statType, target = m.target, operation = m.operation, value = m.value };
    }

    [Serializable]
    public sealed class ScenarioReward
    {
        public string id;
        public int stacks = 1;
        // Yerleşim isteyen ödülün konum kimliği (anten). −1: ilk geçerli konum.
        public int placement = RunEffectSO.NoPlacement;
    }

    // Senaryo dosyaları: proje kökünde, Assets dışında (Unity asset'i değildir; kaynak oyun asset'lerine yazılmaz).
    public static class ScenarioFiles
    {
        public static string Directory => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "MechanicsTestLab", "Scenarios"));

        public static string PathFor(string name)
        {
            var safe = new System.Text.StringBuilder();
            foreach (char c in name ?? "senaryo") safe.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return Path.Combine(Directory, (safe.Length > 0 ? safe.ToString() : "senaryo") + ".json");
        }

        public static Result Save(Scenario scenario, string path)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(scenario, true));
                return Result.Success("Senaryo kaydedildi: " + path);
            }
            catch (Exception e) { return Result.Fail("Senaryo kaydedilemedi: " + e.Message); }
        }

        public static Result Load(string path, out Scenario scenario)
        {
            scenario = null;
            try
            {
                scenario = JsonUtility.FromJson<Scenario>(File.ReadAllText(path));
                if (scenario == null) return Result.Fail("Dosya bir senaryo değil: " + path);
                scenario.tiles ??= new(); scenario.planters ??= new(); scenario.plants ??= new();
                scenario.stats ??= new(); scenario.rewards ??= new();
                return Result.Success("Senaryo okundu: " + scenario.name);
            }
            catch (Exception e) { return Result.Fail("Senaryo okunamadı: " + e.Message); }
        }

        public static List<string> List()
        {
            var result = new List<string>();
            if (System.IO.Directory.Exists(Directory)) result.AddRange(System.IO.Directory.GetFiles(Directory, "*.json"));
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }
    }
}
