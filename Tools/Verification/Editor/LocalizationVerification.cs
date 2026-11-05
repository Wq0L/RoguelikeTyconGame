using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

// Yerelleştirme doğrulaması (4–5 Kasım 2026): ana liste (Assets/Localization/GameText.csv) → Unity Localization "GameText"
// tablosu, kurulumun ayarları ve tekrar çalıştırılınca bozmaması, denetim, yer tutucu, çoğul (Smart), yedek dil zinciri, sahte dil,
// LocalizedText bileşeni, CSV kaydedilince yazıların kendiliğinden değişmesi (gerçek dosya değiştirilip içe aktarılır) ve sabit
// yazı taraması. Yalnız izole kopyada. Çalıştırma:
//   Tools/run-isolated-verification.ps1 -Method LocalizationVerification.RunBatch -Full
// Çıktı: Logs/LocalizationVerification.txt, Logs/SabitYazilar.csv (çeviri listesi taslağı).
[InitializeOnLoad]
public static class LocalizationVerification
{
    const string Key = "LocalizationVerification", EditNotes = "Logs/LocalizationVerification_edit.txt";
    static readonly List<string> notes = new();
    static int step;
    static double next;

    static LocalizationVerification() { EditorApplication.update += Tick; }

    static void Check(bool value, string text) { if (!value) throw new Exception(text); notes.Add("PASS " + text); }

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        try
        {
            SetupChecks();
            ImportChecks();
            CsvPathChecks();
            CoverageChecks();
            ScannerChecks();
        }
        catch (Exception ex) { Finish(ex); return; }
        // Play'e geçişte alan yeniden yüklenir, bellekteki notlar silinir: düzenleme modu sonuçları dosyada bekler.
        Directory.CreateDirectory("Logs");
        File.WriteAllLines(EditNotes, notes);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static StringTableCollection Collection => LocalizationEditorSettings.GetStringTableCollection(GameText.TableName);
    static StringTable Table(string code) => Collection.GetTable(new LocaleIdentifier(code)) as StringTable;
    static List<string> CsvKeys(string csv) => GameText.Parse(csv).Skip(1).Where(r => r.Count > 0 && !string.IsNullOrWhiteSpace(r[0])).Select(r => r[0].Trim()).ToList();

