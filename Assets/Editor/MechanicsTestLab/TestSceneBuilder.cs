using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MechanicsTestLab
{
    // MechanicsTestScene'i kurar. Sahne yalnız bir BAŞLATICIDIR: içinde oyun nesnesi, kamera ya da kopyalanmış hiyerarşi yoktur.
    // Play'e basılınca TestSession gerçek GameScene'i yükler; böylece test sahnesi oyundan ayrışamaz. Sahne Build Settings'e
    // eklenmez (oyuncu build'ine ve menü akışına girmez). Kurulum açık sahneyi değiştirmez: yeni sahne yanına açılır, kaydedilir
    // ve kapatılır.
    public static class TestSceneBuilder
    {
        public const string MarkerName = "Mechanics Test Launcher (Play: gerçek GameScene test oturumu olarak yüklenir)";

        [MenuItem(Lab.Menu + "/Test sahnesini (yeniden) oluştur", priority = 120)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Play modunda test sahnesi oluşturulmaz."); return; }
            // Açık sahne kayıtlı bir sahneyse dokunulmaz (yeni sahne yanına açılır). Unity, kaydedilmemiş ADSIZ bir sahnenin yanına
            // sahne açtırmaz: o durumda değişiklik varsa önce Unity'nin normal kaydetme sorusu sorulur.
            Scene active = SceneManager.GetActiveScene();
            bool untitled = string.IsNullOrEmpty(active.path);
            if (untitled && active.isDirty && !Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, untitled ? NewSceneMode.Single : NewSceneMode.Additive);
            var marker = new GameObject(MarkerName);
            SceneManager.MoveGameObjectToScene(marker, scene);
            bool saved = EditorSceneManager.SaveScene(scene, Lab.ScenePath);
            if (!untitled) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
            if (saved) Debug.Log($"Test sahnesi kuruldu: {Lab.ScenePath}. Build Settings'e eklenmedi.");
            else Debug.LogError($"Test sahnesi kaydedilemedi: {Lab.ScenePath}");
        }

        [MenuItem(Lab.Menu + "/Test sahnesini (yeniden) oluştur", true)]
        private static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(Lab.Menu + "/Test sahnesini aç", priority = 101)]
        private static void OpenScene()
        {
            Result result = TestSession.OpenScene();
            if (!result.Ok) Debug.LogWarning(result.Message);
        }

        [MenuItem(Lab.Menu + "/Test oturumunu başlat (Play)", priority = 102)]
        private static void StartPlay()
        {
            Result result = TestSession.StartPlay(TestSession.Pending);
            if (!result.Ok) Debug.LogWarning(result.Message);
        }

        // Batch: izole kopyada sahne dosyasını üretir (ana projeye kopyalanmak üzere).
        public static void BuildBatch()
        {
            Build();
            EditorApplication.Exit(AssetDatabase.LoadAssetAtPath<SceneAsset>(Lab.ScenePath) != null ? 0 : 1);
        }
    }
}
