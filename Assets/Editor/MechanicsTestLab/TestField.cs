using System.Collections.Generic;
using UnityEngine;

namespace MechanicsTestLab
{
    // Tarla kurma komutları. Hepsi oyunun kendi yollarını çağırır: tile → GroundCell.ApplyModifier / AddLevels, saksı →
    // PlacementManager (aynı yerleşim doğrulaması ve bağlama), bitki → PlantSpawner'ın normal doğuş yolu. Saksı–hücre bağı, tile
    // toplamları ve rezonanslar oyunun kendi yeniden hesabından (PlanterBrain.RefreshTileBuffs) çıkar; burada hesaplanmaz.
    // Serbest atama yalnız bu test aracına aittir: normal oyundaki rastgele tile yerleşimi (ProgressionManager) değişmez.
    public static class TestField
    {
        public static GridSystem Grid => FieldCells.Grid;

        public static GridObject Cell(GridPosition position) => Grid?.GetGridObject(position);
        public static GroundCell Ground(GridPosition position) => Cell(position)?.GetGroundCellCached();
        public static PlanterBrain PlanterAt(GridPosition position) => Cell(position)?.GetPlanterBrain();

        public static PlantSpawner SpawnerAt(GridPosition position)
        {
            GridObject cell = Cell(position);
            PlanterBrain brain = cell?.GetPlanterBrain();
            if (brain == null) return null;
            foreach (PlantSpawner spawner in brain.EditorSpawners)
                if (spawner != null && spawner.GridObject == cell) return spawner;
            return null;
        }

        public static PlantHealth PlantAt(GridPosition position)
        {
            GameObject plant = Cell(position)?.GetPlantObject();
            return plant != null && plant.TryGetComponent(out PlantHealth health) && !health.IsDead ? health : null;
        }

        public static void Planters(List<PlanterBrain> into)
        {
            into.Clear();
            GridManager manager = GridManager.Instance;
            if (manager == null) return;
            for (int x = 0; x < manager.GetWidth(); x++)
                for (int z = 0; z < manager.GetHeight(); z++)
                {
                    PlanterBrain brain = PlanterAt(new GridPosition(x, z));
                    if (brain != null && brain.IsPlaced && !into.Contains(brain)) into.Add(brain);
                }
        }

        // ---------------------------------------------------------------- grid
        // Açık alanın kenarı (hücre). Alan yoksa 0.
        public static int GridSize => FieldCells.TryBounds(out FieldBounds bounds) ? bounds.Width : 0;

        public static Result SetGridSize(int size)
        {
            if (System.Array.IndexOf(Lab.GridSizes, size) < 0) return Result.Fail($"Grid boyutu {size} desteklenmiyor (3, 5, 7, 9, 11).");
            GridUnlockManager unlock = GridUnlockManager.Instance;
            if (unlock == null) return Result.Fail("GridUnlockManager yok.");
            int current = GridSize;
            if (size == current) return Result.Success($"Grid zaten {size}×{size}.");
            if (size < current)
                return Result.Fail($"Açık alan canlı küçültülemez ({current}×{current} → {size}×{size}): oyunda hücre kilitleme yolu yok. " +
                                   "Boyutu senaryoya yazıp 'Senaryoyu yeniden kur' ile temiz oturumda uygula.");
            unlock.UnlockNextTier(size);
            return GridSize == size ? Result.Success($"Grid {size}×{size} açıldı.") : Result.Fail($"Grid {size}×{size} açılamadı (şu an {GridSize}).");
        }