    // ---- kurulum ----
    static void SetupChecks()
    {
        Check(Resources.Load<TextAsset>("Dialogue/dialogue_text") == null && Resources.Load<TextAsset>("Localization/GameText") == null,
            "no text table left in Resources (the game reads Unity tables; the CSV is the editing list)");
        // Kopya her çalıştırmada kullanıcının proje dosyalarıyla eşitlenir: buradaki ayar, ana projenin kayıtlı ayarıdır.
        List<string> before = LocalizationSetup.IsComplete ? LocalizationSetup.ConfigProblems() : new List<string> { "kurulum yok" };
        notes.Add("INFO main project's saved setup before Run: " + (before.Count == 0 ? "up to date" : string.Join(" | ", before)));
        if (before.Count > 0) Check(LocalizationAutoSetup.ShouldRun, "an outdated saved setup makes the editor's auto setup run (it no longer stops at 'already installed')");
        LocalizationSetup.ImportReport first = LocalizationSetup.Run();
        notes.Add($"INFO setup: +{first.Added} ~{first.Updated} -{first.Removed} ={first.Unchanged}, smart {first.Smart}, languages {string.Join(",", first.Languages)}");
        Check(LocalizationSetup.IsComplete, "setup complete: active settings, source locale tr, GameText table");
        LocalizationSettings s = LocalizationEditorSettings.ActiveLocalizationSettings;
        var so = new SerializedObject(s);
        Check(so.FindProperty("m_InitializeSynchronously").boolValue && so.FindProperty("m_ProjectLocaleIdentifier.m_Code").stringValue == "en",
            "tables initialise synchronously; project (source) locale is English");
        Check(s.GetStringDatabase().UseFallback && s.GetStartupLocaleSelectors().Count == 2 && s.GetStartupLocaleSelectors()[0] is SystemLocaleSelector
              && s.GetStartupLocaleSelectors()[1] is SpecificLocaleSelector sel && sel.LocaleId.Code == "en",
            "fallback is on; Unity's start language: the computer's language, else English (the player's choice is applied by GameText)");
        Locale en = LocalizationEditorSettings.GetLocales().First(l => l.Identifier.Code == "en");
        Locale tr = LocalizationEditorSettings.GetLocales().First(l => l.Identifier.Code == "tr");
        Check(tr.Metadata.GetMetadata<FallbackLocale>()?.Locale?.Identifier.Code == "en" && en.Metadata.GetMetadata<FallbackLocale>() == null,
            "one-way fallback: Turkish (and every other language) → English → end (no cycle)");
        var link = new FallbackLocale(en);
        Check(link.Locale == en, "a new language can still be linked to English (Unity rejects links into a cyclic chain)");
        Check(LocalizationSetup.ConfigProblems().Count == 0 && !LocalizationAutoSetup.ShouldRun, "after Run the setup is up to date; auto setup stays idle");

        // Eski kurulumun döngüsü (5 Kasım öncesi): en → tr geri eklenir; denetim, otomatik kurulum ve derleme denetimi yakalamalı.
        en.Metadata.AddMetadata(new FallbackLocale(tr));
        Check(en.Metadata.GetMetadata<FallbackLocale>()?.Locale == tr, "old cyclic link en → tr recreated for the test");
        Check(LocalizationSetup.ConfigProblems().Any(p => p.Contains("yedeği olmamalı")) && LocalizationAutoSetup.ShouldRun
              && LocalizationBuildCheck.Problems().Any(p => p.Contains("ayar güncel değil")),
            "a cyclic fallback is reported by the setup check, triggers auto setup and stops builds");
        LocalizationSetup.Run();
        Check(en.Metadata.GetMetadata<FallbackLocale>() == null && tr.Metadata.GetMetadata<FallbackLocale>()?.Locale == en
              && LocalizationSetup.ConfigProblems().Count == 0, "Run repairs the cycle (en → none, tr → en)");
        Check(Collection.StringTables.All(t => t != null && LocalizationEditorSettings.GetPreloadTableFlag(t)), "every language table is preloaded (first read is immediate)");
        Check(AddressableAssetSettingsDefaultObject.Settings != null
              && AddressableAssetSettingsDefaultObject.Settings.BuildAddressablesWithPlayerBuild == AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer,
            "Addressables build runs with every player build (no manual step)");

        string csv = File.ReadAllText(GameText.MasterCsvPath);
        List<string> keys = CsvKeys(csv);
        Check(Collection.SharedData.Entries.Count == keys.Count && keys.All(k => Collection.SharedData.Contains(k)), $"table holds exactly the list's {keys.Count} keys");
        Check(Table("tr").GetEntry("ui.skip")?.Value == "Atla" && Table("en").GetEntry("ui.skip")?.Value == "Skip", "values copied per language");
        Check(keys.Where(k => !k.StartsWith("meta.")).All(k => !string.IsNullOrEmpty(Table("en").GetEntry(k)?.Value))
              && Table("en").GetEntry("tut.hit.0").Value.StartsWith("Don't") && Table("tr").GetEntry("tut.hit.0").Value.StartsWith("Hasat"),
            "English source is complete; Turkish is the first translation");
        Check(Collection.SharedData.GetEntry("ui.skip").Metadata.GetMetadata<Comment>()?.CommentText.StartsWith("Button on the big box") == true,
            "translator note becomes the row comment in Unity");

        // Kullanıcının editöründe görülen (5 Kasım): kurucu editör modunda yazı okurken Unity'nin seçili dili yoktu → paket
        // "SelectedLocale is null" hatası verdi, GameText de gereksiz bir uyarıyla CSV'ye düştü. Okuma artık dili açıkça verir.
        LocalizationSettings.SelectedLocale = null;
        GameText.Reload();
        var logged = new List<string>();
        void Collect(string message, string stack, LogType type) { if (type is LogType.Error or LogType.Exception or LogType.Warning) logged.Add(message); }
        Application.logMessageReceived += Collect;
        string trSkip, enSkip;
        bool unity;
        try
        {
            GameText.Language = "tr";
            unity = GameText.UsingUnityTables;
            trSkip = GameText.Get("ui.skip");
            GameText.Language = "en";
            enSkip = GameText.Get("ui.skip");
            GameText.Language = "tr";
        }
        finally { Application.logMessageReceived -= Collect; }
        Check(unity && trSkip == "Atla" && enSkip == "Skip" && logged.Count == 0 && LocalizationSettings.SelectedLocale == null,
            $"editor mode without Unity's selected language (builders, tools): text comes from the tables, no error or warning, Unity's selection untouched ({string.Join(" | ", logged)})");

        LocalizationSetup.ImportReport again = LocalizationSetup.Run();
        Check(again.Added == 0 && again.Updated == 0 && again.Removed == 0 && again.Unchanged == keys.Count, "running setup again changes nothing");
    }

