using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MechanicsTestLab
{
    // Mechanics Test Lab ana paneli (Tools > Mechanics Test Lab > Paneli aç). Panel yalnız kullanıcı girdisini alır ve durumu
    // gösterir: müdahaleler test komutlarında (TestField / TestStats / TestRewards / TestEvents / TestAttack), okuma
    // TestInspector'da, kayıt TestLog'da, tekrar kurulabilir düzen Scenario'dadır. Sekmeler ayrı dosyalardadır (LabWindow.*.cs).
    // Sayısal doğrulama (önce / sonra, saldırı izi, makbuz, kontroller, şans, rapor) 'H · Sayısal' sekmesindedir.
    // Komutlar yalnız MechanicsTestScene'den başlatılan oturumda çalışır; başka sahnedeki Play'e hiçbir komut gönderilmez.
    public sealed partial class LabWindow : EditorWindow
    {
        private static readonly string[] Tabs = { "A · Oturum", "B · Tarla", "C · Statlar", "D · Ödüller", "E · Boss ve olaylar", "F · İnceleme", "G · Senaryolar", "H · Sayısal" };

        private int tab;
        private Vector2 scroll;
        private string message = "";
        private bool messageOk = true;
        private int knownGeneration = -1;
        private readonly StringBuilder text = new();

        // Seçim: tek hücre ya da dikdörtgen alan (grid koordinatı). anchor: ilk tıklanan hücre.
        private bool hasSelection;
        private GridPosition anchor, corner;

        private GUIStyle cellStyle, monoStyle, wrapStyle;

        [MenuItem(Lab.Menu + "/Paneli aç", priority = 100)]
        public static void Open() => GetWindow<LabWindow>("Mechanics Test Lab");

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGui;
            minSize = new Vector2(620f, 480f);
        }

        private void OnDisable() => SceneView.duringSceneGui -= OnSceneGui;

        private void OnInspectorUpdate() { if (EditorApplication.isPlaying) Repaint(); }

        private void Styles()
        {
            if (cellStyle != null) return;
            cellStyle = new GUIStyle(GUI.skin.button) { fontSize = 9, wordWrap = false, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(1, 1, 1, 1), richText = false };
            monoStyle = new GUIStyle(EditorStyles.label) { font = EditorStyles.miniFont, fontSize = 11, wordWrap = false, richText = false };
            wrapStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = false };
        }

        private void Say(Result result)
        {
            message = result.Message;
            messageOk = result.Ok;
            TestLog.Note(TestLog.Kind.Tool, (result.Ok ? "Komut: " : "Komut reddedildi: ") + result.Message);
            Repaint();
        }

        private RectInt Selection
        {
            get
            {
                int minX = Mathf.Min(anchor.x, corner.x), minZ = Mathf.Min(anchor.z, corner.z);
                return new RectInt(minX, minZ, Mathf.Abs(anchor.x - corner.x) + 1, Mathf.Abs(anchor.z - corner.z) + 1);
            }
        }

        private void OnGUI()
        {
            Styles();
            if (TestSession.Generation != knownGeneration)
            {
                knownGeneration = TestSession.Generation;
                OnSessionRebuilt();
            }
            DrawHeader();
            // Dar pencerede sekiz sekme tek satıra sığmaz (adlar kırpılır): iki satıra bölünür.
            tab = position.width < 900f ? GUILayout.SelectionGrid(tab, Tabs, 4) : GUILayout.Toolbar(tab, Tabs);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            // Senaryolar sekmesi Play dışında da kullanılır (hangi senaryoyla başlanacağı); diğerleri canlı oturum ister.
            if (tab == 6) DrawScenarios();
            else if (tab == 0) DrawSession();
            else if (!TestSession.Ready) EditorGUILayout.HelpBox("Bu sekme canlı test oturumu ister. 'A · Oturum' sekmesinden test oturumunu başlat.", MessageType.Info);
            else
                switch (tab)
                {
                    case 1: DrawField(); break;
                    case 2: DrawStats(); break;
                    case 3: DrawRewards(); break;
                    case 4: DrawEvents(); break;
                    case 5: DrawInspect(); break;
                    case 7: DrawNumeric(); break;
                }
            EditorGUILayout.EndScrollView();
        }

        // Yeni kurulum: canlı nesnelere bağlı seçimler ve listeler tazelenir.
        private void OnSessionRebuilt()
        {
            rewardList.Clear();
            bossList.Clear();
            logView.Clear();
            selectedReward = null;
            resourcesRead = false;
            OnNumericRebuilt();
        }

        private void DrawHeader()
        {
            if (!EditorApplication.isPlaying)
                EditorGUILayout.HelpBox("Test oturumu açık değil. 'A · Oturum' sekmesinden başlat ya da MechanicsTestScene'i açıp Play'e bas. " +
                                        "Bu panel gerçek kayda, seçili profile ve kaynak asset'lere yazmaz.", MessageType.Info);
            else if (!TestSession.Active)
                EditorGUILayout.HelpBox("Play şu an MechanicsTestScene'den BAŞLAMADI (GameScene / DemoScene / menü). Bu panel yalnız test oturumunda komut " +
                                        "gönderir; açık olan gerçek run'a hiçbir şey yapılmaz. Tam oyunda çekim için: Tools > Geç Oyun Testi.", MessageType.Warning);
            else
                EditorGUILayout.HelpBox((TestSession.Ready ? "TEST OTURUMU · " : "Kuruluyor · ") + TestSession.Status +
                                        (TestSession.SourceProfile != null ? $"\nProfil: {TestSession.SourceProfile.name} (çalışma zamanı kopyası) · seed {TestSession.Profile.bossSeed} · kayıt: yalnız bellekte" : ""),
                    TestSession.Ready ? MessageType.None : MessageType.Info);
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, messageOk ? MessageType.None : MessageType.Warning);
        }

        private static void Header(string title)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        // ---------------------------------------------------------------- grid görünümü (tarla ve inceleme sekmeleri)
        private static readonly string[] TileShort = { "Fe", "Wa", "Cr", "En", "Ex", "Du", "Dm", "To", "Bo", "El" };

        private void DrawGrid()
        {
            GridManager manager = GridManager.Instance;
            if (manager == null) { EditorGUILayout.LabelField("Grid yok."); return; }
            int width = manager.GetWidth(), height = manager.GetHeight();
            const float size = 40f;
            RectInt selection = Selection;
            Color normal = GUI.backgroundColor;
            EditorGUILayout.LabelField("Sol tık: hücre seç · Shift + tık: dikdörtgen alan. Satırlar z (üstte büyük), sütunlar x.", EditorStyles.miniLabel);
            for (int z = height - 1; z >= 0; z--)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(z.ToString(), EditorStyles.miniLabel, GUILayout.Width(18f), GUILayout.Height(size));
                for (int x = 0; x < width; x++)
                {
                    var position = new GridPosition(x, z);
                    GroundCell ground = TestField.Ground(position);
                    bool inSelection = hasSelection && x >= selection.xMin && x < selection.xMax && z >= selection.yMin && z < selection.yMax;
                    string label = "";
                    Color color = new Color(0.25f, 0.25f, 0.25f);
                    if (ground == null) label = "";
                    else if (ground.IsLocked) label = "·";
                    else
                    {
                        color = new Color(0.72f, 0.72f, 0.72f);
                        TileModifierSO tile = ground.CurrentModifier;
                        if (tile != null)
                        {
                            color = Color.Lerp(tile.tileColor, Color.white, 0.35f);
                            label = TileShort[(int)tile.modifierType] + ground.Level;
                        }
                        PlanterBrain brain = TestField.PlanterAt(position);
                        if (brain != null)
                        {
                            PlantHealth plant = TestField.PlantAt(position);
                            label += plant != null ? $"\n▣ {plant.CurrentHealth}" : TestField.SpawnerAt(position) != null ? "\n▣ –" : "\n▣";
                        }
                    }
                    if (inSelection) color = Color.Lerp(color, new Color(1f, 0.85f, 0.2f), 0.75f);
                    GUI.backgroundColor = color;
                    using (new EditorGUI.DisabledScope(ground == null))
                        if (GUILayout.Button(label, cellStyle, GUILayout.Width(size), GUILayout.Height(size)))
                        {
                            if (Event.current.shift && hasSelection) corner = position;
                            else { anchor = corner = position; hasSelection = true; }
                            SceneView.RepaintAll();
                        }
                }
                GUI.backgroundColor = normal;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(22f);
            for (int x = 0; x < width; x++) GUILayout.Label(x.ToString(), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(size));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Hücre yazısı: tile kısaltması + seviye (Fe Fertile, Wa Water, Cr Crystal, En Energy, Ex Explosive, Du Duplicate, Dm Damage, " +
                                       "To Tornado, Bo Boomerang, El Electric) · ▣ saksı + bitkinin canı (– boş yuva) · '·' kilitli.", EditorStyles.wordWrappedMiniLabel);
            if (hasSelection)
                EditorGUILayout.LabelField(selection.width * selection.height == 1
                    ? $"Seçili hücre: ({anchor.x}, {anchor.z})"
                    : $"Seçili alan: ({selection.xMin}, {selection.yMin}) – ({selection.xMax - 1}, {selection.yMax - 1}) · {selection.width}×{selection.height} · köşe (ilk tık): ({anchor.x}, {anchor.z})");
            else EditorGUILayout.LabelField("Seçim yok.");
        }

        // ---------------------------------------------------------------- Scene görünümü işaretleri (yalnız gösterim)
        private void OnSceneGui(SceneView view)
        {
            if (!TestSession.Ready || GridManager.Instance == null) return;
            float cell = GridManager.Instance.GetCellSize();
            if (hasSelection)
            {
                RectInt selection = Selection;
                Vector3 min = TestAttack.WorldOf(new GridPosition(selection.xMin, selection.yMin)), max = TestAttack.WorldOf(new GridPosition(selection.xMax - 1, selection.yMax - 1));
                Vector3 center = (min + max) * 0.5f + Vector3.up * 0.1f;
                Handles.color = new Color(1f, 0.85f, 0.2f, 1f);
                Handles.DrawWireCube(center, new Vector3(Mathf.Abs(max.x - min.x) + cell, 0.2f, Mathf.Abs(max.z - min.z) + cell));
                PlanterBrain brain = TestField.PlanterAt(anchor);
                if (brain != null)
                {
                    Handles.color = new Color(0.3f, 1f, 0.5f, 1f);
                    foreach (GridObject grid in brain.OccupiedGrids)
                        Handles.DrawWireCube(TestAttack.WorldOf(grid.GetGridPosition()) + Vector3.up * 0.15f, new Vector3(cell * 0.9f, 0.3f, cell * 0.9f));
                }
            }
            if (TestAttack.Aim.HasValue)
            {
                Handles.color = Color.cyan;
                Handles.DrawWireDisc(TestAttack.Aim.Value + Vector3.up * 0.12f, Vector3.up, 0.35f);
            }
            if (!TestVisuals.TargetOverlay || TestAttack.Player == null) return;
            // Test göstergesi: sıradaki saldırının gerçek temas dairesi ve değdiği hücreler (oyunun hedefleme sorgusuyla).
            Vector3 aim = TestAttack.Player.CursorWorldPosition;
            float radius = TestAttack.Player.CursorRadius;
            Handles.color = new Color(1f, 0.3f, 0.3f, 1f);
            Handles.DrawWireDisc(aim + Vector3.up * 0.2f, Vector3.up, radius);
            TestAttack.TargetCells(aim, radius, overlayCells);
            foreach (GridObject grid in overlayCells)
                Handles.DrawWireCube(TestAttack.WorldOf(grid.GetGridPosition()) + Vector3.up * 0.2f, new Vector3(cell * 0.8f, 0.05f, cell * 0.8f));
        }

        private readonly List<GridObject> overlayCells = new();
    }
}
