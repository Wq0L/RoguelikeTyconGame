using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runtime'da otomatik eklenen feel bileşenlerini sahneye kalıcı ekler; böylece değerler inspector'dan ayarlanıp kaydedilir.
public static class FeelSceneSetup
{
    [MenuItem("Tools/Feel/Sahneye Kur (Kamera, Uzay, Director, Koordinatlar)")]
    private static void Setup()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            EditorUtility.DisplayDialog("Feel", "Sahnede MainCamera tag'li kamera bulunamadı.", "Tamam");
            return;
        }

        bool changed = false;
        if (!camera.TryGetComponent(out CameraFeel _))
        {
            Undo.AddComponent<CameraFeel>(camera.gameObject);
            changed = true;
        }
        if (!camera.TryGetComponent(out SpaceBackground _))
        {
            Undo.AddComponent<SpaceBackground>(camera.gameObject);
            changed = true;
        }
        if (Object.FindAnyObjectByType<GameFeelDirector>() == null)
        {
            var director = new GameObject("Game Feel Director");
            SceneManager.MoveGameObjectToScene(director, camera.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(director, "Add Game Feel Director");
            director.AddComponent<GameFeelDirector>();
            changed = true;
        }

        if (Object.FindAnyObjectByType<GridCoordinateLabels>() == null)
        {
            var labels = new GameObject("Grid Coordinates");
            SceneManager.MoveGameObjectToScene(labels, camera.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(labels, "Add Grid Coordinates");
            labels.AddComponent<GridCoordinateLabels>();
            changed = true;
        }

        if (Object.FindAnyObjectByType<FreshTileMarkers>() == null)
        {
            var markers = new GameObject("Fresh Tile Markers");
            SceneManager.MoveGameObjectToScene(markers, camera.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(markers, "Add Fresh Tile Markers");
            markers.AddComponent<FreshTileMarkers>();
            changed = true;
        }

        if (changed) EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
        Selection.activeGameObject = camera.gameObject;
    }

    [MenuItem("Tools/Feel/Sahneye Kur (Kamera, Uzay, Director, Koordinatlar)", true)]
    private static bool CanSetup() => !EditorApplication.isPlaying;
}