    // ---- aktarım: ekle / değiştir / sil / Smart ----
    static void ImportChecks()
    {
        string original = File.ReadAllText(GameText.MasterCsvPath);
        string edited = original.Replace("ui.skip,\"Button on the big box: skips the whole conversation. Keep short.\",Skip,Atla",
                                         "ui.skip,\"Button on the big box: skips the whole conversation. Keep short.\",Skip,Geç")
            .TrimEnd('\r', '\n') + "\ntest.new,,New,Yeni\ntest.plural,,\"{0:plural:1 round|{} rounds}\",{0} round\ntest.untr,,Only English,\ntest.blank,,Blank test,\"   \"\n";
        edited = string.Join("\n", edited.Split('\n').Where(l => !l.StartsWith("react.period.0")));
        Check(edited.Contains(",Skip,Geç") && !edited.Contains("react.period.0"), "test list prepared");
        LocalizationSetup.ImportReport r = LocalizationSetup.Import(edited, Collection);
        Check(r.Added == 4 && r.Updated == 1 && r.Removed == 1, $"import adds, updates and removes keys to match the list (+{r.Added} ~{r.Updated} -{r.Removed})");
        Check(Table("tr").GetEntry("test.untr") == null && Table("en").GetEntry("test.untr")?.Value == "Only English",
            "an empty Turkish cell is not written (the line falls back to English)");
        Check(Table("tr").GetEntry("test.blank") == null && GameTextAudit.Run(edited).MissingPerLanguage["tr"] >= 2,
            "a cell with only spaces counts as empty in the audit AND in the import (falls back to English, no blank text on screen)");
        Check(Table("tr").GetEntry("ui.skip").Value == "Geç" && !Collection.SharedData.Contains("react.period.0"), "changed text and removed key reach the table");
        Check(Table("en").GetEntry("test.plural").IsSmart && !Table("tr").GetEntry("test.plural").IsSmart && !Table("tr").GetEntry("test.new").IsSmart,
            "plural pattern is marked Smart; plain {0} stays a normal format string");
        Check(LocalizationBuildCheck.Problems().Any(p => p.Contains("güncel değil") || p.Contains("aynı değil")),
            "pre-build check notices tables that no longer match the list file");
        LocalizationSetup.ImportReport back = LocalizationSetup.Import(original, Collection);
        Check(back.Added == 1 && back.Removed == 4 && Table("tr").GetEntry("ui.skip").Value == "Atla", "re-importing the real list restores the table");
        Check(LocalizationBuildCheck.Problems().Count == 0, "pre-build check is clean again: " + string.Join("; ", LocalizationBuildCheck.Problems()));

        // Bozuk liste: bir notun tırnağı kapanmamış. Önceki sürüm aktarıp ardından uyarırdı; yutulan satırlar tablodan silinirdi.
        int before = Collection.SharedData.Entries.Count;
        string broken = original.Replace("\"Same conversation, line 2.\"", "\"Same conversation, line 2.");
        Check(broken != original, "broken list prepared (unclosed quote)");
        bool blocked = false;
        try { LocalizationSetup.Import(broken, Collection); }
        catch (LocalizationSetup.InvalidListException e) { blocked = e.Report.Errors.Any(x => x.Contains("kapanmamış tırnak") || x.Contains("satır sonu")); }
        Check(blocked && Collection.SharedData.Entries.Count == before && Table("tr").GetEntry("ui.skip").Value == "Atla"
              && Collection.SharedData.Contains("react.period.0"), $"a broken list is rejected BEFORE touching the tables ({Collection.SharedData.Entries.Count}/{before} keys kept)");
        AssetDatabase.SaveAssets();
    }

