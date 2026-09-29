using UnityEngine;
using UnityEngine.SceneManagement;

// Oyun sahnesinde (RoundManager olan sahne) ana kameraya CameraFeel ve SpaceBackground, sahneye de
// GameFeelDirector, grid koordinat etiketleri ve yeni tile çerçeveleri yoksa ekler.
// Sahneye kalıcı eklemek ve inspector'dan ayarlamak için: Tools > Feel > Sahneye Kur.
// Batch mode doğrulamaları (performans testleri) etkilenmesin diye orada çalışmaz.
public static class FeelBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (Application.isBatchMode) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Install();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Install();

    private static void Install()
    {
        if (Object.FindAnyObjectByType<RoundManager>() == null) return;
        Camera camera = Camera.main;
        if (camera == null) return;

        if (!camera.TryGetComponent(out CameraFeel _)) camera.gameObject.AddComponent<CameraFeel>();
        if (Object.FindAnyObjectByType<SpaceBackground>() == null) camera.gameObject.AddComponent<SpaceBackground>();
        if (Object.FindAnyObjectByType<GameFeelDirector>() == null) new GameObject("Game Feel Director").AddComponent<GameFeelDirector>();
        if (Object.FindAnyObjectByType<GridCoordinateLabels>() == null) new GameObject("Grid Coordinates").AddComponent<GridCoordinateLabels>();
        if (Object.FindAnyObjectByType<FreshTileMarkers>() == null) new GameObject("Fresh Tile Markers").AddComponent<FreshTileMarkers>();
    }
}
