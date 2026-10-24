using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MechanicsTestLab
{
    // B · Tarla kurma: grid boyutu, tile, saksı ve bitki ataması. Girdiyi alır, TestField komutlarını çağırır.
    public sealed partial class LabWindow
    {
        private static readonly string[] Rotations = { "0°", "90°", "180°", "270°" };
        private static readonly string[] SizeNames = { "3 × 3", "5 × 5", "7 × 7", "9 × 9", "11 × 11" };

        private int gridSizeIndex = 1;
        private TileModifierType tileType = TileModifierType.Explosive;
        private TileRarity tileRarity = TileRarity.Legendary;
        private int tileLevel;
        private float tileRoll = 0.5f;
        private int planterIndex, rotationIndex, plantIndex;
        private bool allowOffTable;
        private int plantHealthValue = 1;
        private readonly List<GridPosition> areaCells = new();
        private readonly List<PlantSO> plantChoices = new();

        private void DrawField()
        {
            Header("Açık alan");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Şu an {TestField.GridSize} × {TestField.GridSize}", GUILayout.Width(110f));
            gridSizeIndex = EditorGUILayout.Popup(gridSizeIndex, SizeNames);
            if (GUILayout.Button("Uygula", GUILayout.Width(70f))) Say(TestField.SetGridSize(Lab.GridSizes[gridSizeIndex]));
            EditorGUILayout.EndHorizontal();

            DrawGrid();
            if (!hasSelection) { EditorGUILayout.HelpBox("Tile, saksı ve bitki işlemleri için gridden hücre ya da alan seç.", MessageType.None); }

            Header("Tile modifier");
            tileType = (TileModifierType)EditorGUILayout.EnumPopup("Tür", tileType);
            tileRarity = (TileRarity)EditorGUILayout.EnumPopup("Nadirlik", tileRarity);
            tileLevel = EditorGUILayout.IntSlider("Seviye", tileLevel, 0, GroundCell.MaxLevel);
            tileRoll = EditorGUILayout.Slider("Zar konumu (0 en düşük · 1 en yüksek)", tileRoll, 0f, 1f);
            TileModifierSO tile = LabCatalog.Tile(tileType, tileRarity);
            if (tile != null)
            {
                text.Clear();
                foreach (StatModifierRange range in tile.modifierRanges)
                    text.Append(range.statType).Append(' ').Append(range.operation).Append(' ').Append(Mathf.Lerp(range.minValue, range.maxValue, tileRoll).ToString("0.####"))
                        .Append("  (aralık ").Append(range.minValue.ToString("0.####")).Append(" … ").Append(range.maxValue.ToString("0.####")).Append(")  ");
                EditorGUILayout.LabelField(tile.name + " · " + text, EditorStyles.wordWrappedMiniLabel);
            }
            else EditorGUILayout.LabelField("Bu tür / nadirlik için tile asset'i yok.", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!hasSelection || tile == null))
                if (GUILayout.Button("Seçili alana uygula / değiştir")) ForArea(cell => TestField.ApplyTile(cell, tile, tileRoll, tileLevel), "tile");
            using (new EditorGUI.DisabledScope(!hasSelection))
                if (GUILayout.Button("Seçili alanın tile'ını temizle")) ForArea(TestField.ClearTile, "tile temizliği");
            EditorGUILayout.EndHorizontal();

            Header("Saksı");
            IReadOnlyList<PlanterSO> planters = LabCatalog.Planters;
            if (planters.Count == 0) { EditorGUILayout.LabelField("Saksı asset'i bulunamadı."); return; }
            var planterNames = new string[planters.Count];
            for (int i = 0; i < planters.Count; i++) planterNames[i] = $"{planters[i].name} ({planters[i].sizeX}×{planters[i].sizeZ})";
            planterIndex = Mathf.Clamp(EditorGUILayout.Popup("Tür", planterIndex, planterNames), 0, planters.Count - 1);
            rotationIndex = EditorGUILayout.Popup("Yön", rotationIndex, Rotations);
            PlanterSO planter = planters[planterIndex];
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!hasSelection))
            {
                if (GUILayout.Button("Köşe hücresine kur")) Say(TestField.PlacePlanter(planter, anchor, rotationIndex * 90, out _));
                if (GUILayout.Button("Alanı bu saksıyla doldur")) ForArea(cell => TestField.PlanterAt(cell) != null ? Result.Fail("dolu") : TestField.PlacePlanter(planter, cell, rotationIndex * 90, out _), "saksı", true);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!hasSelection))
            {
                if (GUILayout.Button("Seçili hücredeki saksıyı değiştir")) Say(TestField.ReplacePlanter(anchor, planter, rotationIndex * 90));
                if (GUILayout.Button("Seçili alandaki saksıları kaldır"))
                    ForArea(cell => TestField.PlanterAt(cell) != null ? TestField.RemovePlanter(cell) : Result.Fail("saksı yok"), "saksı kaldırma", true);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Yerleşim oyunun doğrulamasından geçer: tarla dışına, kilitli hücreye ve dolu hücreye saksı kurulmaz. Bedel ve satın alma kilidi testte aranmaz. " +
                                       "Köşe = seçimin ilk tıklanan hücresi; dönen saksı o hücreden yöne göre uzanır.", EditorStyles.wordWrappedMiniLabel);

            Header("Bitki");
            PlanterBrain selectedPlanter = hasSelection ? TestField.PlanterAt(anchor) : null;
            plantChoices.Clear();
            if (allowOffTable) plantChoices.AddRange(LabCatalog.Plants);
            else LabCatalog.PlantsOf(selectedPlanter != null ? selectedPlanter.EditorData : planter, plantChoices);
            var plantNames = new string[plantChoices.Count + 1];
            plantNames[0] = "Saksının normal bitki zarı";
            for (int i = 0; i < plantChoices.Count; i++) plantNames[i + 1] = $"{plantChoices[i].name} [{plantChoices[i].rarity}]";
            plantIndex = Mathf.Clamp(EditorGUILayout.Popup("Bitki", plantIndex, plantNames), 0, plantChoices.Count);
            allowOffTable = EditorGUILayout.ToggleLeft("Saksının üretim tablosunda olmayan bitkilere izin ver (normal oyunda oluşmaz)", allowOffTable);
            PlantSO plantChoice = plantIndex == 0 ? null : plantChoices[plantIndex - 1];
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!hasSelection))
            {
                if (GUILayout.Button("Seçili alana bitki koy")) ForArea(cell => TestField.SpawnerAt(cell) == null ? Result.Fail("yuva yok") : TestField.SetPlant(cell, plantChoice, allowOffTable), "bitki", true);
                if (GUILayout.Button("Seçili alanı boşalt")) ForArea(cell => TestField.SpawnerAt(cell) == null ? Result.Fail("yuva yok") : TestField.ClearPlant(cell), "bitki kaldırma", true);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            plantHealthValue = Mathf.Max(1, EditorGUILayout.IntField("Mevcut can", plantHealthValue));
            using (new EditorGUI.DisabledScope(!hasSelection))
                if (GUILayout.Button("Seçili alana uygula", GUILayout.Width(150f))) ForArea(cell => TestField.PlantAt(cell) == null ? Result.Fail("bitki yok") : TestField.SetPlantHealth(cell, plantHealthValue), "can", true);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Can 1 ile bitkinin azami canı arasında tutulur; azami can oyunun can eğrisinden gelir ve değiştirilmez.", EditorStyles.wordWrappedMiniLabel);

            Header("Toplu işlemler");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Bütün bitkileri yenile (yerleşim korunur)")) Say(Result.Success($"{TestField.FillPlants(null)} üretim noktası normal bitki zarıyla dolduruldu."));
            if (GUILayout.Button("Bütün bitkileri kaldır")) Say(Result.Success($"{TestField.ClearPlants()} bitki kaldırıldı (hasat sayılmadı)."));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Senaryonun bitki kurulumunu yeniden uygula"))
            {
                var report = new List<Result>();
                ScenarioRunner.ApplyPlants(TestSession.Current, report);
                Say(report.Count == 0 ? Result.Success("Senaryonun bitki kurulumu yeniden uygulandı.") : report[0]);
            }
            if (GUILayout.Button("Tüm tarlayı temizle (saksı + tile)") && EditorUtility.DisplayDialog("Tarlayı temizle", "Bütün saksılar ve tile'lar kaldırılsın mı? (Ödüller ve statlar kalır.)", "Temizle", "Vazgeç"))
                Say(TestField.ClearField());
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Burada 'kaldır' gerçek nesneyi kaldırır; görselleri gizlemek için 'A · Oturum' sekmesindeki görsel kontrollere bak.", EditorStyles.wordWrappedMiniLabel);
        }

        // Seçili alandaki her açık hücreye komutu uygular ve özetler. quiet: beklenen atlamalar (dolu / boş hücre) hata sayılmaz.
        private void ForArea(System.Func<GridPosition, Result> command, string what, bool quiet = false)
        {
            TestField.CellsIn(Selection, areaCells);
            if (areaCells.Count == 0) { Say(Result.Fail("Seçili alanda açık hücre yok.")); return; }
            int done = 0;
            string firstFailure = null;
            foreach (GridPosition cell in areaCells)
            {
                Result result = command(cell);
                if (result.Ok) done++;
                else firstFailure ??= $"({cell.x}, {cell.z}): {result.Message}";
            }
            int skipped = areaCells.Count - done;
            if (skipped == 0) Say(Result.Success($"{what}: {done} hücre."));
            else if (done > 0 && quiet) Say(Result.Success($"{what}: {done} hücre · {skipped} hücre atlandı."));
            else Say(Result.Fail($"{what}: {done} hücre · {skipped} hücrede uygulanmadı — {firstFailure}"));
        }
    }
}