    // ---- CSV yolu (test zorlaması / kurulumsuz proje) ----
    static void CsvPathChecks()
    {
        GameTextAudit.Report real = GameTextAudit.Run(GameText.SourceCsv);
        Check(real.Ok, "real list passes the audit: " + string.Join(" | ", real.Errors));
        notes.Add($"INFO real list: {real.Keys} keys, missing tr: {real.MissingPerLanguage.GetValueOrDefault("tr")}, warnings: {real.Warnings.Count}");
        GameTextAudit.Report bad = GameTextAudit.Run("key,note,en,tr\na,,One,Bir\na,,Again,Yine\nb,,,İki\nc,,{0} gold,{1} altın\nd,,Bold,<b>Kalın</b>\nE x,,Ok,Ok\n");
        Check(bad.Errors.Any(e => e.Contains("yinelenen anahtar: a")) && bad.Errors.Any(e => e.Contains("kaynak (en) boş: b"))
              && bad.Errors.Any(e => e.Contains("yer tutucu farklı [tr]: c")) && bad.Warnings.Any(w => w.Contains("etiket farklı [tr]: d"))
              && bad.Warnings.Any(w => w.Contains("anahtar biçimi: E x")), "audit catches duplicate key, empty source, placeholder and tag mismatch, bad key shape");

        GameTextAudit.Report named = GameTextAudit.Run("key,note,en,tr\n" +
            "p,,Hello {player},Merhaba {name}\n" +
            "q,,\"{0:plural:1 round|{} rounds}\",\"{1:plural:1 round|{} round}\"\n" +
            "ok1,,Hello {player},Merhaba {player}\n" +
            "ok2,,\"{0:plural:1 round|{} rounds}\",{0} round\n" +
            "ok3,,{{literal}} {0},{{düz}} {0}\n" +
            "u,,Hi {0,Selam {0\n" +
            "s,,{a b:plural:x|y},{a b:plural:x|y}\n");
        Check(named.Errors.Any(e => e.Contains("yer tutucu farklı [tr]: p") && e.Contains("{player}") && e.Contains("{name}")),
            "named placeholders are compared: {player} ≠ {name}");
        Check(named.Errors.Any(e => e.Contains("yer tutucu farklı [tr]: q")), "plural selector is compared: {0:plural…} ≠ {1:plural…}");
        Check(!named.Errors.Any(e => e.Contains(": ok1") || e.Contains(": ok2") || e.Contains(": ok3")),
            "same named field, plural vs plain {0} on the same selector, and escaped {{ }} pass");
        Check(named.Errors.Any(e => e.Contains("süslü parantez [en]: u")), "unbalanced braces are an error");
        Check(named.Errors.Any(e => e.Contains("Smart metin hatalı [en]: s")), "Smart text is parsed with Unity's SmartFormat parser: " + string.Join(" | ", named.Errors.Where(e => e.Contains(": s"))));

        GameTextAudit.Report shape = GameTextAudit.Run("key,note,en,tr\na,,One,Bir,extra\nb,\"line\nbreak\",Two,İki\n,,orphan,yetim\n");
        Check(shape.Errors.Any(e => e.Contains("fazla sütun")) && shape.Errors.Any(e => e.Contains("satır sonu")) && shape.Errors.Any(e => e.Contains("anahtar boş")),
            "structure errors: extra column, line break inside a cell, filled row without key");
        Check(GameTextAudit.Run("key,note,en,tr\na,\"open note,One,Bir\nb,,Two,İki\n").Errors.Any(e => e.Contains("kapanmamış tırnak")), "unclosed quote is an error");
        Check(GameTextAudit.Run("key,note,en,tr\n").Errors.Any(e => e.Contains("hiç metin satırı yok"))
              && GameTextAudit.Run("key,note,en,tr\nmeta.speed,,1,1\n").Errors.Any(e => e.Contains("hiç metin satırı yok")),
            "a list with only the header (or only meta rows) is an error (it would have deleted every key)");
        GameTextAudit.Report nested = GameTextAudit.Run("key,note,en,tr\n" +
            "n1,,\"{0:plural:1 {player}|{} {player}s}\",\"{0:plural:1 {name}|{} {name}}\"\n" +
            "n2,,\"{0:plural:1 {player}|{} {player}s}\",\"{0:plural:{player} 1|{player} {}}\"\n");
        Check(nested.Errors.Any(e => e.Contains("yer tutucu farklı [tr]: n1")) && !nested.Errors.Any(e => e.Contains(": n2")),
            "named fields nested inside a plural are compared too ({player} ≠ {name}); same fields in another order pass");

        GameText.UseCsv("key,note,tr,en\ngold,,{0} altın,{0} gold\nbroken,,{0 bozuk,{0 broken\nmix,,<color=red>{1}</color> / {0},<color=red>{1}</color> / {0}\n");
        Check(!GameText.UsingUnityTables, "a test list overrides the Unity tables");
        GameText.Language = "tr";
        Check(GameText.Format("gold", 12) == "12 altın", "placeholder filled: {0} altın → 12 altın");
        Check(GameText.Format("broken", 1) == "{0 bozuk", "a broken pattern does not throw; the raw text is shown");
        GameText.Language = "en";
        Check(GameText.Format("gold", 3) == "3 gold", "same key, English pattern");
        GameText.Language = GameText.PseudoLanguage;
        string pseudo = GameText.Get("mix");
        Check(pseudo.StartsWith("[") && pseudo.EndsWith("]") && pseudo.Contains("<color=red>{1}</color>") && pseudo.Contains("{0}"),
            $"pseudo language keeps placeholders and rich-text tags ({pseudo})");
        string longer = GameText.Pseudo("Round'u başlat");
        Check(longer.Length >= "Round'u başlat".Length * 1.3f && longer.Contains("å") && !longer.Contains("Round"), $"pseudo text is accented and ~35 % longer ({longer})");
        GameText.Language = "tr";
        GameText.UseCsv(null);
    }

    // ---- kapsam (5 Kasım, taşıma adım 3–4): koddaki anahtarlar, türetilen anahtarlar, oyuna giren veri adları, İngilizce sütun,
    // ekranların İngilizce ve Türkçe metni (sayı biçimi dahil) ----
    static readonly System.Text.RegularExpressions.Regex CodeKey =
        new(@"""((?:ui|data|stat|rarity|resource)\.[a-z0-9_]+(?:\.[a-z0-9_]+)*)""");