        // ---------------------------------------------------------------- tile
        // Tile'ın stat değerleri: aralıktaki yeri (roll) ya da kesin değerler. Seviye, oyunun yükseltme yoluyla eklenir.
        public static Result ApplyTile(GridPosition position, TileModifierSO tile, float roll, int level, IReadOnlyList<float> values = null)
        {
            GroundCell ground = Ground(position);
            if (ground == null) return Result.Fail($"({position.x}, {position.z}) tarlanın dışında.");
            if (ground.IsLocked) return Result.Fail($"({position.x}, {position.z}) kilitli: önce grid boyutunu büyüt.");
            if (tile == null) return ClearTile(position);
            var offered = new List<StatModifier>();
            for (int i = 0; i < tile.modifierRanges.Count; i++)
            {
                StatModifierRange range = tile.modifierRanges[i];
                float value = values != null && i < values.Count ? values[i] : Mathf.Lerp(range.minValue, range.maxValue, Mathf.Clamp01(roll));
                offered.Add(new StatModifier { statType = range.statType, target = range.target, operation = range.operation, value = value });
            }
            ground.ApplyModifier(tile, offered);
            level = Mathf.Clamp(level, 0, GroundCell.MaxLevel);
            if (level > 0) ground.AddLevels(level);
            return Result.Success($"({position.x}, {position.z}) ← {tile.name} · Sv {ground.Level}");
        }

        public static Result ClearTile(GridPosition position)
        {
            GroundCell ground = Ground(position);
            if (ground == null) return Result.Fail($"({position.x}, {position.z}) tarlanın dışında.");
            if (ground.CurrentModifier != null) ground.ApplyModifier(null);
            return Result.Success($"({position.x}, {position.z}) tile temizlendi.");
        }

        // ---------------------------------------------------------------- saksı
        public static Result PlacePlanter(PlanterSO planter, GridPosition origin, int rotation, out PlanterBrain placed)
        {
            placed = null;
            if (PlacementManager.Instance == null) return Result.Fail("PlacementManager yok.");
            placed = PlacementManager.Instance.EditorPlace(planter, origin, rotation, out string error);
            return placed != null
                ? Result.Success($"{planter.name} → ({origin.x}, {origin.z}) · {placed.EditorRotation}°")
                : Result.Fail("Saksı kurulamadı: " + error);
        }

        public static Result RemovePlanter(GridPosition position)
        {
            PlanterBrain brain = PlanterAt(position);
            if (brain == null) return Result.Fail($"({position.x}, {position.z}) hücresinde saksı yok.");
            string name = brain.EditorData != null ? brain.EditorData.name : brain.name;
            brain.EditorRemove();
            return Result.Success($"{name} kaldırıldı (iade ve hasat yok).");
        }

        // Saksıyı aynı konum ve yönde başka bir türle değiştirir. Yenisi sığmazsa eskisi geri kurulur.
        public static Result ReplacePlanter(GridPosition position, PlanterSO planter, int rotation)
        {
            PlanterBrain old = PlanterAt(position);
            if (old == null) return PlacePlanter(planter, position, rotation, out _);
            PlanterSO oldData = old.EditorData;
            GridPosition origin = old.EditorOrigin;
            int oldRotation = old.EditorRotation;
            old.EditorRemove();
            Result result = PlacePlanter(planter, origin, rotation, out _);
            if (result.Ok) return result;
            Result restored = PlacePlanter(oldData, origin, oldRotation, out _);
            return Result.Fail(result.Message + (restored.Ok ? " Eski saksı geri kuruldu." : " Eski saksı da geri kurulamadı!"));
        }

        // ---------------------------------------------------------------- bitki
        // plant null: saksının normal bitki zarı. allowOffTable false: yalnız saksının kendi üretim tablosundaki bitkiler.
        public static Result SetPlant(GridPosition position, PlantSO plant, bool allowOffTable = false)
        {
            PlantSpawner spawner = SpawnerAt(position);
            if (spawner == null) return Result.Fail($"({position.x}, {position.z}) bir üretim noktası değil (saksı yok ya da bu hücrede bitki yuvası yok).");
            if (plant != null && !allowOffTable)
            {
                var table = new List<PlantSO>();
                LabCatalog.PlantsOf(spawner.EditorData, table);
                if (!table.Contains(plant))
                    return Result.Fail($"{plant.name} bu saksının üretim tablosunda yok (normal oyunda burada çıkmaz). Tablo dışı bitki için ayrıca izin ver.");
            }
            if (!spawner.EditorRespawn(plant)) return Result.Fail($"({position.x}, {position.z}) noktasına bitki çıkarılamadı (üretim tablosu boş olabilir).");
            PlantHealth health = PlantAt(position);
            return Result.Success($"({position.x}, {position.z}) ← {(health != null && health.Data != null ? health.Data.name : "bitki")}");
        }

