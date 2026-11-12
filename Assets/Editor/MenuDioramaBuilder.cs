using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Ana menünün canlı sahnesi — "Oluştur / Güncelle" aracı (11 Kasım 2026, kullanıcı: "sahneyi biz yapalım, oyunun uzay arka
// planını kullanalım"; AGENTS.md editörde düzenleme kuralı). Tools > Menü > Canlı Sahne.
// - Prefab yoksa kurar: Assets/Prefabs/Menu/MenuDiorama.prefab — oyunun kendi karoları (GameScene'deki GroundCell'in mesh'i ve
//   malzemeleri; içte 5 × 5 açık, dışta bir halka kapalı), ortada 3 × 3 "Grass Planter 1x1" ve her birinde büyümüş bir
//   bitki (Assets/Prefabs/Plants). Yalnız görsel parçalar kalır (oyun betikleri sökülür: menüde yönetici yok).
//   Prefab varsa DOKUNMAZ: sonrasında görünüm prefab'da elle düzenlenir.
// - MenuScene'de eksik olanları ekler: prefab örneği, "Menu Camera" (perspektif, oyunun SpaceBackground'u ile), menünün eski
//   bahçe resmini kapatır, butonları sola yaslı yerleşime alır (MainMenuUI.sceneLayout). Bunları yalnız İLK kurulumda yapar;
//   sonradan sahnede yapılan düzenlemeler korunur. Tekrar çalıştırmak hiçbir şeyi çoğaltmaz. Hepsi Undo'lu.
// Hareket çalışma anında MenuDioramaMotion'dadır (yalnız süzülme / sallanma / kamera kayması).
public static class MenuDioramaBuilder
{
    public const string PrefabPath = "Assets/Prefabs/Menu/MenuDiorama.prefab";
    public const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
    const string GameScenePath = "Assets/Scenes/GameScene.unity";
    const string PlanterPath = "Assets/Prefabs/Grass Planter 1x1.prefab";
    public const string RootName = "Menu Diorama", CameraName = "Menu Camera";
    const float Cell = 2f;
    // 3 × 3 saksı, sıra sıra (arka sol → ön sağ): uzun mısırlar arkada, alçak marullar önde.
    static readonly string[] PlantNames =
        { "Plant_Corn", "Plant_Corn", "Plant_Tomato", "Plant_Pepper", "Plant_Beetroot", "Plant_Corn", "Plant_Lettuce", "Plant_Lettuce", "Plant_Beetroot" };
    // Kamera: alçak kahraman açısı (yükselim 19°, yön 55°), ada ekranın sağında (kadrajın %67'si), solu menüye boş.
    public const float CamElevation = 20f, CamAzimuth = 52f, CamDistance = 35f, CamFov = 28f, IslandScreenX = .69f, CamTargetY = -1.6f;   // hedef ada üstünün biraz altı: altındaki toprak / kaya da kadrajda

    [MenuItem("Tools/Menü/Canlı Sahne: Oluştur - Güncelle")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log(Build());
    }

    // Kamerayı varsayılan kadraja alır (yalnız bu komutla; araç var olan kamerayı kendiliğinden değiştirmez).
    [MenuItem("Tools/Menü/Canlı Sahne: Kamerayı Varsayılana Al")]
    public static void ResetCameraFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log(ResetCamera());
    }

    public static string ResetCamera()
    {
        Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        GameObject camObject = scene.GetRootGameObjects().FirstOrDefault(g => g.name == CameraName);
        GameObject diorama = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (camObject == null || diorama == null) return "kamera ya da sahne yok: önce Oluştur - Güncelle";
        Undo.RecordObject(camObject.transform, "Menü kamerası varsayılan");
        var cam = camObject.GetComponent<Camera>(); cam.fieldOfView = CamFov;
        PlaceCamera(cam, diorama.transform.position);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return "menü kamerası varsayılan kadraja alındı";
    }

    // Komut satırı (editör kapalıyken): Unity -batchmode -projectPath . -executeMethod MenuDioramaBuilder.RunBatch
    public static void RunBatch()
    {
        try { Debug.Log(Build()); if (System.Environment.GetCommandLineArgs().Contains("-menuPolish")) Debug.Log(ApplyPolish()); if (System.Environment.GetCommandLineArgs().Contains("-toonBoost")) Debug.Log(ApplyToonBoost()); if (System.Environment.GetCommandLineArgs().Contains("-resetMenuCamera")) Debug.Log(ResetCamera()); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
    }

    // Döner: ne yapıldığının kısa özeti.
    public static string Build()
    {
        var log = new List<string>();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { prefab = CreatePrefab(); log.Add("prefab kuruldu"); }
        else log.Add("prefab vardı (dokunulmadı)");
        log.Add(EnsureUnderside());
        log.Add(EnsureExtras());
        log.Add(EnsureToonLook());

        Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        GameObject diorama = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (diorama == null)
        {
            diorama = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            diorama.name = RootName;
            Undo.RegisterCreatedObjectUndo(diorama, "Menü sahnesi");
            log.Add("sahneye eklendi");
        }

        GameObject camObject = scene.GetRootGameObjects().FirstOrDefault(g => g.name == CameraName);
        bool firstSetup = camObject == null;
        if (firstSetup)
        {
            camObject = new GameObject(CameraName, typeof(Camera), typeof(AudioListener));
            SceneManager.MoveGameObjectToScene(camObject, scene);
            Undo.RegisterCreatedObjectUndo(camObject, "Menü kamerası");
            camObject.tag = "MainCamera";
            var cam = camObject.GetComponent<Camera>();
            cam.orthographic = false; cam.fieldOfView = CamFov; cam.nearClipPlane = .3f; cam.farClipPlane = 5000f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.043f, .043f, .118f);   // SpaceBackground'un derin rengi (editörde de uzay laciverti)
            cam.depth = 1f;   // sahnedeki eski menü kamerasından (derinlik 0) sonra çizilir
            PlaceCamera(cam, diorama.transform.position);
            CalmSpace(camObject.AddComponent<SpaceBackground>());
            // Sahnede başka AudioListener varsa ikincisini ekleme.
            if (Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 1)
                Object.DestroyImmediate(camObject.GetComponent<AudioListener>());
            var motion = diorama.GetComponent<MenuDioramaMotion>();
            if (motion != null)
            {
                var so = new SerializedObject(motion);
                so.FindProperty("menuCamera").objectReferenceValue = cam;
                so.ApplyModifiedProperties();
            }
            log.Add("kamera kuruldu");
            log.Add(CopyGameLighting(scene));

            // Menünün eski bahçe resmi kapanır (yerini canlı sahne alır); silinmez, geri açılabilir.
            foreach (Image image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (image.sprite != null && image.sprite.name.StartsWith("MainMenu_Garden"))
                {
                    Undo.RecordObject(image.gameObject, "Menü eski arka plan");
                    image.gameObject.SetActive(false);
                    log.Add($"eski arka plan kapatıldı ({image.name})");
                }

            // Butonlar sola yaslı (sahne sağda). Konum / boyut artık sahnede; MainMenuUI ezmez.
            MainMenuUI menu = Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
            if (menu != null)
            {
                var so = new SerializedObject(menu);
                ApplyButtonLayout(so);
                so.FindProperty("sceneLayout").boolValue = true;
                so.ApplyModifiedProperties();
                log.Add("butonlar sola yaslandı");
            }
        }
        else log.Add("kamera vardı (dokunulmadı)");
        log.Add(EnsureMenuUi());

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Menü canlı sahne: " + string.Join(", ", log);
    }

    // ------------------------------------------------------------------ başlık tabelası + arayüz hareketi
    // Kullanıcı: "ekran çok boş; butonlar oynasın; butonların üstünde solda yamuk bir tabelada ERIGONE yazsın; bizim fontumuzla;
    // butonlar üstüne gelince yamulabilir (özellikle Oyna), nefes alabilir". Tabela: kitin lacivert şeridi (header_navy), hafif
    // yamuk, altın Bungee başlık (çeviri anahtarı ui.menu.title). Butonlara sahnede ComicHoverMotion (eğilme + nefes) eklenir.
    // Yönetilen parçalar: yoksa eklenir, varsa dokunulmaz. Hareket MenuUiMotion + ComicHoverMotion'da.
    public const string SignName = "Menu Title Sign", TitleKey = "ui.menu.title";
    // Yerleşim (kullanıcı: "butonlar büyüyebilir, ERIGONE üste uzayabilir, büyüyebilir"). İlk kurulumda ve "Cila" komutunda yazılır.
    static readonly Vector2 SignSize = new(820f, 196f), SignPosition = new(-530f, 522f), TitleOffsetMin = new(40f, 30f), TitleOffsetMax = new(-58f, -16f);
    const float TitleSize = 136f, BreathePeriod = 3.6f;
    static readonly (string field, Vector2 position, Vector2 size)[] ButtonLayout =
        { ("playButton", new(-600f, 128f), new(640f, 146f)), ("optionsButton", new(-618f, -26f), new(560f, 122f)), ("quitButton", new(-618f, -160f), new(560f, 122f)) };

    static void ApplyButtonLayout(SerializedObject menu)
    {
        foreach (var b in ButtonLayout) LayoutButton(menu, b.field, b.position, b.size);
    }

    // Uzay arka planı lo-fi sakinliğinde (kullanıcı: "arka plan sadece chill bir lofi havası versin"): kayan yıldız seyrek,
    // yıldız ve bulutsu biraz kısık, paralaks yumuşak. Değerler sahnede, Inspector'dan değişir.
    static void CalmSpace(SpaceBackground space)
    {
        if (space == null) return;
        var so = new SerializedObject(space);
        so.FindProperty("shootingStarRate").floatValue = .06f;
        so.FindProperty("starIntensity").floatValue = .75f;
        so.FindProperty("nebulaIntensity").floatValue = .28f;
        so.FindProperty("parallax").floatValue = .22f;
        so.ApplyModifiedProperties();
    }

    // Tek seferlik "cila" (11 Kasım, kullanıcı isteği): var olan sahnede butonları büyütür, tabelayı büyütüp yukarı taşır, uzayı
    // ve hareketleri lo-fi sakinliğine alır. Yalnız bu komutla çalışır; araç kendiliğinden var olan değerlere dokunmaz.
    [MenuItem("Tools/Menü/Canlı Sahne: Lo-fi Cila Uygula")]
    public static void PolishFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log(ApplyPolish());
    }

    public static string ApplyPolish()
    {
        Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        var log = new List<string>();
        MainMenuUI menu = Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
        if (menu != null)
        {
            var menuSo = new SerializedObject(menu);
            ApplyButtonLayout(menuSo);
            foreach (var f in new[] { "playButton", "optionsButton", "quitButton" })
                if (menuSo.FindProperty(f).objectReferenceValue is Button b && b.TryGetComponent(out ComicHoverMotion h)) { Undo.RecordObject(h, "Menü nefes"); h.breathePeriod = BreathePeriod; }
            var panel = menuSo.FindProperty("mainMenuPanel").objectReferenceValue as GameObject;
            var sign = panel != null ? panel.transform.Find(SignName) as RectTransform : null;
            if (sign != null)
            {
                Undo.RecordObject(sign, "Menü tabelası");
                sign.sizeDelta = SignSize; sign.anchoredPosition = SignPosition;
                var plate = sign.GetComponent<Image>();
                if (plate != null && plate.sprite != null) { Undo.RecordObject(plate, "Menü tabelası"); plate.pixelsPerUnitMultiplier = plate.sprite.rect.height / SignSize.y; }
                var title = sign.Find("Title")?.GetComponent<TextMeshProUGUI>();
                if (title != null)
                {
                    Undo.RecordObject(title, "Menü başlığı"); Undo.RecordObject(title.rectTransform, "Menü başlığı");
                    title.rectTransform.offsetMin = TitleOffsetMin; title.rectTransform.offsetMax = TitleOffsetMax;
                    title.fontSize = TitleSize; title.fontSizeMax = TitleSize; title.fontSizeMin = 48f;
                }
                log.Add("butonlar ve tabela büyütüldü");
            }
            var uiMotion = panel != null ? panel.GetComponent<MenuUiMotion>() : null;
            if (uiMotion != null)
            {
                var so = new SerializedObject(uiMotion);
                so.FindProperty("buttonBob").floatValue = 4f; so.FindProperty("buttonPeriod").floatValue = 4.2f;
                so.FindProperty("signSwing").floatValue = 1.1f; so.FindProperty("signPeriod").floatValue = 6.5f;
                so.ApplyModifiedProperties();
            }
        }
        GameObject camObject = scene.GetRootGameObjects().FirstOrDefault(g => g.name == CameraName);
        if (camObject != null) { CalmSpace(camObject.GetComponent<SpaceBackground>()); log.Add("uzay lo-fi sakinliğinde"); }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);

        // Sahne hareketi prefab'da: lo-fi hızları; süzülen taşlar yanlara ve küçüğe.
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            for (int i = 0; i < FloaterSpots.Length; i++)
            {
                Transform rock = contents.transform.Find($"Platform/{UndersideName}/Floating Rock {i + 1}");
                if (rock == null) continue;
                rock.localPosition = FloaterSpots[i]; rock.localScale = Vector3.one * FloaterScales[i];
            }
            var motion = contents.GetComponent<MenuDioramaMotion>();
            if (motion != null)
            {
                var so = new SerializedObject(motion);
                so.FindProperty("bobHeight").floatValue = .1f; so.FindProperty("bobPeriod").floatValue = 8f;
                so.FindProperty("swayDegrees").floatValue = 2f; so.FindProperty("swayPeriod").floatValue = 4.6f;
                so.FindProperty("floaterPeriod").floatValue = 6.5f; so.FindProperty("isletPeriod").floatValue = 11f;
                so.FindProperty("cameraDrift").floatValue = .3f; so.FindProperty("cameraPeriod").floatValue = 24f;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                log.Add("sahne hareketi yavaşlatıldı");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        return "Menü lo-fi cila: " + string.Join(", ", log);
    }

    static string EnsureMenuUi()
    {
        MainMenuUI menu = Object.FindAnyObjectByType<MainMenuUI>(FindObjectsInactive.Include);
        if (menu == null) return "MainMenuUI yok, tabela eklenmedi";
        var menuSo = new SerializedObject(menu);
        var panel = menuSo.FindProperty("mainMenuPanel").objectReferenceValue as GameObject;
        if (panel == null) return "ana menü paneli yok, tabela eklenmedi";
        var log = new List<string>();
        var sign = panel.transform.Find(SignName) as RectTransform;
        if (sign == null)
        {
            var go = new GameObject(SignName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = panel.layer;
            Undo.RegisterCreatedObjectUndo(go, "Menü tabelası");
            sign = (RectTransform)go.transform;
            sign.SetParent(panel.transform, false);
            sign.anchorMin = sign.anchorMax = new Vector2(.5f, .5f);
            sign.pivot = new Vector2(.5f, 1f);   // tepesinden asılı gibi sallanır
            sign.sizeDelta = SignSize;
            sign.anchoredPosition = SignPosition;
            sign.localRotation = Quaternion.Euler(0f, 0f, -4f);
            var plate = go.GetComponent<Image>();
            plate.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/ComicPack/header_navy.png");
            plate.type = Image.Type.Sliced;
            if (plate.sprite != null) plate.pixelsPerUnitMultiplier = plate.sprite.rect.height / sign.sizeDelta.y;
            plate.raycastTarget = false;

            var textGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.layer = panel.layer;
            var rect = (RectTransform)textGo.transform;
            rect.SetParent(sign, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = TitleOffsetMin; rect.offsetMax = TitleOffsetMax;   // şeridin alt 3B kenarı ve sağ çentiği dışında
            var title = textGo.GetComponent<TextMeshProUGUI>();
            var theme = Resources.Load<ComicUITheme>("ComicUITheme");
            if (theme != null && theme.displayFont != null)
            {
                title.font = theme.displayFont;
                title.fontSharedMaterial = theme.displayOutline != null ? theme.displayOutline : theme.displayFont.material;
            }
            title.text = "ERIGONE";
            title.fontSize = TitleSize; title.enableAutoSizing = true; title.fontSizeMin = 48f; title.fontSizeMax = TitleSize;
            title.characterSpacing = 6f;
            title.alignment = TextAlignmentOptions.Center;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Overflow;
            title.color = new Color32(255, 206, 84, 255);
            title.raycastTarget = false;
            var localized = textGo.AddComponent<LocalizedText>();
            var lso = new SerializedObject(localized);
            lso.FindProperty("key").stringValue = TitleKey;
            lso.ApplyModifiedPropertiesWithoutUndo();
            log.Add("başlık tabelası eklendi");
        }

        // Butonlar: üstüne gelince eğilir (Oyna en çok), boşta nefes alır (her biri ayrı fazda).
        (string field, float scale, float tilt, float breathe, float phase)[] hover =
            { ("playButton", 1.08f, 4.5f, .022f, 0f), ("optionsButton", 1.05f, -3f, .014f, 2.1f), ("quitButton", 1.05f, 3f, .014f, 4.2f) };
        var buttons = new List<RectTransform>();
        int added = 0;
        foreach (var h in hover)
        {
            var b = menuSo.FindProperty(h.field).objectReferenceValue as Button;
            if (b == null) continue;
            buttons.Add((RectTransform)b.transform);
            if (b.GetComponent<ComicHoverMotion>() != null) continue;
            var m = Undo.AddComponent<ComicHoverMotion>(b.gameObject);
            m.hoverScale = h.scale; m.tilt = h.tilt; m.breathe = h.breathe; m.breathePhase = h.phase; m.breathePeriod = BreathePeriod;
            added++;
        }
        if (added > 0) log.Add($"{added} butona eğilme + nefes eklendi");

        if (panel.GetComponent<MenuUiMotion>() == null)
        {
            var motion = Undo.AddComponent<MenuUiMotion>(panel);
            var so = new SerializedObject(motion);
            so.FindProperty("sign").objectReferenceValue = sign;
            var arr = so.FindProperty("buttons"); arr.arraySize = buttons.Count;
            for (int i = 0; i < buttons.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            so.ApplyModifiedProperties();
            log.Add("menü hareketi eklendi");
        }
        return log.Count == 0 ? "tabela ve hareket vardı (dokunulmadı)" : string.Join(", ", log);
    }

    // ------------------------------------------------------------------ ekranı dolduranlar: uzak adacıklar + polen
    // Kullanıcı: "polish'ler ile ekranı biraz doldurmak lazım, çok boş hissettiriyor". Süs plakası değil, oyunun dünyası:
    // arkada uzakta süzülen iki küçük ada (aynı karolar, bir saksı, bir bitki, küçültülmüş alt kütle) ve adanın çevresinde
    // yavaşça yükselen polen ışıkları. Yönetilen parçalar: yoksa eklenir, varsa dokunulmaz.
    public const string IsletsName = "Far Islets", PollenName = "Pollen";

    static string EnsureExtras()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        var log = new List<string>();
        try
        {
            Transform platform = contents.transform.Find("Platform");
            Transform underside = platform != null ? platform.Find(UndersideName) : null;
            if (platform == null || underside == null) return "platform / alt kısım yok, adacıklar eklenmedi";
            var motion = contents.GetComponent<MenuDioramaMotion>();

            if (contents.transform.Find(IsletsName) == null)
            {
                var islets = new GameObject(IsletsName).transform; islets.SetParent(contents.transform, false);
                // Ekranda yer: kameranın varsayılan kadrajından hesaplanır (tabela ile gezegen arası, sol altta butonların altı).
                var temp = new GameObject("temp camera").AddComponent<Camera>();
                temp.fieldOfView = CamFov; temp.aspect = 16f / 9f;
                PlaceCamera(temp, Vector3.zero);
                (Vector3 viewport, float scale, string plant, float yaw)[] spots =
                    { (new Vector3(.47f, .73f, 70f), 1.0f, "Planter 2,3", 25f), (new Vector3(.27f, .13f, 46f), .8f, "Planter 1,2", -35f) };
                var roots = new List<Transform>();
                for (int n = 0; n < spots.Length; n++)
                {
                    var islet = new GameObject($"Islet {n + 1}").transform;
                    islet.SetParent(islets, false);
                    islet.position = temp.ViewportToWorldPoint(spots[n].viewport);
                    islet.localRotation = Quaternion.Euler(0f, spots[n].yaw, 0f);
                    islet.localScale = Vector3.one * spots[n].scale;
                    for (int x = 0; x <= 1; x++)
                        for (int z = 0; z <= 1; z++)
                        {
                            Transform tile = platform.Find($"Tiles/Tile {x},{z}");
                            if (tile == null) continue;
                            var copy = Object.Instantiate(tile.gameObject, islet).transform;
                            copy.localPosition = new Vector3((x - .5f) * Cell, tile.localPosition.y, (z - .5f) * Cell);
                        }
                    Transform planter = platform.Find("Planters/" + spots[n].plant) ?? platform.Find("Planters").GetChild(0);
                    var p = Object.Instantiate(planter.gameObject, islet).transform;
                    p.localPosition = new Vector3(-.5f * Cell, 0f, -.5f * Cell);
                    Transform body = underside.Find("Island Body");
                    var b = Object.Instantiate(body.gameObject, islet).transform;
                    b.localPosition = new Vector3(0f, 0f, 0f);
                    b.localScale = new Vector3(.32f, .5f, .32f);
                    roots.Add(islet);
                }
                Object.DestroyImmediate(temp.gameObject);
                if (motion != null)
                {
                    var so = new SerializedObject(motion);
                    var prop = so.FindProperty("islets"); prop.arraySize = roots.Count;
                    for (int i = 0; i < roots.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = roots[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                log.Add("uzak adacıklar eklendi");
            }

            if (contents.transform.Find(PollenName) == null)
            {
                var go = new GameObject(PollenName, typeof(ParticleSystem));
                go.transform.SetParent(contents.transform, false);
                go.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                var ps = go.GetComponent<ParticleSystem>();
                var main = ps.main;
                main.loop = true; main.prewarm = true; main.useUnscaledTime = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(.05f, .25f);
                main.startSize = new ParticleSystem.MinMaxCurve(.3f, .55f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color32(255, 226, 140, 255), new Color32(190, 255, 170, 255));
                main.gravityModifier = -.012f;   // yavaşça yükselir
                main.maxParticles = 90;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                var emission = ps.emission; emission.rateOverTime = 12f;
                var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(15f, 3f, 15f);
                var col = ps.colorOverLifetime; col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .2f), new GradientAlphaKey(1f, .75f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                var noise = ps.noise; noise.enabled = true; noise.strength = .25f; noise.frequency = .3f;
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = PollenMaterial();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                log.Add("polen eklendi");
            }
            if (log.Count > 0) PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            return log.Count == 0 ? "adacıklar ve polen vardı (dokunulmadı)" : string.Join(", ", log);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    static Material PollenMaterial()
    {
        const string path = "Assets/Prefabs/Menu/MenuPollen.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader) { name = "MenuPollen" };
        var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);   // saydam
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);       // toplamalı (ışık gibi)
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // ------------------------------------------------------------------ toon görünüm (kullanıcı: "toonicliği kopuyor gibi")
    // Menüde kamera perspektif ve yakın: oyunun ince mürekkep çizgisi (oyunda uzaktan izometrik bakılıyor) burada zayıf kalıyor,
    // yeni parçalar (toprak, kaya, kök, taş) hiç çizgi almamıştı, polen yumuşak parlayan noktaydı. Düzeltme yalnız menüde:
    // - Karo / saksı / bitki: oyunun malzemelerinin menüye özel VARYANTI (Unity material variant; renkler oyunla bağlı kalır),
    //   yalnız dış çizgi kalınlığı ×MenuOutlineScale. Oyunun kendi malzemeleri değişmez.
    // - Toprak / kaya / kök / taş: aynı mürekkep rengiyle dış çizgi; mesh'ler paylaşılan köşeli (çizgi kopmaz).
    // - Polen: sert kenarlı, mürekkep çizgili çizgi roman parıltısı (yumuşak ışık noktası değil).
    // Yönetilen: varyanta geçmemiş renderer'lar geçirilir; geçmişlere dokunulmaz.
    // Toon güçlendirme (kullanıcı: "hâlâ toonluk az; saksıyı yerleştirdiğimiz karolarda da yok"): çizgi ×2,4, karolar da çizgi
    // alır (oyunda çizgisizler), gölgelendirme 2 ton sert (cel), menü ışığının gölgesi sert.
    const float MenuOutlineScale = 2.4f, TileOutline = .022f, SoilOutline = .042f, RootOutline = .016f, ToonSteps = 2f, ToonMinLight = .48f;
    static readonly Color InkColor = new(.065f, .08f, .035f, 1f);
    const string ToonFolder = "Assets/Prefabs/Menu/Toon", SparklePath = "Assets/Prefabs/Menu/MenuSparkle.png";

    static void InkOutline(Material m, float width)
    {
        if (m == null) return;
        if (m.HasProperty("_OtlWorldWidth")) m.SetFloat("_OtlWorldWidth", width);
        if (m.HasProperty("_OtlColor")) m.SetColor("_OtlColor", InkColor);
    }

    internal static Material MenuVariant(Material source)
    {
        if (source == null || source.shader == null || !source.HasProperty("_OtlWorldWidth")) return source;
        if (source.name.EndsWith("_Menu") || AssetDatabase.GetAssetPath(source).StartsWith("Assets/Prefabs/Menu")) return source;   // menünün kendi malzemeleri (toprak, kaya) ve varyantlar
        if (!AssetDatabase.IsValidFolder(ToonFolder)) AssetDatabase.CreateFolder("Assets/Prefabs/Menu", "Toon");
        string path = $"{ToonFolder}/{source.name}_Menu.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var variant = new Material(source) { name = source.name + "_Menu" };
        variant.parent = source;   // renkler / doku oyunun malzemesinden gelir; yalnız çizgi ve gölge basamağı burada
        ToonUp(variant);
        AssetDatabase.CreateAsset(variant, path);
        return variant;
    }

    // Menü varyantının toon ayarları: çizgi oyunun ×MenuOutlineScale (oyunda çizgisi yoksa karo çizgisi), 2 ton sert gölge.
    static void ToonUp(Material variant)
    {
        Material game = variant.parent != null ? variant.parent : variant;
        float width = game.HasProperty("_OtlWorldWidth") ? game.GetFloat("_OtlWorldWidth") : 0f;
        variant.SetFloat("_OtlWorldWidth", width > 0f ? width * MenuOutlineScale : TileOutline);
        if (variant.HasProperty("_OtlColor")) variant.SetColor("_OtlColor", InkColor);
        if (variant.HasProperty("_Steps")) variant.SetFloat("_Steps", ToonSteps);
        if (variant.HasProperty("_StpSmooth")) variant.SetFloat("_StpSmooth", 0f);
        if (variant.HasProperty("_MinLight") && game.HasProperty("_MinLight")) variant.SetFloat("_MinLight", Mathf.Max(game.GetFloat("_MinLight"), ToonMinLight));
    }

    // Var olan menü malzemelerini toon güçlendirmesine alır (yalnız bu komutla; araç kendiliğinden var olana dokunmaz).
    [MenuItem("Tools/Menü/Canlı Sahne: Toon Güçlendir")]
    public static void ToonBoostFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log(ApplyToonBoost());
    }

    public static string ApplyToonBoost()
    {
        int variants = 0;
        if (AssetDatabase.IsValidFolder(ToonFolder))
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { ToonFolder }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null) continue;
                ToonUp(m); EditorUtility.SetDirty(m); variants++;
            }
        foreach (var (name, width) in new[] { ("MenuSoil", SoilOutline), ("MenuSoilDark", SoilOutline), ("MenuRock", SoilOutline), ("MenuRoot", RootOutline) })
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Prefabs/Menu/{name}.mat");
            if (m == null) continue;
            InkOutline(m, width);
            if (m.HasProperty("_Steps")) m.SetFloat("_Steps", ToonSteps);
            if (m.HasProperty("_StpSmooth")) m.SetFloat("_StpSmooth", 0f);
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
        string toon = EnsureToonLook();   // çizgisiz karolar da varyanta geçer
        Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
        var light = scene.GetRootGameObjects().FirstOrDefault(g => g.name == LightName)?.GetComponent<Light>();
        if (light != null) { Undo.RecordObject(light, "Menü ışığı sert gölge"); light.shadows = LightShadows.Hard; }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return $"Menü toon güçlendirme: {variants} varyant (çizgi ×{MenuOutlineScale}, {ToonSteps} ton), toprak / kaya çizgisi {SoilOutline}, {toon}, ışık sert gölge";
    }

    public static string EnsureToonLook()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        int swapped = 0; bool sparkle = false;
        try
        {
            foreach (string group in new[] { "Platform/Tiles", "Platform/Planters", IsletsName })
            {
                Transform t = contents.transform.Find(group);
                if (t == null) continue;
                foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = r.sharedMaterials; bool changed = false;
                    for (int i = 0; i < mats.Length; i++) { var v = MenuVariant(mats[i]); if (v != mats[i]) { mats[i] = v; changed = true; } }
                    if (changed) { r.sharedMaterials = mats; swapped++; }
                }
            }
            var pollen = contents.transform.Find(PollenName)?.GetComponent<ParticleSystemRenderer>();
            if (pollen != null && pollen.sharedMaterial != null)
            {
                Texture2D star = SparkleTexture();
                Material m = pollen.sharedMaterial;
                Texture current = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                if (current != star)
                {
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", star);
                    if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", star);
                    if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);   // normal saydamlık: parlama değil, boyalı parıltı
                    m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    EditorUtility.SetDirty(m);
                    var ps = pollen.GetComponent<ParticleSystem>();
                    var main = ps.main;
                    main.startSize = new ParticleSystem.MinMaxCurve(.18f, .34f);   // az ve küçük: lo-fi sakinlik, konfeti değil
                    var emission = ps.emission; emission.rateOverTime = 4.5f;
                    main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-.6f, .6f);
                    sparkle = true;
                }
            }
            if (swapped > 0 || sparkle) PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        var log = new List<string>();
        if (swapped > 0) log.Add($"{swapped} renderer menü çizgi varyantına geçti");
        if (sparkle) log.Add("polen çizgi roman parıltısı oldu");
        return log.Count == 0 ? "toon görünüm vardı (dokunulmadı)" : string.Join(", ", log);
    }

    // 64 px dört kollu parıltı: krem dolgu, koyu mürekkep kenar, dışı saydam (kenarlar sert; mip yok).
    static Texture2D SparkleTexture()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(SparklePath);
        if (existing != null) return existing;
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        var fill = new Color32(255, 240, 196, 255); var ink = new Color32(40, 30, 48, 255); var clear = new Color32(0, 0, 0, 0);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = Mathf.Abs((x + .5f) / N * 2f - 1f), v = Mathf.Abs((y + .5f) / N * 2f - 1f);
                float d = Mathf.Sqrt(u) + Mathf.Sqrt(v);   // astroid: dört sivri kol
                tex.SetPixel(x, y, d <= .78f ? fill : d <= .95f ? ink : clear);
            }
        tex.Apply();
        System.IO.File.WriteAllBytes(SparklePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(SparklePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(SparklePath);
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(SparklePath);
    }

    // ------------------------------------------------------------------ adanın altı: toprak + kaya (kullanıcı: "altta toprak gibi zemin")
    // Yönetilen parça: prefab'da "Platform/Underside" yoksa eklenir, varsa dokunulmaz (elle düzenleme korunur). Üstte kahverengi
    // toprak katmanı, altında aşağı sivrilen köşeli kaya (yüzler düz gölgeli: toon basamakları kayada okunur), topraktan sarkan
    // kökler, çevrede süzülen küçük taşlar. Biçimler sabit tohumla üretilir (her kurulumda aynı) ve mesh varlığı olarak saklanır.
    // Malzemeler oyunun toon malzemesinin (Simple Toon / STDefault, GameToon/Ground) kopyası, yalnız renk farklı.
    public const string UndersideName = "Underside";
    // Süzülen taşlar adanın iki yanında ve arkasında, küçük (önde, adanın altında koyu bir kütle gibi durmasın).
    static readonly Vector3[] FloaterSpots = { new(-10.5f, -2.5f, 4f), new(10f, -1.8f, -7f), new(-12f, -4.5f, -6f), new(11.5f, -4f, 6f), new(3f, -9f, 11f) };
    static readonly float[] FloaterScales = { .5f, .45f, .6f, .4f, .55f };
    const string MeshAssetPath = "Assets/Prefabs/Menu/MenuIslandUnderside.asset";
    static readonly Color SoilColor = new Color32(158, 102, 66, 255), SoilDark = new Color32(120, 74, 50, 255), RockColor = new Color32(128, 104, 120, 255), RootColor = new Color32(104, 74, 52, 255);

    static string EnsureUnderside()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform platform = contents.transform.Find("Platform");
            if (platform == null) return "Platform yok, alt kısım eklenmedi";
            // Karoların kapladığı alan: alt kısmın üstü karoların altına oturur.
            Bounds tiles = new Bounds(platform.position, Vector3.zero); bool any = false;
            foreach (Renderer r in platform.Find("Tiles").GetComponentsInChildren<Renderer>(true)) { if (!any) { tiles = r.bounds; any = true; } else tiles.Encapsulate(r.bounds); }
            Vector3 local = platform.InverseTransformPoint(tiles.center);
            float half = Mathf.Min(tiles.extents.x, tiles.extents.z) - .15f, top = local.y - tiles.extents.y + .05f;

            Transform existing = platform.Find(UndersideName);
            if (existing != null)
            {
                // Üretilen biçim eskiyse yalnız mesh verisi yerinde yenilenir (nesneler, yerleri, malzemeler, elle ayarlar korunur).
                MeshFilter bodyFilter = existing.Find("Island Body")?.GetComponent<MeshFilter>();
                if (bodyFilter == null || bodyFilter.sharedMesh == null || bodyFilter.sharedMesh.vertexCount == BodyVertexCount) return "alt kısım vardı (dokunulmadı)";
                var regen = new System.Random(7);
                Mesh fresh = IslandMesh(regen, half, top, new Vector2(local.x, local.z));
                Mesh freshRoots = RootsMesh(regen, half, top - 1.3f, new Vector2(local.x, local.z));
                CopyMesh(fresh, bodyFilter.sharedMesh);
                MeshFilter rootFilter = existing.Find("Hanging Roots")?.GetComponent<MeshFilter>();
                if (rootFilter != null && rootFilter.sharedMesh != null) CopyMesh(freshRoots, rootFilter.sharedMesh);
                Mesh freshPebble = PebbleMesh(regen);
                MeshFilter pebbleFilter = existing.Find("Floating Rock 1")?.GetComponent<MeshFilter>();
                if (pebbleFilter != null && pebbleFilter.sharedMesh != null) CopyMesh(freshPebble, pebbleFilter.sharedMesh);
                Object.DestroyImmediate(fresh); Object.DestroyImmediate(freshRoots); Object.DestroyImmediate(freshPebble);
                foreach (var (name, minLight) in new[] { ("MenuSoil", .5f), ("MenuSoilDark", .48f), ("MenuRock", .42f), ("MenuRoot", .5f) })
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Prefabs/Menu/{name}.mat");
                    if (m != null && m.HasProperty("_MinLight")) { m.SetFloat("_MinLight", minLight); InkOutline(m, name == "MenuRoot" ? RootOutline : SoilOutline); EditorUtility.SetDirty(m); }
                }
                AssetDatabase.SaveAssets();
                return "alt kısım biçimi yenilendi (v3: oranlı, ters yüz yok, kesintisiz dış çizgi)";
            }

            Material soil = ToonMaterial("MenuSoil", SoilColor), rock = ToonMaterial("MenuRock", RockColor), root = ToonMaterial("MenuRoot", RootColor), soilDark = ToonMaterial("MenuSoilDark", SoilDark);
            var rng = new System.Random(7);
            Mesh body = IslandMesh(rng, half, top, new Vector2(local.x, local.z));
            Mesh roots = RootsMesh(rng, half, top - 1.3f, new Vector2(local.x, local.z));
            Mesh pebble = PebbleMesh(rng);
            SaveMeshes(body, roots, pebble);

            var under = new GameObject(UndersideName).transform; under.SetParent(platform, false);
            AddRenderer(under, "Island Body", body, new[] { soil, soilDark, rock });
            AddRenderer(under, "Hanging Roots", roots, new[] { root });
            var floaters = new List<Transform>();
            Vector3[] spots = FloaterSpots;
            for (int i = 0; i < spots.Length; i++)
            {
                Transform t = AddRenderer(under, $"Floating Rock {i + 1}", pebble, new[] { i % 2 == 0 ? rock : soilDark });
                t.localPosition = new Vector3(local.x, 0f, local.z) + spots[i];
                t.localScale = Vector3.one * FloaterScales[i];
                floaters.Add(t);
            }

            var motion = contents.GetComponent<MenuDioramaMotion>();
            if (motion != null)
            {
                var so = new SerializedObject(motion);
                var prop = so.FindProperty("floaters"); prop.arraySize = floaters.Count;
                for (int i = 0; i < floaters.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = floaters[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            return "alt kısım (toprak + kaya + kökler + taşlar) eklendi";
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    static Transform AddRenderer(Transform parent, string name, Mesh mesh, Material[] materials)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.GetComponent<MeshRenderer>(); r.sharedMaterials = materials;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return go.transform;
    }

    static Material ToonMaterial(string name, Color color)
    {
        string path = $"Assets/Prefabs/Menu/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        Material template = AssetDatabase.FindAssets("t:Material Ground", new[] { "Assets/Materials/GameToon" })
            .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(m => m != null);
        var mat = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = name;
        // Simple Toon: taban renk = doku × _Color × _BaseColor (şablonda _Color siyah: ada simsiyah çıkıyordu).
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", null);
        // Ada altı çoğunlukla gölge tarafından görünür: gölge basamağı karanlığa düşmesin (kahverengi / mor-gri okunsun).
        if (mat.HasProperty("_MinLight")) mat.SetFloat("_MinLight", name == "MenuRock" ? .42f : .5f);
        InkOutline(mat, name == "MenuRoot" ? RootOutline : SoilOutline);
        if (mat.HasProperty("_Steps")) mat.SetFloat("_Steps", ToonSteps);
        if (mat.HasProperty("_StpSmooth")) mat.SetFloat("_StpSmooth", 0f);
        if (mat.HasProperty("_Steps")) mat.SetFloat("_Steps", 3f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void CopyMesh(Mesh src, Mesh dst)
    {
        dst.Clear();
        dst.name = src.name;
        dst.SetVertices(src.vertices);
        dst.SetNormals(src.normals);
        dst.subMeshCount = src.subMeshCount;
        for (int i = 0; i < src.subMeshCount; i++) dst.SetTriangles(src.GetTriangles(i), i);
        dst.RecalculateBounds();
        EditorUtility.SetDirty(dst);
    }

    static void SaveMeshes(Mesh body, Mesh roots, Mesh pebble)
    {
        if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath) != null) AssetDatabase.DeleteAsset(MeshAssetPath);
        AssetDatabase.CreateAsset(body, MeshAssetPath);
        AssetDatabase.AddObjectToAsset(roots, MeshAssetPath);
        AssetDatabase.AddObjectToAsset(pebble, MeshAssetPath);
        AssetDatabase.SaveAssets();
    }

    // Halkalardan kurulan, aşağı sivrilen kütle (v3). Alt mesh 0: toprak bandı, 1: koyu toprak, 2: kaya.
    // v2'de her dörtgen ayrı yüzdü; oyunun toon dış çizgisi (köşe normali boyunca şişirilen kabuk) keskin kenarlarda kopuyordu.
    // v3'te köşeler paylaşılır (yumuşak normal): çizgi kesintisiz, toon basamakları temiz bantlar. Yönler kütlenin içinden.
    // Oran (kullanıcı: "oran orantı otursun"): derinlik genişliğin yaklaşık yarısı.
    public const string BodyMeshName = "Menu Island Body v3";
    // Sürüm işareti yapıdan (Unity varlığın ana nesnesini dosya adıyla yeniden adlandırıyor, ad güvenilmez):
    // v3 = 7 halka × 16 köşe + uç + kapak (16 + merkez).
    public const int BodyVertexCount = 7 * 16 + 1 + 16 + 1;
    static Mesh IslandMesh(System.Random rng, float half, float top, Vector2 c)
    {
        const int Sides = 16;
        float[] ys = { 0f, -.75f, -1.55f, -2.7f, -3.9f, -5.1f, -6.2f };
        float[] scale = { 1f, .98f, .92f, .8f, .62f, .4f, .18f };
        var verts = new List<Vector3>();
        for (int k = 0; k < ys.Length; k++)
        {
            float roundness = Mathf.Clamp01((k - 1) / 2f);   // toprak bantları karo biçiminde (kare), kaya yuvarlaklaşır
            for (int i = 0; i < Sides; i++)
            {
                float a = i * Mathf.PI * 2f / Sides + Mathf.PI / Sides;
                float sq = 1f / Mathf.Max(Mathf.Abs(Mathf.Cos(a)), Mathf.Abs(Mathf.Sin(a)));
                float r = half * scale[k] * Mathf.Lerp(Mathf.Min(sq, 1.3f), 1.12f, roundness);
                float jitter = 1f + (float)(rng.NextDouble() - .5) * (k <= 1 ? .03f : k == 2 ? .08f : .16f);
                float dy = k == 0 ? 0f : (float)(rng.NextDouble() - .5) * (k <= 2 ? .1f : .45f);
                verts.Add(new Vector3(c.x + Mathf.Cos(a) * r * jitter, top + ys[k] + dy, c.y + Mathf.Sin(a) * r * jitter));
            }
        }
        int tip = verts.Count; verts.Add(new Vector3(c.x + .6f, top - 7.0f, c.y - .4f));
        int capStart = verts.Count; for (int i = 0; i < Sides; i++) verts.Add(verts[i]);
        int capCenter = verts.Count; verts.Add(new Vector3(c.x, top, c.y));
        var inside = new Vector3(c.x, top - 2.4f, c.y);
        var subs = new[] { new List<int>(), new List<int>(), new List<int>() };
        void Tri(int sub, int a, int b, int d, Vector3 outwardFrom)
        {
            Vector3 pa = verts[a], pb = verts[b], pd = verts[d];
            if (Vector3.Dot(Vector3.Cross(pb - pa, pd - pa), (pa + pb + pd) / 3f - outwardFrom) < 0f) (b, d) = (d, b);
            subs[sub].Add(a); subs[sub].Add(b); subs[sub].Add(d);
        }
        for (int k = 0; k < ys.Length - 1; k++)
        {
            int sub = k == 0 ? 0 : k == 1 ? 1 : 2;
            for (int i = 0; i < Sides; i++)
            {
                int j = (i + 1) % Sides;
                int A = k * Sides + i, Bq = k * Sides + j, D = (k + 1) * Sides + j, E = (k + 1) * Sides + i;
                Tri(sub, A, Bq, D, inside); Tri(sub, A, D, E, inside);
            }
        }
        int last = (ys.Length - 1) * Sides;
        for (int i = 0; i < Sides; i++) Tri(2, last + i, last + (i + 1) % Sides, tip, inside);
        var center = verts[capCenter];
        for (int i = 0; i < Sides; i++) Tri(0, capStart + (i + 1) % Sides, capStart + i, capCenter, center - Vector3.up);

        var mesh = new Mesh { name = BodyMeshName };
        mesh.SetVertices(verts);
        mesh.subMeshCount = subs.Length;
        for (int i = 0; i < subs.Length; i++) mesh.SetTriangles(subs[i], i);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    // Topraktan sarkan kökler: üç parçalı, uca doğru incelen, hafif kıvrık altıgen çubuklar.
    static Mesh RootsMesh(System.Random rng, float half, float y, Vector2 c)
    {
        var verts = new List<Vector3>(); var tris = new List<int>();
        for (int n = 0; n < 9; n++)
        {
            float a = n * Mathf.PI * 2f / 9f + (float)rng.NextDouble() * .4f;
            float sq = 1f / Mathf.Max(Mathf.Abs(Mathf.Cos(a)), Mathf.Abs(Mathf.Sin(a)));
            Vector3 p = new Vector3(c.x + Mathf.Cos(a) * half * .93f * Mathf.Min(sq, 1.25f), y + (float)rng.NextDouble() * .4f, c.y + Mathf.Sin(a) * half * .93f * Mathf.Min(sq, 1.25f));
            float r = .11f + (float)rng.NextDouble() * .05f, len = 1.1f + (float)rng.NextDouble() * 1.6f;
            Vector3 dir = new Vector3(Mathf.Cos(a) * .25f, -1f, Mathf.Sin(a) * .25f).normalized;
            for (int s = 0; s < 3; s++)
            {
                Vector3 q = p + dir * (len / 3f);
                float r2 = r * .55f;
                AddPrism(verts, tris, p, q, r, r2);
                p = q; r = r2; dir = (dir + new Vector3((float)(rng.NextDouble() - .5) * .8f, -.2f, (float)(rng.NextDouble() - .5) * .8f)).normalized;
            }
        }
        var mesh = new Mesh { name = "Menu Island Roots" }; mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    static void AddPrism(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, float ra, float rb)
    {
        const int Sides = 6;
        Vector3 axis = (b - a).normalized, u = Vector3.Cross(axis, Vector3.right).sqrMagnitude > .01f ? Vector3.Cross(axis, Vector3.right).normalized : Vector3.Cross(axis, Vector3.forward).normalized, v = Vector3.Cross(axis, u);
        for (int i = 0; i < Sides; i++)
        {
            float t0 = i * Mathf.PI * 2f / Sides, t1 = (i + 1) * Mathf.PI * 2f / Sides;
            Vector3 d0 = u * Mathf.Cos(t0) + v * Mathf.Sin(t0), d1 = u * Mathf.Cos(t1) + v * Mathf.Sin(t1);
            Vector3 p0 = a + d0 * ra, p1 = a + d1 * ra, p2 = b + d1 * rb, p3 = b + d0 * rb, mid = (a + b) * .5f;
            AddFacing(verts, tris, p0, p1, p2, mid); AddFacing(verts, tris, p0, p2, p3, mid);
        }
    }

    // Küçük taş: titretilmiş ikosahedron, köşeler paylaşılır (yumuşak normal: toon dış çizgisi kopmaz).
    static Mesh PebbleMesh(System.Random rng)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] p = { new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1) };
        for (int i = 0; i < p.Length; i++) p[i] = p[i].normalized * (.8f + (float)rng.NextDouble() * .4f);
        int[] f = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        var tris = new List<int>();
        for (int i = 0; i < f.Length; i += 3)
        {
            int a = f[i], b = f[i + 1], d = f[i + 2];
            if (Vector3.Dot(Vector3.Cross(p[b] - p[a], p[d] - p[a]), p[a] + p[b] + p[d]) < 0f) (b, d) = (d, b);
            tris.Add(a); tris.Add(b); tris.Add(d);
        }
        var mesh = new Mesh { name = "Menu Floating Rock" };
        mesh.SetVertices(p); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    // Üçgeni, normali "inside" noktasından dışa bakacak sırayla ekler (Unity'de ön yüz = cross(b-a, c-a) yönü). Köşe paylaşmaz.
    static void AddFacing(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 d, Vector3 inside)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, d - a), (a + b + d) / 3f - inside) < 0f) (b, d) = (d, b);
        int n = verts.Count; verts.Add(a); verts.Add(b); verts.Add(d); tris.Add(n); tris.Add(n + 1); tris.Add(n + 2);
    }

    static Mesh Build(string name, List<Vector3> verts, List<int>[] subs)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.subMeshCount = subs.Length;
        for (int i = 0; i < subs.Length; i++) mesh.SetTriangles(subs[i], i);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    // Oyun sahnesinin ışığı menüye kopyalanır (menüde ışık yoktu: oyunun toon malzemeleri soluk griye düşüyordu):
    // yönlü ışık (yön, renk, şiddet, gölge) ve ortam ışığı + sis. Yalnız ilk kurulumda; sonra menü sahnesinde düzenlenir.
    public const string LightName = "Menu Light";

    static string CopyGameLighting(Scene menu)
    {
        Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(game);
        var ambientMode = RenderSettings.ambientMode; var ambientSky = RenderSettings.ambientSkyColor; var ambientEquator = RenderSettings.ambientEquatorColor;
        var ambientGround = RenderSettings.ambientGroundColor; var ambientLight = RenderSettings.ambientLight; float ambientIntensity = RenderSettings.ambientIntensity;
        var skybox = RenderSettings.skybox; bool fog = RenderSettings.fog; var fogColor = RenderSettings.fogColor; var fogMode = RenderSettings.fogMode;
        float fogDensity = RenderSettings.fogDensity, fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance;
        Light[] lights = game.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true)).ToArray();
        Light source = lights.FirstOrDefault(l => l != null && l.type == LightType.Directional);
        bool hasSource = source != null; string sourceName = hasSource ? source.name : "";   // sahne kapanınca ışık nesnesi yok olur: bilgiler önce alınır
        // Oyun kamerasının çizim ayarı (URP renderer; oyunun toon görünümü buna bağlı) menü kamerasına da verilir.
        Camera gameCam = game.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c => c.CompareTag("MainCamera"))
                         ?? game.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
        int rendererIndex = -1;
        if (gameCam != null && gameCam.TryGetComponent(out UnityEngine.Rendering.Universal.UniversalAdditionalCameraData gameData))
            rendererIndex = new SerializedObject(gameData).FindProperty("m_RendererIndex").intValue;
        Quaternion lightRotation = source != null ? source.transform.rotation : Quaternion.Euler(50f, -30f, 0f);
        Color lightColor = source != null ? source.color : Color.white;
        float lightIntensity = source != null ? source.intensity : 1f;
        LightShadows shadows = source != null ? source.shadows : LightShadows.Soft;
        float shadowStrength = source != null ? source.shadowStrength : 1f;
        SceneManager.SetActiveScene(menu);
        EditorSceneManager.CloseScene(game, true);

        RenderSettings.ambientMode = ambientMode; RenderSettings.ambientSkyColor = ambientSky; RenderSettings.ambientEquatorColor = ambientEquator;
        RenderSettings.ambientGroundColor = ambientGround; RenderSettings.ambientLight = ambientLight; RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.skybox = skybox; RenderSettings.fog = fog; RenderSettings.fogColor = fogColor; RenderSettings.fogMode = fogMode;
        RenderSettings.fogDensity = fogDensity; RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
        Camera menuCam = menu.GetRootGameObjects().FirstOrDefault(g => g.name == CameraName)?.GetComponent<Camera>();
        if (menuCam != null && rendererIndex >= 0)
        {
            var data = menuCam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() ?? menuCam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.SetRenderer(rendererIndex);
            data.renderPostProcessing = false;   // oyunda da kapalı
        }
        if (menu.GetRootGameObjects().Any(g => g.name == LightName)) return $"ortam ışığı kopyalandı, renderer {rendererIndex}";
        var lightObject = new GameObject(LightName, typeof(Light));
        SceneManager.MoveGameObjectToScene(lightObject, menu);
        Undo.RegisterCreatedObjectUndo(lightObject, "Menü ışığı");
        var light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional; light.color = lightColor; light.intensity = lightIntensity; light.shadowStrength = shadowStrength;
        light.shadows = shadows == LightShadows.None ? LightShadows.None : LightShadows.Hard;   // toon: sert gölge
        lightObject.transform.rotation = lightRotation;
        return (hasSource ? $"oyunun ışığı kopyalandı ({sourceName}, şiddet {lightIntensity:0.##})" : "oyunda yönlü ışık bulunamadı, varsayılan ışık") + $", renderer {rendererIndex}";
    }

    public static void PlaceCamera(Camera cam, Vector3 islandCenter)
    {
        Vector3 dir = Quaternion.Euler(CamElevation, CamAzimuth, 0f) * Vector3.forward;
        Vector3 target = islandCenter + Vector3.up * CamTargetY;
        cam.transform.rotation = Quaternion.LookRotation(dir);
        float halfW = CamDistance * Mathf.Tan(CamFov * .5f * Mathf.Deg2Rad) * (16f / 9f);
        cam.transform.position = target - dir * CamDistance - cam.transform.right * ((IslandScreenX - .5f) * 2f * halfW);
    }

    static void LayoutButton(SerializedObject menu, string field, Vector2 position, Vector2 size)
    {
        var button = menu.FindProperty(field).objectReferenceValue as Button;
        if (button == null) return;
        var rect = (RectTransform)button.transform;
        Undo.RecordObject(rect, "Menü butonları");
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static GameObject CreatePrefab()
    {
        // Geçici boş sahnede kurulur; GameScene yalnız karo görünüşünü (mesh + malzeme) almak için açılır ve kapanır.
        Scene temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject(RootName);
        var platform = new GameObject("Platform").transform; platform.SetParent(root.transform, false);
        var tiles = new GameObject("Tiles").transform; tiles.SetParent(platform, false);
        var planters = new GameObject("Planters").transform; planters.SetParent(platform, false);

        Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
        GroundCell source = game.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GroundCell>(true)).FirstOrDefault();
        if (source == null) throw new System.Exception("GameScene'de GroundCell bulunamadı");
        var cellSo = new SerializedObject(source);
        var locked = cellSo.FindProperty("lockedMaterial").objectReferenceValue as Material;
        var unlockedProp = cellSo.FindProperty("unlockedMaterials");
        var unlocked = new Material[unlockedProp.arraySize];
        for (int i = 0; i < unlocked.Length; i++) unlocked[i] = unlockedProp.GetArrayElementAtIndex(i).objectReferenceValue as Material;
        GameObject template = Object.Instantiate(source.gameObject);
        SceneManager.MoveGameObjectToScene(template, temp);
        float tileY = source.transform.position.y;
        EditorSceneManager.CloseScene(game, true);
        StripToVisual(template);

        for (int x = -3; x <= 3; x++)
            for (int z = -3; z <= 3; z++)
            {
                GameObject tile = Object.Instantiate(template, tiles);
                tile.name = $"Tile {x},{z}";
                tile.transform.localPosition = new Vector3(x * Cell, tileY, z * Cell);
                bool open = Mathf.Abs(x) <= 2 && Mathf.Abs(z) <= 2;
                foreach (Renderer r in tile.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = open ? (unlocked.Length > 0 ? unlocked[i % unlocked.Length] : mats[i]) : (locked != null ? locked : mats[i]);
                    r.sharedMaterials = mats;
                }
            }
        Object.DestroyImmediate(template);

        var planterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlanterPath);
        var plantRoots = new List<Transform>();
        int k = 0;
        for (int z = 1; z >= -1; z--)
            for (int x = -1; x <= 1; x++, k++)
            {
                var planter = (GameObject)PrefabUtility.InstantiatePrefab(planterPrefab, temp);
                planter.transform.SetParent(planters, false);
                planter.transform.localPosition = new Vector3(x * Cell, 0f, z * Cell);
                Transform spawn = planter.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "MineSpawnPoint");
                Vector3 plantPos = spawn != null ? spawn.position : planter.transform.position;
                var plantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Plants/{PlantNames[k]}.prefab");
                if (plantPrefab != null)
                {
                    var plant = (GameObject)PrefabUtility.InstantiatePrefab(plantPrefab, temp);
                    PrefabUtility.UnpackPrefabInstance(plant, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    plant.transform.SetParent(planter.transform, true);
                    plant.transform.position = plantPos;
                    plant.transform.localRotation = Quaternion.Euler(0f, k * 47f, 0f);   // sıradan gelen dönüş: hepsi aynı yöne bakmasın
                    plantRoots.Add(plant.transform);
                }
                PrefabUtility.UnpackPrefabInstance(planter, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                StripToVisual(planter);
                planter.name = $"Planter {x + 1},{z + 1}";
            }

        var motion = root.AddComponent<MenuDioramaMotion>();
        var so = new SerializedObject(motion);
        so.FindProperty("platform").objectReferenceValue = platform;
        var plantsProp = so.FindProperty("plants");
        plantsProp.arraySize = plantRoots.Count;
        for (int i = 0; i < plantRoots.Count; i++) plantsProp.GetArrayElementAtIndex(i).objectReferenceValue = plantRoots[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        System.IO.Directory.CreateDirectory("Assets/Prefabs/Menu");
        GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return asset;
    }

    // Yalnız görsel kalır: Transform, mesh, renderer, LOD, Animator. Oyun betikleri, collider'lar, ses, parçacık sökülür;
    // kapalı yardımcılar (hayalet görünüm vb.) silinir. Bağımlılık sırası yüzünden birkaç turda.
    static void StripToVisual(GameObject go)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true).Where(t => t != go.transform && !t.gameObject.activeSelf).ToList())
            if (t != null) Object.DestroyImmediate(t.gameObject);
        for (int pass = 0; pass < 4; pass++)
            foreach (Component c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer || c is LODGroup || c is Animator) continue;
                Object.DestroyImmediate(c);
            }
    }
}