    static void CoverageChecks()
    {
        Check(GameText.UsingUnityTables, "coverage is checked against the Unity tables (setup done)");
        // 1) Koddaki anahtar yazıları ("ui.start.title" …) listede olmalı: yazım hatası anahtar olarak ekranda görünürdü.
        var missing = new SortedSet<string>();
        int literals = 0;
        foreach (string file in Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts"), "*.cs", SearchOption.AllDirectories))
            foreach (System.Text.RegularExpressions.Match m in CodeKey.Matches(File.ReadAllText(file)))
            {
                literals++;
                if (!GameText.Has(m.Groups[1].Value)) missing.Add($"{Path.GetFileName(file)}: {m.Groups[1].Value}");
            }
        Check(literals > 100 && missing.Count == 0, $"every key written in the game code is in the list ({literals} uses): " + string.Join(", ", missing));

        // 2) Koddan türetilen anahtarlar: enum adları, ".detail" / ".boss" ekleri.
        var derived = new List<string>();
        foreach (StatType s in Enum.GetValues(typeof(StatType))) derived.Add(TileBuffText.StatKey(s));
        foreach (string k in new[] { "rarity.common", "rarity.uncommon", "rarity.rare", "rarity.epic", "rarity.legendary", "resource.gold", "resource.iron", "resource.stone" })
            derived.Add(k);
        foreach (string k in new[] { "ui.quota.first", "ui.quota.passed", "ui.quota.next", "ui.quota.name" }) derived.Add(k + ".boss");
        foreach (string k in new[] { "direct_damage", "harvest_resources", "harvest_score" }) derived.Add("ui.start.effect." + k + ".detail");
        foreach (string f in new[] { "direct", "rare", "behavior" })
            foreach (string s in new[] { "card", "short", "long" }) derived.Add($"ui.reward.factor.{f}.{s}");
        derived.Add("ui.reward.panel.tail.lower"); derived.Add("ui.reward.panel.last_tail.lower");
        List<string> absent = derived.Where(k => !GameText.Has(k)).ToList();
        Check(absent.Count == 0, $"keys built in code (stat / rarity / resource names, .detail and .boss forms) are in the list ({derived.Count}): " + string.Join(", ", absent));

        // 3) Oyuna giren veri adları (çiftçi, tırpan, görev, yetenek düğümü, tile, boss): her birinin satırı var.
        HashSet<string> shipped = HardcodedTextScanner.ShippedAssets();
        var dataMissing = new SortedSet<string>();
        int dataChecked = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/ScriptableObjects" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!shipped.Contains(path)) continue;
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            foreach (string field in new[] { "displayName", "playstyle", "description", "nodeName", "modifierName", "plantName", "victoryTitle", "effectLabel", "inverseLabel" })
            {
                string key = asset != null ? HardcodedTextScanner.BoundDataKey(asset, field) : null;
                if (key == null || key == HardcodedTextScanner.NotShown) continue;
                var prop = new SerializedObject(asset).FindProperty(field);
                if (prop == null || string.IsNullOrWhiteSpace(prop.stringValue)) continue;
                dataChecked++;
                if (!GameText.Has(key)) dataMissing.Add($"{Path.GetFileName(path)}.{field} → {key}");
            }
        }
        var rules = Resources.Load<ResonanceRulesSO>("ResonanceRules");
        var so = rules != null ? new SerializedObject(rules) : null;
        for (SerializedProperty p = so?.GetIterator(); p != null && p.Next(true);)
            if (p.propertyType == SerializedPropertyType.String && p.name == "resonanceName" && !string.IsNullOrWhiteSpace(p.stringValue))
            {
                dataChecked++;
                if (!GameText.Has(ResonanceRule.NameKey(p.stringValue))) dataMissing.Add("resonance " + p.stringValue);
            }
        // Boss ödülleri: ad, etiket, ters etiket, not (yazısının özetiyle) ve havuzdaki aşama adları ile aşama notları.
        foreach (string guid in AssetDatabase.FindAssets("t:BossRewardSO t:BossRewardPoolSO", new[] { "Assets/ScriptableObjects" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!shipped.Contains(path)) continue;
            void Need(string key, string what) { dataChecked++; if (key == null || !GameText.Has(key)) dataMissing.Add($"{Path.GetFileName(path)} {what} → {key}"); }
            if (AssetDatabase.LoadAssetAtPath<BossRewardSO>(path) is BossRewardSO r)
            {
                Need(r.NameKey, "name");
                if (!string.IsNullOrWhiteSpace(r.effectLabel)) Need(r.EffectKey, "effect");
                if (!string.IsNullOrWhiteSpace(r.inverseLabel)) Need(r.InverseKey, "inverse");
                if (!string.IsNullOrWhiteSpace(r.note)) Need(r.NoteKey(r.note), "note");
            }
            else if (AssetDatabase.LoadAssetAtPath<BossRewardPoolSO>(path) is BossRewardPoolSO pool && pool.IsStaged)
                foreach (BossRewardStage stage in pool.stages.Append(pool.endlessStage))
                {
                    if (stage == null || string.IsNullOrWhiteSpace(stage.displayName)) continue;
                    Need(stage.NameKey, "stage " + stage.id);
                    foreach (BossRewardStageEntry entry in stage.rewards)
                        if (entry.reward != null && !string.IsNullOrWhiteSpace(entry.note)) Need(entry.reward.NoteKey(entry.note), "stage note " + entry.reward.id);
                }
        }
        NoRuleBossSO fallback = NoRuleBossSO.Create();
        if (!GameText.Has(fallback.NameKey)) dataMissing.Add(fallback.NameKey);
        Object.DestroyImmediate(fallback);
        Check(dataChecked > 50 && dataMissing.Count == 0, $"shipped data names have rows ({dataChecked} fields): " + string.Join(", ", dataMissing));

        // 4) İngilizce (kaynak) sütunda Türkçeye özgü harf yok: çevrilmemiş hücre kalmasın.
        var csvRows = GameText.Parse(GameText.SourceCsv);
        int en = csvRows[0].FindIndex(h => h.Trim() == "en");
        var turkish = csvRows.Skip(1).Where(r => r.Count > en && !r[0].StartsWith("meta.") && r[en].IndexOfAny("çğıöşüÇĞİÖŞÜ".ToCharArray()) >= 0)
            .Select(r => r[0]).ToList();
        Check(turkish.Count == 0, "the English column has no Turkish letters: " + string.Join(", ", turkish));

        // 5) Ekran metinleri iki dilde (düzenleme modu, açık dil): başlangıç kartı, stat adı, yetenek düğümü adı, sayı biçimi.
        var tuccar = AssetDatabase.LoadAssetAtPath<StartOptionSO>("Assets/ScriptableObjects/StartOptions/Farmers/Tuccar.asset");
        var node = AssetDatabase.FindAssets("t:SkillNodeSO", new[] { "Assets/ScriptableObjects/Skill Tree Upgrades/FinalSkillTree" })
            .Select(g => AssetDatabase.LoadAssetAtPath<SkillNodeSO>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(n => n != null && n.nodeName.StartsWith("Keskin Başlangıç") && SkillNodeSO.OrderSuffixStart(n.nodeName) > 0);
        Check(tuccar != null && node != null, "sample data found (Tüccar, a numbered Keskin Başlangıç node)");
        string suffix = node.nodeName.Substring(SkillNodeSO.OrderSuffixStart(node.nodeName));
        GameText.Language = "en";
        string enCard = StartOptionText.Effects(tuccar), enName = tuccar.DisplayName, enNode = node.DisplayName, enStat = TileBuffText.Name(StatType.CritChance);
        string enQuota = HarvestQuota.Format(12345), enRarity = TileBuffText.RarityName(TileRarity.Epic);
        GameText.Language = "tr";
        string trCard = StartOptionText.Effects(tuccar), trName = tuccar.DisplayName, trNode = node.DisplayName, trQuota = HarvestQuota.Format(12345);
        notes.Add($"INFO en: {enName} | {enCard.Replace('\n', '/')} | {enNode} | {enStat} | {enQuota}");
        Check(enName == "Merchant" && enCard.Contains("Harvest resources ×1.25") && enCard.Contains("Harvest Score ×0.90") && !enCard.Any(c => "çğıöşüÇĞİÖŞÜ".IndexOf(c) >= 0),
            "English start card: name and effects in English, decimal point (Harvest resources ×1.25)");
        Check(trName == "Tüccar" && trCard.Contains("Hasat kaynağı ×1,25") && trCard.Contains("Hasat skoru ×0,90"), "Turkish start card unchanged (Hasat kaynağı ×1,25)");
        Check(enNode == "Sharp Start" + suffix && trNode == node.nodeName, $"skill node name: the stem is translated, the order number stays ({enNode} / {trNode})");
        Check(enStat == "Crit chance" && enRarity == "Epic", "stat and rarity names come from the list");
        Check(enQuota == "12,345" && trQuota == "12.345", $"thousands separator follows the language (en {enQuota}, tr {trQuota})");
    }

    // ---- sabit yazı taraması (çeviri listesi taslağı) ----
    static void ScannerChecks()
    {
        var items = HardcodedTextScanner.Scan();
        HardcodedTextScanner.WriteCsv(items, "Logs/SabitYazilar.csv");
        notes.Add("INFO hard-coded texts: " + items.Count + " (" + HardcodedTextScanner.Summary(items) + ")");
        Check(items.Count > 0 && File.Exists("Logs/SabitYazilar.csv"), "scanner lists hard-coded player texts into a CSV");
        Check(items.All(i => HardcodedTextScanner.IsText(i.Text)) && !HardcodedTextScanner.IsText("12") && !HardcodedTextScanner.IsText(" + ")
              && !HardcodedTextScanner.IsText("") && !HardcodedTextScanner.IsText("B8322A") && HardcodedTextScanner.IsText("START ROUND"),
            "numbers, symbols, colour codes and icon glyphs are not counted as text");
        Check(items.All(i => !i.Location.Contains("TextMesh Pro")), "TextMesh Pro samples are skipped");
        HashSet<string> shipped = HardcodedTextScanner.ShippedAssets();
        Check(shipped.Contains("Assets/ScriptableObjects/RunProfiles/Run50_GecOyunV1.asset") && !shipped.Contains("Assets/ScriptableObjects/RunProfiles/Run50_DengeV1.asset"),
            "shipped data = what the build scenes and Resources use (active profile in, old balance version out)");
        Check(items.Where(i => i.Location.Contains("RunProfiles/Run50_DengeV1.asset")).All(i => i.Category == "kullanilmiyor")
              && items.Where(i => i.Location.Contains("RunProfiles/Run50_GecOyunV1.asset")).All(i => i.Category == "veri"),
            "texts of unused data files are listed as 'kullanilmiyor' (not to be translated)");
        notes.Add("INFO to translate (sahne+prefab+veri+kod): " + items.Count(i => i.Category is "sahne" or "prefab" or "veri" or "kod")
                  + ", distinct texts: " + items.Where(i => i.Category is "sahne" or "prefab" or "veri" or "kod").Select(i => i.Text).Distinct().Count());
    }

    // ---- play mode: Unity tabloları, LocalizedText, CSV kaydedilince yenilenme ----
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            switch (step++)
            {
                case 0: next = EditorApplication.timeSinceStartup + 1; return;
                case 1: UnityPathChecks(); break;
                case 2: LabelChecks(); break;
                case 3: HotReloadChecks(); break;
                default: Finish(null); return;
            }
            next = EditorApplication.timeSinceStartup + .2;
        }
        catch (Exception ex) { Finish(ex); }
    }