        public static Result ClearPlant(GridPosition position)
        {
            PlantSpawner spawner = SpawnerAt(position);
            if (spawner == null) return Result.Fail($"({position.x}, {position.z}) bir üretim noktası değil.");
            spawner.EditorClearPlant();
            return Result.Success($"({position.x}, {position.z}) bitkisi kaldırıldı (hasat sayılmadı).");
        }

        public static Result SetPlantHealth(GridPosition position, int health)
        {
            PlantHealth plant = PlantAt(position);
            if (plant == null) return Result.Fail($"({position.x}, {position.z}) hücresinde canlı bitki yok.");
            plant.EditorSetHealth(health);
            return Result.Success($"({position.x}, {position.z}) can {plant.CurrentHealth}/{plant.MaxHealth}" +
                                  (health != plant.CurrentHealth ? " (geçerli aralığa çekildi)" : ""));
        }

        // Bütün üretim noktaları: plant null → normal zar. Yerleşim (saksı, tile) değişmez. Dönen: dolan nokta sayısı.
        public static int FillPlants(PlantSO plant, int health = 0, bool allowOffTable = false)
        {
            var planters = new List<PlanterBrain>();
            Planters(planters);
            var table = new List<PlantSO>();
            int filled = 0;
            foreach (PlanterBrain brain in planters)
            {
                LabCatalog.PlantsOf(brain.EditorData, table);
                PlantSO chosen = plant != null && (allowOffTable || table.Contains(plant)) ? plant : null;
                foreach (PlantSpawner spawner in brain.EditorSpawners)
                {
                    if (spawner == null || !spawner.EditorRespawn(chosen)) continue;
                    filled++;
                    if (health > 0 && spawner.SpawnedPlant != null && spawner.SpawnedPlant.TryGetComponent(out PlantHealth hp)) hp.EditorSetHealth(health);
                }
            }
            return filled;
        }

        public static int ClearPlants()
        {
            var planters = new List<PlanterBrain>();
            Planters(planters);
            int cleared = 0;
            foreach (PlanterBrain brain in planters)
                foreach (PlantSpawner spawner in brain.EditorSpawners)
                {
                    if (spawner == null) continue;
                    if (spawner.SpawnedPlant != null) cleared++;
                    spawner.EditorClearPlant();
                }
            return cleared;
        }

        // ---------------------------------------------------------------- toplu
        // Bütün saksılar ve tile'lar kalkar. Açık alan, ödüller ve stat'lar değişmez.
        public static Result ClearField()
        {
            var planters = new List<PlanterBrain>();
            Planters(planters);
            foreach (PlanterBrain brain in planters) brain.EditorRemove();
            int tiles = 0;
            GridManager manager = GridManager.Instance;
            if (manager != null)
                for (int x = 0; x < manager.GetWidth(); x++)
                    for (int z = 0; z < manager.GetHeight(); z++)
                    {
                        GroundCell ground = Ground(new GridPosition(x, z));
                        if (ground == null || ground.CurrentModifier == null) continue;
                        ground.ApplyModifier(null);
                        tiles++;
                    }
            return Result.Success($"Tarla temizlendi: {planters.Count} saksı, {tiles} tile.");
        }

        // Dikdörtgen alandaki bütün açık hücreler (seçim sırası: x, sonra z).
        public static void CellsIn(RectInt area, List<GridPosition> into)
        {
            into.Clear();
            for (int x = area.xMin; x < area.xMax; x++)
                for (int z = area.yMin; z < area.yMax; z++)
                {
                    GroundCell ground = Ground(new GridPosition(x, z));
                    if (ground != null && !ground.IsLocked) into.Add(new GridPosition(x, z));
                }
        }
    }
}
