using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MechanicsTestLab
{
    // H · Sayısal doğrulama: önce / sonra karşılaştırması, saldırı izi, ödül makbuzları ve geçici etkiler, beklenen / gerçekleşen
    // kontroller, şans istatistikleri ve rapor. Panel yalnız gösterir ve komut verir: ölçüm Snapshots / TestTrace / BuffWatch'ta,
    // kontroller NumericChecks'te, örnekleme ChanceSampler'da, dışa aktarım LabReport'tadır. Uzun içerik alt sekmelere ve
    // açılır bölümlere ayrılmıştır.
    public sealed partial class LabWindow
    {
        private static readonly string[] NumericTabs = { "Önce / Sonra", "Saldırı izi", "Makbuz ve buff", "Kontroller", "Şans", "Rapor" };

        private int numericTab;
        private bool diffOnlyChanged = true, diffWithCell = true, showSources, showTraceText, showNumericGrid;
        private int sourceIndex, traceIndex = -1, traceHit = -1, receiptIndex = -1, checkGroup, chanceBehavior, chanceTrials = 300, chanceSeed = 12345;
        private TileRarity chanceRarity = TileRarity.Common;
        private string exportFolder = "";
        private Vector2 diffScroll, traceScroll, traceTextScroll, checkScroll, headerScroll, buffScroll;
        private readonly HashSet<string> openChecks = new();
        private readonly List<StatSource> sourceView = new();
        private readonly List<TestLog.Entry> buffView = new();
        private readonly List<CheckDefinition> checkSelection = new();
        private GUIStyle tableStyle, tableHead, badgeStyle;
        // Tablo sütun genişlikleri 1000 birimlik referansa göre yazılır ve pencere genişliğine ölçeklenir (yatay kaydırma olmasın).
        private float tableScale = 1f;
        private float W(float width) => width * tableScale;

        private static readonly Color RowTint = new Color(1f, 1f, 1f, 0.045f), ChangedTint = new Color(1f, 0.82f, 0.25f, 0.13f), HeadTint = new Color(0f, 0f, 0f, 0.22f);

        private void NumericStyles()
        {
            if (tableStyle != null) return;
            tableStyle = new GUIStyle(EditorStyles.label) { fontSize = 11, wordWrap = true, richText = false, alignment = TextAnchor.UpperLeft };
            tableHead = new GUIStyle(EditorStyles.miniBoldLabel) { wordWrap = true, alignment = TextAnchor.LowerLeft };
            badgeStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            badgeStyle.normal.textColor = Color.white;
        }

        private void OnNumericRebuilt()
        {
            traceIndex = traceHit = receiptIndex = -1;
        }

        private void DrawNumeric()
        {
            NumericStyles();
            tableScale = Mathf.Clamp((position.width - 64f) / 1000f, 0.58f, 1.7f);
            numericTab = GUILayout.Toolbar(numericTab, NumericTabs, EditorStyles.miniButton);
            if (LabRoutine.Running)
                EditorGUILayout.HelpBox($"Çalışıyor: {LabRoutine.Name}" + (NumericChecks.Running ? $" · {NumericChecks.CurrentId} ({NumericChecks.Done}/{NumericChecks.Planned})" : ""), MessageType.Info);
            switch (numericTab)
            {
                case 0: DrawComparison(); break;
                case 1: DrawTrace(); break;
                case 2: DrawReceipts(); break;
                case 3: DrawChecks(); break;
                case 4: DrawChance(); break;
                default: DrawReport(); break;
            }
        }

        // ---------------------------------------------------------------- tablo yardımcıları
        private void HeadRow(params (string text, float width)[] cells)
        {
            Rect rect = EditorGUILayout.BeginHorizontal();
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(rect, HeadTint);
            foreach ((string label, float width) in cells) GUILayout.Label(label, tableHead, GUILayout.Width(W(width)));
            EditorGUILayout.EndHorizontal();
        }

        private void Row(int index, bool highlight, params (string text, float width)[] cells)
        {
            Rect rect = EditorGUILayout.BeginHorizontal();
            if (Event.current.type == EventType.Repaint)
            {
                if (highlight) EditorGUI.DrawRect(rect, ChangedTint);
                else if ((index & 1) == 1) EditorGUI.DrawRect(rect, RowTint);
            }
            foreach ((string label, float width) in cells) GUILayout.Label(label, tableStyle, GUILayout.Width(W(width)));
            EditorGUILayout.EndHorizontal();
        }

        private static Color StatusColor(CheckStatus status) => status switch
        {
            CheckStatus.Pass => new Color(0.05f, 0.30f, 0.10f),
            CheckStatus.Fail => new Color(0.48f, 0.05f, 0.04f),
            CheckStatus.NotChecked => new Color(0.42f, 0.25f, 0.01f),
            CheckStatus.Running => new Color(0.05f, 0.20f, 0.42f),
            CheckStatus.Manual => new Color(0.14f, 0.18f, 0.30f),
            CheckStatus.Missing => new Color(0.12f, 0.12f, 0.12f),
            CheckStatus.Warning => new Color(0.36f, 0.08f, 0.30f),
            _ => new Color(0.17f, 0.17f, 0.17f),
        };

        private void Badge(CheckStatus status, float width)
        {
            width = Mathf.Max(96f, W(width));
            Rect rect = GUILayoutUtility.GetRect(width, 18f, GUILayout.Width(width));
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(new Rect(rect.x, rect.y + 1f, rect.width, rect.height - 2f), StatusColor(status));
            GUI.Label(rect, NumericChecks.StatusText(status), badgeStyle);
        }

        // ---------------------------------------------------------------- önce / sonra
        private void DrawComparison()
        {
            Header("Önce / sonra stat karşılaştırması");
            EditorGUILayout.LabelField("1) \"Önce\" görüntüsünü al  →  2) değişikliği yap (ödül kur, stat katkısı ekle, tile değiştir, boss başlat…)  →  3) \"Sonra\" ile karşılaştır.",
                EditorStyles.wordWrappedMiniLabel);
            showNumericGrid = EditorGUILayout.Foldout(showNumericGrid, hasSelection ? $"Hedef hücre: ({anchor.x}, {anchor.z}) — değiştirmek için aç" : "Hedef hücre seç (saksı / bitki / tile satırları için)", true);
            if (showNumericGrid) DrawGrid();
            using (new EditorGUI.DisabledScope(!hasSelection))
                diffWithCell = EditorGUILayout.ToggleLeft(hasSelection ? $"Oyuncuyla birlikte seçili hücreyi de karşılaştır: ({anchor.x}, {anchor.z})" : "Hücre seçilmedi: yalnız oyuncu karşılaştırılır", diffWithCell);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("1 · \"Önce\" görüntüsünü al", GUILayout.Height(26f)))
                Say(Comparison.TakeBefore(hasSelection && diffWithCell ? anchor : (GridPosition?)null));
            using (new EditorGUI.DisabledScope(Comparison.Before == null))
            {
                if (GUILayout.Button("2 · \"Sonra\" görüntüsünü al ve karşılaştır", GUILayout.Height(26f))) Say(Comparison.TakeAfter(diffOnlyChanged));
                if (GUILayout.Button("Temizle", GUILayout.Width(70f), GUILayout.Height(26f))) Comparison.Clear();
            }
            EditorGUILayout.EndHorizontal();
            bool only = EditorGUILayout.ToggleLeft("Yalnız değişen satırları göster", diffOnlyChanged);
            if (only != diffOnlyChanged) { diffOnlyChanged = only; Comparison.Refilter(only); }

            NumericSnapshot before = Comparison.Before, after = Comparison.After;
            if (before == null) { EditorGUILayout.HelpBox("Henüz görüntü alınmadı.", MessageType.None); DrawSources(); return; }
            EditorGUILayout.LabelField($"Önce: {before.Target} · R{before.Round} · {before.GameTime:0.00} sn · {before.Rows.Count} satır" +
                                       (after != null ? $"     Sonra: R{after.Round} · {after.GameTime:0.00} sn" : "     Sonra: henüz alınmadı"), EditorStyles.miniLabel);
            if (after != null)
            {
                List<DiffRow> rows = Comparison.Rows;
                if (rows.Count == 0) EditorGUILayout.HelpBox("İki görüntü arasında değişen değer yok.", MessageType.None);
                else
                {
                    // Kısa tablo olduğu gibi çizilir; uzun tablo (bütün satırlar) kendi kaydırma alanında kalır.
                    bool longTable = rows.Count > 14;
                    if (longTable) diffScroll = EditorGUILayout.BeginScrollView(diffScroll, GUILayout.Height(460f));
                    HeadRow(("Stat", 190f), ("Kapsam", 100f), ("Önce", 100f), ("Sonra", 100f), ("Mutlak fark", 120f), ("Oran", 100f), ("Kaynak · birim · not", 280f));
                    for (int i = 0; i < rows.Count; i++)
                    {
                        DiffRow row = rows[i];
                        string note = (row.Source.Length > 0 ? row.Source + "\n" : "") + "birim: " + row.Unit + (row.Note.Length > 0 ? " · " + row.Note : "");
                        Row(i, row.Changed && !diffOnlyChanged, (row.Label, 190f), (row.Scope, 100f), (row.Before, 100f), (row.After, 100f), (row.Delta, 120f), (row.Ratio, 100f), (note, 280f));
                    }
                    if (longTable) EditorGUILayout.EndScrollView();
                    EditorGUILayout.Space(4f);
                }
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Önce/sonra farkını kopyala")) Say(LabReport.Copy("önce / sonra farkı", Snapshots.ToText(before, after, rows)));
                if (GUILayout.Button("CSV olarak kopyala")) Say(LabReport.Copy("önce / sonra CSV", Snapshots.ToCsv(rows)));
                if (GUILayout.Button("Console'a yaz")) Say(LabReport.ToConsole("Önce / sonra", Snapshots.ToText(before, after, rows)));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.LabelField("Şans farkı YÜZDE PUANI, çarpan farkı çarpan, süre farkı saniyedir (parantezde saniyedeki sayı). Oran = sonra ÷ önce; önce 0 ise \"hesaplanamaz\". " +
                                       "\"— ham taban\" satırları oyun verisidir; diğerleri çözülmüş gerçek değerdir. \"Doğrudan vuruş (sapmasız)\" satırları oyunun hasar çarpımının " +
                                       "satırda yazılı bağlamla çağrılmasıdır, gerçek bir saldırının sonucu değildir: gerçek sonuç için \"Saldırı izi\".", EditorStyles.wordWrappedMiniLabel);
            DrawSources();
        }

        private void DrawSources()
        {
            showSources = EditorGUILayout.Foldout(showSources, "Gerçek katkı kaynakları (seçili stat)", true);
            if (!showSources) return;
            var names = new string[TestStats.Offered.Length];
            for (int i = 0; i < names.Length; i++) names[i] = TestStats.Offered[i].label;
            sourceIndex = EditorGUILayout.Popup("Stat", Mathf.Clamp(sourceIndex, 0, names.Length - 1), names);
            (StatType stat, StatTarget target, string _) = TestStats.Offered[sourceIndex];
            PlanterBrain planter = hasSelection ? TestField.PlanterAt(anchor) : null;
            bool planterScope = target == StatTarget.Planter;
            if (planterScope && planter == null) { EditorGUILayout.HelpBox("Bu stat saksı kapsamlıdır: dökümü görmek için gridden saksısı olan bir hücre seç.", MessageType.Info); return; }
            StatSources.Collect(stat, target, planterScope ? planter : null, sourceView);
            float value = planterScope ? planter.GetFinalStat(stat) : TestStats.Final(stat, target);
            EditorGUILayout.LabelField("Çözülmüş gerçek değer", Snapshots.Format(Snapshots.UnitOf(stat), value) + (planterScope ? $" · saksı ({anchor.x}, {anchor.z})" : " · oyuncu"));
            if (sourceView.Count == 0) EditorGUILayout.LabelField("Kaynak dökümü yok: bu değer için kaynak kaydı tutulmuyor.");
            for (int i = 0; i < sourceView.Count; i++) Row(i, sourceView[i].Kind == SourceKind.Test, (sourceView[i].Describe(), 980f));
            EditorGUILayout.LabelField(StatSources.OrderNote, EditorStyles.wordWrappedMiniLabel);
        }

        // ---------------------------------------------------------------- saldırı izi
        private void DrawTrace()
        {
            Header("Bir sonraki saldırıyı kaydet");
            TraceRecord current = TestTrace.Current;
            string state = current != null ? $"KAYITTA · iz #{current.Id} · {current.Hits.Count} vuruş, {current.Harvests.Count} hasat · bekleyen: {(TestTrace.PendingWork().Length > 0 ? TestTrace.PendingWork() : "yok (sessizlik bekleniyor)")}"
                : TestTrace.Armed ? "KURULU · bir sonraki oyuncu saldırısı kaydedilecek" : "boşta (hiçbir olaya abone değil)";
            EditorGUILayout.LabelField("Durum", state);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(current != null || TestTrace.Armed || LabRoutine.Running))
            {
                if (GUILayout.Button("Bir sonraki saldırıyı kaydet", GUILayout.Height(26f))) { TestTrace.Arm(); Say(Result.Success("İz kuruldu: bir sonraki oyuncu saldırısı ve devam olayları kaydedilecek.")); }
                if (GUILayout.Button("Kaydet ve nişana tek saldırı at", GUILayout.Height(26f)))
                {
                    PlayerController player = TestAttack.Player;
                    Vector3 target = TestAttack.Aim ?? (hasSelection ? TestAttack.WorldOf(anchor) : player != null ? player.CursorWorldPosition : Vector3.zero);
                    TestTrace.Arm();
                    Result attacked = TestAttack.AttackOnce(target);
                    if (!attacked.Ok) TestTrace.Cancel();
                    Say(attacked);
                }
            }
            using (new EditorGUI.DisabledScope(current == null && !TestTrace.Armed))
                if (GUILayout.Button("Durdur", GUILayout.Width(70f), GUILayout.Height(26f))) { TestTrace.Cancel(); Say(Result.Success("İz durduruldu (açık kayıt \"kesildi, sonuç eksik\" diye kapanır).")); }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField($"Kayıt, bekleyen iş (zincir, ikinci darbe, ödül zamanlayıcısı, kasırga, bumerang, hilal dalgası) bitene ve {TestTrace.QuietSeconds:0.##} sn sessizlik geçene kadar açık kalır. " +
                                       $"Sınır: {TestTrace.MaxEvents} olay ya da {TestTrace.MaxSeconds:0} sn oyun zamanı; aşılırsa \"kayıt kesildi, sonuç eksik\". Son {TestTrace.Kept} iz saklanır." +
                                       (TestTrace.Faults > 0 ? $" Dinleyici hatası: {TestTrace.Faults}." : ""), EditorStyles.wordWrappedMiniLabel);

            IReadOnlyList<TraceRecord> records = TestTrace.Records;
            if (records.Count == 0) { EditorGUILayout.HelpBox("Henüz kapanmış bir iz yok.", MessageType.None); return; }
            var titles = new string[records.Count];
            for (int i = 0; i < records.Count; i++) titles[i] = LabReport.TraceTitle(records[i]);
            // Yeni iz seçilince ilk vuruşun hesabı açık gelir.
            if (traceIndex < 0 || traceIndex >= records.Count) { traceIndex = records.Count - 1; traceHit = 0; }
            int chosen = EditorGUILayout.Popup("İz", traceIndex, titles);
            if (chosen != traceIndex) { traceIndex = chosen; traceHit = 0; }
            TraceRecord record = records[traceIndex];

            if (record.Truncated) EditorGUILayout.HelpBox("KAYIT KESİLDİ, SONUÇ EKSİK — " + record.TruncatedReason, MessageType.Warning);
            foreach (string warning in record.Interference) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            EditorGUILayout.LabelField($"Saldırı S{record.AttackSerial} · zincir kökü {(record.ChainRoot != 0 ? record.ChainRoot.ToString() : "yok")} · gerçek temas yarıçapı {record.Radius:0.###} · değen hücre {record.Cells} · " +
                                       $"canlı hedef {record.LivingTargets} · süre {record.EndTime - record.StartTime:0.000} sn", EditorStyles.wordWrappedLabel);

            Header($"Vuruşlar ({record.Hits.Count}) — satıra tıkla: hesabı aşağıda");
            bool manyHits = record.Hits.Count > 12;
            if (manyHits) traceScroll = EditorGUILayout.BeginScrollView(traceScroll, GUILayout.Height(320f));
            HeadRow(("#", 30f), ("zaman", 60f), ("tür", 150f), ("kaynak", 170f), ("hedef", 150f), ("can önce → sonra", 110f), ("hesaplanan darbe", 80f), ("gerçek can azalması", 80f), ("taşan", 60f), ("sonuç", 110f));
            for (int i = 0; i < record.Hits.Count; i++)
            {
                TraceHit hit = record.Hits[i];
                Rect rect = EditorGUILayout.BeginHorizontal();
                if (Event.current.type == EventType.Repaint)
                {
                    if (i == traceHit) EditorGUI.DrawRect(rect, ChangedTint);
                    else if ((i & 1) == 1) EditorGUI.DrawRect(rect, RowTint);
                }
                string type = hit.Kind + (hit.Type == DamageType.Reward ? " · " + hit.Reward : hit.Type != DamageType.Direct ? " · " + LabReport.TypeName(hit.Type) : "") + (hit.Generation > 0 ? $" · nesil {hit.Generation}" : "");
                string result = (hit.Killed ? "HASAT " : "") + (hit.Crit ? "KRİTİK " : "") + (hit.Linked ? "" : "(bağ kanıtı yok)");
                GUILayout.Label(hit.Index.ToString(), tableStyle, GUILayout.Width(W(30f)));
                GUILayout.Label($"+{hit.Time:0.000}", tableStyle, GUILayout.Width(W(60f)));
                GUILayout.Label(type, tableStyle, GUILayout.Width(W(150f)));
                GUILayout.Label(hit.Source, tableStyle, GUILayout.Width(W(170f)));
                GUILayout.Label($"({hit.Target.x}, {hit.Target.z}) {hit.Plant}", tableStyle, GUILayout.Width(W(150f)));
                GUILayout.Label($"{hit.HealthBefore} → {hit.HealthAfter} / {hit.MaxHealth}", tableStyle, GUILayout.Width(W(110f)));
                GUILayout.Label(hit.Computed.ToString(), tableStyle, GUILayout.Width(W(80f)));
                GUILayout.Label(hit.Decrease.ToString(), tableStyle, GUILayout.Width(W(80f)));
                GUILayout.Label(hit.Overkill.ToString(), tableStyle, GUILayout.Width(W(60f)));
                GUILayout.Label(result, tableStyle, GUILayout.Width(W(110f)));
                EditorGUILayout.EndHorizontal();
                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition)) { traceHit = i; Event.current.Use(); Repaint(); }
            }
            if (manyHits) EditorGUILayout.EndScrollView();
            if (record.Hits.Count == 0) EditorGUILayout.LabelField("Vuruş yok (boş savuruş).", EditorStyles.miniLabel);
            if (traceHit >= 0 && traceHit < record.Hits.Count)
            {
                TraceHit hit = record.Hits[traceHit];
                text.Clear();
                foreach (string line in hit.Calculation) text.Append(line).Append('\n');
                EditorGUILayout.HelpBox($"Vuruş #{hit.Index} gerçek hesap yolu:\n" + (text.Length > 0 ? text.ToString().TrimEnd('\n') : "bu vuruş için hesap satırı yok"), MessageType.None);
            }

            Header($"Hasatlar ({record.Harvests.Count}) ve hesap farkı");
            EditorGUILayout.TextArea(LabReport.EconomyText(record).TrimEnd('\n'), wrapStyle);

            showTraceText = EditorGUILayout.Foldout(showTraceText, "Tam metin (vuruş hesapları, hasat ödülleri, devam olayları)", true);
            if (showTraceText)
            {
                traceTextScroll = EditorGUILayout.BeginScrollView(traceTextScroll, GUILayout.MinHeight(200f), GUILayout.MaxHeight(480f));
                EditorGUILayout.TextArea(LabReport.TraceText(record), wrapStyle);
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Seçili saldırı zincirini Console'a yaz")) Say(LabReport.ToConsole("Saldırı izi " + LabReport.TraceTitle(record), LabReport.TraceText(record)));
            if (GUILayout.Button("Panoya kopyala")) Say(LabReport.Copy("saldırı izi", LabReport.TraceText(record)));
            if (GUILayout.Button("Vuruş tablosunu CSV kopyala")) Say(LabReport.Copy("vuruş tablosu CSV", LabReport.TraceHitsCsv(new[] { record })));
            EditorGUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- makbuz ve buff
        private void DrawReceipts()
        {
            Header("Aktif geçici / koşullu etkiler (oyunun durumundan)");
            text.Clear();
            BuffWatch.Describe(text);
            EditorGUILayout.TextArea(text.ToString().TrimEnd('\n'), wrapStyle);

            Header("Yaşam döngüsü kaydı: eklendi · yenilendi · hak hazırlandı / harcandı · süresi bitti · temizlendi");
            if (!TestLog.Enabled) EditorGUILayout.HelpBox("Olay kaydı kapalı: yaşam döngüsü yazılmıyor. \"Rapor\" alt sekmesinden seviyeyi \"İşlem özeti\" ya da \"Ayrıntılı sayısal iz\" yap.", MessageType.Info);
            TestLog.Collect(TestLog.Kind.Buff, null, 0, buffView);
            if (buffView.Count == 0) EditorGUILayout.LabelField("Henüz yaşam döngüsü olayı yok.", EditorStyles.miniLabel);
            else
            {
                bool manyBuffs = buffView.Count > 10;
                if (manyBuffs) buffScroll = EditorGUILayout.BeginScrollView(buffScroll, GUILayout.Height(200f));
                for (int i = Mathf.Max(0, buffView.Count - 60); i < buffView.Count; i++) GUILayout.Label(TestLog.Format(buffView[i]), monoStyle);
                if (manyBuffs) EditorGUILayout.EndScrollView();
            }

            Header($"Ödül uygulama makbuzları ({TestRewards.Receipts.Count}; son {TestRewards.KeptReceipts} saklanır)");
            IReadOnlyList<RewardReceipt> receipts = TestRewards.Receipts;
            if (receipts.Count == 0) { EditorGUILayout.HelpBox("Henüz kontrollü ödül kurulumu yapılmadı ('D · Ödüller').", MessageType.None); return; }
            var titles = new string[receipts.Count];
            for (int i = 0; i < receipts.Count; i++) titles[i] = $"{i + 1} · {receipts[i].Name} · {(receipts[i].Success ? "başarılı" : "reddedildi")}";
            if (receiptIndex < 0 || receiptIndex >= receipts.Count) receiptIndex = receipts.Count - 1;
            receiptIndex = EditorGUILayout.Popup("Makbuz", receiptIndex, titles);
            RewardReceipt receipt = receipts[receiptIndex];
            EditorGUILayout.HelpBox(receipt.Verdict, receipt.Success ? MessageType.None : MessageType.Warning);
            EditorGUILayout.TextArea(receipt.ToText().TrimEnd('\n'), wrapStyle);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Makbuzu Console'a yaz")) Say(LabReport.ToConsole("Ödül makbuzu · " + receipt.Name, receipt.ToText()));
            if (GUILayout.Button("Panoya kopyala")) Say(LabReport.Copy("ödül makbuzu", receipt.ToText()));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Makbuz, kurulumdan hemen önce ve sonra alınan iki gerçek görüntünün farkıdır (oyuncu + referans saksı). Ödül adının listede görünmesi tek başına " +
                                       "\"uygulandı\" sayılmaz. Bu araç genel bir ödül geri alma sistemi içermez: temiz duruma dönmek için senaryoyu yeniden kur.", EditorStyles.wordWrappedMiniLabel);
        }

        // ---------------------------------------------------------------- kontroller
        private void DrawChecks()
        {
            Header("Beklenen / gerçekleşen kontroller");
            EditorGUILayout.HelpBox("Kontroller senaryoyu YENİDEN KURAR, saldırıdan hemen önce oyunun rastgele akışını sabit seed'le başlatır (açık test eylemi) ve bitince önceki senaryoyu geri kurar. " +
                                    "Beklenen değer bağımsız aritmetikle (asset değeri + işlemden önce ölçülen değer), gerçekleşen değer oyundan ölçülerek bulunur. " +
                                    "Otomatik karşılaştırılamayan kısım \"manuel inceleme gerekli\", gerçekleşmeyen olay \"İNCELENMEDİ\" olarak kalır; ikisi de PASS sayılmaz. " +
                                    "UYARI: hesap kurala uygun ama oyuncuya yazılan değerin bir kısmı kullanılamıyor ya da yazım yanıltıcı.", MessageType.None);
            IReadOnlyList<CheckDefinition> catalog = NumericChecks.Catalog;
            var groups = new List<string> { "Bütün gruplar" };
            foreach (CheckDefinition definition in catalog) if (!groups.Contains(definition.Group)) groups.Add(definition.Group);
            checkGroup = EditorGUILayout.Popup("Grup", Mathf.Clamp(checkGroup, 0, groups.Count - 1), groups.ToArray());
            string group = checkGroup == 0 ? null : groups[checkGroup];
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(LabRoutine.Running))
                if (GUILayout.Button(group == null ? $"Hepsini çalıştır ({catalog.Count})" : $"Gruptakileri çalıştır: {group}", GUILayout.Height(26f)))
                {
                    checkSelection.Clear();
                    foreach (CheckDefinition definition in catalog) if (group == null || definition.Group == group) checkSelection.Add(definition);
                    Say(NumericChecks.Run(checkSelection));
                }
            using (new EditorGUI.DisabledScope(!NumericChecks.Running))
                if (GUILayout.Button("Durdur", GUILayout.Width(70f), GUILayout.Height(26f))) NumericChecks.Stop();
            using (new EditorGUI.DisabledScope(LabRoutine.Running))
                if (GUILayout.Button("Sonuçları temizle", GUILayout.Width(120f), GUILayout.Height(26f))) NumericChecks.Clear();
            EditorGUILayout.EndHorizontal();
            if (NumericChecks.Results.Count > 0) EditorGUILayout.LabelField(NumericChecks.Summary(), EditorStyles.wordWrappedLabel);

            checkScroll = EditorGUILayout.BeginScrollView(checkScroll, GUILayout.MinHeight(260f), GUILayout.MaxHeight(620f));
            string shownGroup = null;
            foreach (CheckDefinition definition in catalog)
            {
                if (group != null && definition.Group != group) continue;
                if (definition.Group != shownGroup) { shownGroup = definition.Group; EditorGUILayout.Space(4f); EditorGUILayout.LabelField(shownGroup, EditorStyles.boldLabel); }
                CheckResult result = NumericChecks.ResultOf(definition.Id);
                EditorGUILayout.BeginHorizontal();
                Badge(result != null ? result.Status : definition.Body == null ? (definition.Coverage == CheckCoverage.Missing ? CheckStatus.Missing : CheckStatus.Manual) : CheckStatus.NotRun, 150f);
                bool open = openChecks.Contains(definition.Id);
                bool now = EditorGUILayout.Foldout(open, $"{definition.Id} · {definition.Title}", true);
                if (now != open) { if (now) openChecks.Add(definition.Id); else openChecks.Remove(definition.Id); }
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(LabRoutine.Running || definition.Body == null))
                    if (GUILayout.Button("Çalıştır", EditorStyles.miniButton, GUILayout.Width(70f)))
                    {
                        checkSelection.Clear();
                        checkSelection.Add(definition);
                        Say(NumericChecks.Run(checkSelection));
                    }
                EditorGUILayout.EndHorizontal();
                if (!now) continue;
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Kapsam: {NumericChecks.CoverageText(definition.Coverage)}" + (definition.Precondition.Length > 0 ? $" · Ön koşul: {definition.Precondition}" : "") +
                                           (definition.Action.Length > 0 ? $" · İşlem: {definition.Action}" : ""), EditorStyles.wordWrappedMiniLabel);
                if (definition.Note.Length > 0) EditorGUILayout.LabelField("Not: " + definition.Note, EditorStyles.wordWrappedMiniLabel);
                EditorGUI.indentLevel--;
                if (result == null || result.Lines.Count == 0) continue;
                HeadRow(("durum", Mathf.Max(150f, 96f / tableScale)), ("kontrol", 330f), ("beklenen", 150f), ("gerçekleşen", 180f), ("tolerans", 170f));
                for (int i = 0; i < result.Lines.Count; i++)
                {
                    CheckLine line = result.Lines[i];
                    Rect rect = EditorGUILayout.BeginHorizontal();
                    if (Event.current.type == EventType.Repaint && (i & 1) == 1) EditorGUI.DrawRect(rect, RowTint);
                    Badge(line.Status, 150f);
                    GUILayout.Label(line.Name, tableStyle, GUILayout.Width(W(330f)));
                    GUILayout.Label(line.Expected, tableStyle, GUILayout.Width(W(150f)));
                    GUILayout.Label(line.Actual, tableStyle, GUILayout.Width(W(180f)));
                    GUILayout.Label(line.Tolerance, tableStyle, GUILayout.Width(W(170f)));
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Sonuçları Console'a yaz")) Say(LabReport.ToConsole("Kontroller", NumericChecks.Summary() + "\n" + NumericChecks.AllText()));
            if (GUILayout.Button("Sonuçları kopyala")) Say(LabReport.Copy("kontrol sonuçları", NumericChecks.Summary() + "\n" + NumericChecks.AllText()));
            if (GUILayout.Button("Kapsam matrisini kopyala")) Say(LabReport.Copy("kapsam matrisi", NumericChecks.Matrix()));
            EditorGUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- şans
        private void DrawChance()
        {
            Header("Şans mekanikleri: kontrollü örnekleme (gerçek tetik yolu)");
            EditorGUILayout.HelpBox("Her deneme: bitki oyunun üretim girişinden çıkar (canı 1), tek gerçek doğrudan saldırı atılır, şans zarını oyunun davranış kodu atar. " +
                                    "\"Zar başarılı\" ile \"davranış başlatıldı\" ayrı sayılır (kasırga / bumerangta kapasite dolunca zar tutar ama davranış başlamaz). " +
                                    "Örnek başlarken oyunun rastgele akışı seed'le başlatılır (açık test eylemi). Küçük örnek kesin PASS / FAIL değildir.", MessageType.None);
            var names = new string[ChanceSampler.Behaviors.Length];
            for (int i = 0; i < names.Length; i++) names[i] = ChanceSampler.Behaviors[i].label;
            chanceBehavior = EditorGUILayout.Popup("Davranış", Mathf.Clamp(chanceBehavior, 0, names.Length - 1), names);
            chanceRarity = (TileRarity)EditorGUILayout.EnumPopup("Hazır düzenin tile nadirliği", chanceRarity);
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 230f;
            chanceTrials = Mathf.Clamp(EditorGUILayout.IntField($"Deneme sayısı (1 – {ChanceSampler.MaxTrials})", chanceTrials), 1, ChanceSampler.MaxTrials);
            chanceSeed = EditorGUILayout.IntField("Seed (oyunun rastgele akışı)", chanceSeed);
            EditorGUIUtility.labelWidth = labelWidth;
            DamageType type = ChanceSampler.Behaviors[chanceBehavior].type;
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(LabRoutine.Running))
            {
                if (GUILayout.Button("1 · Hazır sabit koşulu kur (senaryoyu yeniden kurar)", GUILayout.Height(24f))) Say(TestSession.Rebuild(ChanceSampler.Preset(type, chanceRarity)));
                if (GUILayout.Button("2 · Merkez saksıda örnekle", GUILayout.Height(24f)))
                    Say(ChanceSampler.Run(new GridPosition(ScenarioPresets.C, ScenarioPresets.C), type, chanceTrials, chanceSeed));
                using (new EditorGUI.DisabledScope(!hasSelection))
                    if (GUILayout.Button(hasSelection ? $"Seçili hücrede örnekle ({anchor.x}, {anchor.z})" : "Seçili hücrede örnekle", GUILayout.Height(24f)))
                        Say(ChanceSampler.Run(anchor, type, chanceTrials, chanceSeed));
            }
            using (new EditorGUI.DisabledScope(!ChanceSampler.Running))
                if (GUILayout.Button("Durdur", GUILayout.Width(70f), GUILayout.Height(24f))) ChanceSampler.Stop();
            EditorGUILayout.EndHorizontal();

            IReadOnlyList<ChanceSample> samples = ChanceSampler.Samples;
            if (samples.Count == 0) { EditorGUILayout.HelpBox("Henüz örnek yok.", MessageType.None); return; }
            EditorGUILayout.Space(4f);
            HeadRow(("davranış", 80f), ("seed", 60f), ("deneme", 75f), ("gerçek fırsat", 80f), ("başarılı zar", 80f), ("başlatılan", 80f), ("kapasite reddi", 85f), ("gözlenen oran", 90f), ("beklenen şans", 90f), ("%95 aralık", 120f), ("durum", 150f));
            for (int i = samples.Count - 1; i >= 0; i--)
            {
                ChanceSample s = samples[i];
                string status = !s.Complete ? (s.Stopped ? "durduruldu (eksik)" : "sürüyor") : s.Opportunities == 0 ? "fırsat yok" : (s.Inside ? "çelişki yok" : "İNCELE") + (s.SmallSample ? " · küçük örnek" : "");
                Row(samples.Count - 1 - i, s.Complete && s.Opportunities > 0 && !s.Inside, (ChanceSampler.Label(s.Behavior), 80f), (s.Seed.ToString(), 60f), ($"{s.Trials}/{s.Requested}", 75f), (s.Opportunities.ToString(), 80f),
                    (s.Rolled.ToString(), 80f), (s.Started.ToString(), 80f), (s.Rejected.ToString(), 85f), (s.Opportunities > 0 ? (s.Observed * 100d).ToString("0.##") + "%" : "hesaplanamaz", 90f),
                    ((s.Expected * 100d).ToString("0.##") + "%", 90f), (s.Opportunities > 0 ? $"{s.Low * 100d:0.#}% – {s.High * 100d:0.#}%" : "—", 120f), (status, 150f));
            }
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Son örnek", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(samples[samples.Count - 1].ToText().TrimEnd('\n'), wrapStyle);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Son örneği Console'a yaz")) Say(LabReport.ToConsole("Şans örneği", samples[samples.Count - 1].ToText()));
            if (GUILayout.Button("Tabloyu CSV kopyala")) Say(LabReport.Copy("şans tablosu CSV", ChanceSampler.ToCsv()));
            using (new EditorGUI.DisabledScope(LabRoutine.Running))
                if (GUILayout.Button("Örnekleri temizle")) ChanceSampler.Clear();
            EditorGUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- rapor
        private static readonly string[] LevelNames = { "Kapalı", "İşlem özeti", "Ayrıntılı sayısal iz" };

        private void DrawLogLevel()
        {
            int level = EditorGUILayout.Popup("Olay kaydı seviyesi", (int)TestLog.Level, LevelNames);
            if (level != (int)TestLog.Level) { TestLog.Preferred = (TestLog.LogLevel)level; TestLog.SetLevel((TestLog.LogLevel)level); logVersion = -1; }
            EditorGUILayout.LabelField("Kapalı: hiçbir olaya abone olunmaz, metin üretilmez. İşlem özeti: saldırı, hasat, davranış, ödül, buff, olay ve komutlar. Ayrıntılı sayısal iz: " +
                                       "ayrıca her vuruş, kritik, zincir zarı, stat değişikliği ve doğrudan vuruşun hesap satırı. Kayıt Console'a kendiliğinden yazmaz; halka " +
                                       $"{TestLog.Capacity} satırla sınırlıdır.", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawReport()
        {
            Header("Kayıt seviyesi");
            DrawLogLevel();

            Header("Console");
            EditorGUILayout.LabelField("Son sayısal işlem", LabReport.LastTitle.Length > 0 ? LabReport.LastTitle : "yok");
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(LabReport.LastText.Length == 0))
            {
                if (GUILayout.Button("Son işlemi Console'a yaz", GUILayout.Height(24f))) Say(LabReport.LastToConsole());
                if (GUILayout.Button("Son işlemi kopyala", GUILayout.Height(24f))) Say(LabReport.Copy("son işlem", LabReport.LastText));
            }
            EditorGUILayout.EndHorizontal();

            Header("Oturum raporunu dışa aktar");
            if (string.IsNullOrEmpty(exportFolder)) exportFolder = LabReport.DefaultFolder;
            EditorGUILayout.BeginHorizontal();
            exportFolder = EditorGUILayout.TextField("Çıktı klasörü", exportFolder);
            if (GUILayout.Button("Seç…", GUILayout.Width(60f)))
            {
                string picked = EditorUtility.OpenFolderPanel("Mechanics Test Lab raporu için klasör", exportFolder, "");
                if (!string.IsNullOrEmpty(picked)) exportFolder = picked;
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
            if (!LabReport.SafeFolder(exportFolder, out string reason)) EditorGUILayout.HelpBox("Bu klasöre yazılmaz: " + reason + ".", MessageType.Warning);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Oturum raporunu dışa aktar (TXT + JSON + CSV tabloları)", GUILayout.Height(26f))) Say(LabReport.Export(exportFolder));
            using (new EditorGUI.DisabledScope(LabReport.LastExportFolder.Length == 0))
                if (GUILayout.Button("Son raporu göster", GUILayout.Width(130f), GUILayout.Height(26f))) EditorUtility.RevealInFinder(LabReport.LastExportFolder);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Rapor klasöründe: rapor.txt (okunabilir), rapor.json, once-sonra.csv, saldiri-izi-vuruslar.csv, saldiri-izi-hasatlar.csv, kontroller.csv, sans.csv. " +
                                       "Gerçek oyuncu kaydının klasörüne ve Assets'e yazılmaz. Paylaşmak için klasörü olduğu gibi gönder.", EditorStyles.wordWrappedMiniLabel);

            Header("Rapor başlığı (önizleme)");
            headerScroll = EditorGUILayout.BeginScrollView(headerScroll, GUILayout.MinHeight(130f), GUILayout.MaxHeight(220f));
            EditorGUILayout.TextArea(LabReport.Header(LabReport.AnyTruncated()).TrimEnd('\n'), wrapStyle);
            EditorGUILayout.EndScrollView();
        }
    }
}