    static void UnityPathChecks()
    {
        Check(GameText.UsingUnityTables, "in play the game reads the Unity Localization tables");
        Check(GameText.StartLanguage() == "tr" && LocalizationSettings.SelectedLocale?.Identifier.Code == "tr" && GameText.Language == "tr",
            "automated tests always start in Turkish (players: their choice → computer language → English)");
        Check(GameText.NativeName("tr") == "Türkçe" && GameText.NativeName("en") == "English", $"languages are shown in their own language ({GameText.NativeName("tr")}, {GameText.NativeName("en")})");
        Check(GameText.StartLanguageFor("tr", SystemLanguage.English) == "tr" && GameText.StartLanguageFor("", SystemLanguage.Turkish) == "tr"
              && GameText.StartLanguageFor("", SystemLanguage.English) == "en" && GameText.StartLanguageFor("", SystemLanguage.German) == "en"
              && GameText.StartLanguageFor(null, SystemLanguage.Unknown) == "en",
            "player start language: chosen in settings → computer language if translated → English");
        Check(GameText.Get("ui.skip") == "Atla" && GameText.Has("tut.hit.0") && !GameText.Has("meta.nothing") && Mathf.Approximately(GameText.SpeedScale, 1f),
            "text, key lookup and the speed setting come from the table");
        Check(GameText.Languages.SequenceEqual(new[] { "en", "tr" }), "available languages: English source first, then Turkish");
        GameText.Language = "en";
        Check(LocalizationSettings.SelectedLocale.Identifier.Code == "en" && GameText.Get("ui.skip") == "Skip"
              && GameText.Get("tut.hit.0").StartsWith("Don't"), "language change switches the Unity locale (English lines)");
        GameText.Language = "de";
        Check(GameText.Language == "de" && LocalizationSettings.SelectedLocale.Identifier.Code == "en" && GameText.Get("ui.skip") == "Skip",
            "a language without a table uses the English source");
        GameText.Language = GameText.PseudoLanguage;
        Check(GameText.Get("ui.skip").StartsWith("[Şkîp"), $"pseudo language is built from the English source ({GameText.Get("ui.skip")})");
        GameText.Language = "tr";
        Check(LocalizationSettings.SelectedLocale.Identifier.Code == "tr" && GameText.Get("ui.skip") == "Atla", "back to Turkish");
    }

    static GameObject canvasObject;

    static TMP_Text MakeLabel(string name, string placeholder)
    {
        if (canvasObject == null) canvasObject = new GameObject("Loc Test Canvas", typeof(Canvas));
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(canvasObject.transform, false);
        var label = go.GetComponent<TMP_Text>();
        label.text = placeholder;
        return label;
    }

    static void LabelChecks()
    {
        Listeners();
        TMP_Text skip = MakeLabel("Skip", "SKIP (placeholder)");
        skip.gameObject.AddComponent<LocalizedText>().SetKey("ui.skip");
        Check(skip.text == "Atla", "LocalizedText pulls its text from the table by key (placeholder replaced)");
        GameText.Language = "en";
        Check(skip.text == "Skip", "language change updates bound labels at once");
        GameText.Language = GameText.PseudoLanguage;
        Check(skip.text.StartsWith("[") && skip.text.Contains("Şkîp"), $"pseudo language shows on bound labels ({skip.text})");
        GameText.Language = "tr";

        GameText.UseCsv("key,note,tr,en\nui.skip,,Geç,Skip\nui.gold,,{0} altın,{0} gold\n");
        Check(skip.text == "Geç", "a changed list updates bound labels");
        TMP_Text gold = MakeLabel("Gold", "0 GOLD");
        gold.gameObject.AddComponent<LocalizedText>().SetKey("ui.gold", 25);
        Check(gold.text == "25 altın", "LocalizedText with a value: {0} altın → 25 altın");
        GameText.Language = "en";
        Check(gold.text == "25 gold", "values are kept across a language change");
        skip.gameObject.SetActive(false);
        GameText.Language = "tr";
        Check(skip.text == "Skip", "a disabled label does not listen (no work while hidden)");
        skip.gameObject.SetActive(true);
        Check(skip.text == "Geç", "re-enabled label catches up with the current language");
        Check(typeof(LocalizedText).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) == null
              && typeof(LocalizedText).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) == null,
            "LocalizedText has no per-frame work");
        GameText.UseCsv(null);
        Object.Destroy(canvasObject);
        canvasObject = null;
    }

    static void HotReloadChecks()
    {
        Check(Listeners() == baseline, $"destroyed labels unsubscribe (listeners {Listeners()})");
        Check(GameText.UsingUnityTables, "back on the Unity tables after the test list");
        TMP_Text hot = MakeLabel("Hot", "?");
        hot.gameObject.AddComponent<LocalizedText>().SetKey("test.hot");
        TMP_Text rounds = MakeLabel("Rounds", "?");
        var roundsLoc = rounds.gameObject.AddComponent<LocalizedText>();
        string original = File.ReadAllText(GameText.MasterCsvPath);
        try
        {
            File.WriteAllText(GameText.MasterCsvPath, original.TrimEnd('\r', '\n')
                + "\ntest.hot,,Hot line,Sıcak satır\ntest.plural,,\"{0:plural:1 round|{} rounds}\",{0} round\ntest.untr,,Only English,\n",
                new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(GameText.MasterCsvPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Check(GameText.UsingUnityTables && Collection.SharedData.Contains("test.hot"), "saving the list re-imports it into the Unity table");
            Check(hot.text == "Sıcak satır", $"bound labels change by themselves after the save ({hot.text})");
            Check(GameText.Get("test.untr") == "Only English", "a line not yet translated to Turkish shows the English source (never a key or a 'missing' message)");
            roundsLoc.SetKey("test.plural", 3);
            Check(rounds.text == "3 round", "Turkish: 3 round");
            GameText.Language = "en";
            string one = GameText.Format("test.plural", 1);
            Check(one == "1 round" && rounds.text == "3 rounds", $"English plural rule from the table: 1 round / 3 rounds ({one} / {rounds.text})");
            GameText.Language = "tr";
        }
        finally
        {
            File.WriteAllText(GameText.MasterCsvPath, original, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(GameText.MasterCsvPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
        Check(!GameText.Has("test.hot") && !Collection.SharedData.Contains("test.plural"), "restored list is re-imported too");
        Object.Destroy(canvasObject);
        canvasObject = null;
    }

    static int baseline = -1;

    static int Listeners()
    {
        var field = typeof(GameText).GetField("Changed", BindingFlags.Static | BindingFlags.NonPublic);
        int n = (field?.GetValue(null) as Delegate)?.GetInvocationList().Length ?? 0;
        if (baseline < 0) baseline = n;
        return n;
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        if (ex != null) notes.Add("FAIL " + ex.Message);
        Directory.CreateDirectory("Logs");
        if (EditorApplication.isPlaying && File.Exists(EditNotes)) notes.InsertRange(0, File.ReadAllLines(EditNotes));
        notes.Add($"SUMMARY {notes.Count(n => n.StartsWith("PASS"))} PASS, {notes.Count(n => n.StartsWith("FAIL"))} FAIL");
        File.WriteAllLines("Logs/LocalizationVerification.txt", notes);
        foreach (string n in notes) Debug.Log("[Loc] " + n);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        EditorApplication.Exit(ex == null ? 0 : 1);
    }
}
