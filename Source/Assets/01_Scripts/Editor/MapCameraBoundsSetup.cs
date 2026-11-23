using System;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 플레이 맵의 타일 범위를 기준으로 카메라 경계 구성
/// 기존 경계가 있으면 수동 조정한 좌표 유지
/// </summary>
public static class MapCameraBoundsSetup
{
    private static readonly string[] ScenePaths =
    {
        "Assets/00_Scenes/HomeScene.unity",
        "Assets/00_Scenes/HostScene.unity",
        "Assets/00_Scenes/HuntScene.unity",
        "Assets/00_Scenes/BossScene.unity"
    };

    [MenuItem("Tools/Camera/Configure Horizontal Map Bounds")]
    public static void ConfigureHorizontal() => Configure(false);

    [MenuItem("Tools/Camera/Configure Full Map Bounds")]
    public static void ConfigureFull() => Configure(true);

    private static void Configure(bool limitVertically)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 카메라 경계 구성 필요");
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                throw new InvalidOperationException("열린 씬 저장 후 카메라 경계 구성 필요");
            }
        }

        SceneSetup[] previousScenes = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string path in ScenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                MapManager map = FindInScene<MapManager>(scene);
                MapCameraController controller = FindInScene<MapCameraController>(scene);
                Camera output = controller.GetComponent<Camera>();
                var controllerSettings = new SerializedObject(controller);
                var vcam = (CinemachineVirtualCamera)controllerSettings
                    .FindProperty("_cinemachineCamera").objectReferenceValue;

                MapCameraBounds extension = vcam.GetComponent<MapCameraBounds>();
                bool isNew = extension == null;
                if (isNew)
                {
                    extension = vcam.gameObject.AddComponent<MapCameraBounds>();
                }
                var settings = new SerializedObject(extension);
                settings.FindProperty("_outputCamera").objectReferenceValue = output;
                settings.FindProperty("_limitVertically").boolValue = limitVertically;
                if (isNew)
                {
                    var mapSettings = new SerializedObject(map);
                    var ground = (Tilemap)mapSettings.FindProperty("_currentTilemap").objectReferenceValue;
                    Bounds bounds = GetOccupiedBounds(ground);
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        foreach (Tilemap tilemap in root.GetComponentsInChildren<Tilemap>(true))
                        {
                            if (tilemap != ground && tilemap.GetComponent<TilemapCollider2D>() != null &&
                                tilemap.GetUsedTilesCount() > 0)
                            {
                                bounds.Encapsulate(GetOccupiedBounds(tilemap));
                            }
                        }
                    }
                    settings.FindProperty("_mapBounds").rectValue =
                        Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
                }
                settings.ApplyModifiedPropertiesWithoutUndo();
                controllerSettings.FindProperty("_cameraBounds").objectReferenceValue = extension;
                controllerSettings.ApplyModifiedPropertiesWithoutUndo();
                extension.ValidateConfiguration();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException($"카메라 경계 씬 저장 실패: {path}");
                }
                Debug.Log($"CAMERA_BOUNDS_CONFIGURED: {path}, Vertical={limitVertically}");
            }
        }
        finally
        {
            if (previousScenes.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            }
        }
        Debug.Log("MAP_CAMERA_BOUNDS_SETUP_OK");
    }

    private static Bounds GetOccupiedBounds(Tilemap tilemap)
    {
        Vector3Int minCell = new(int.MaxValue, int.MaxValue, 0);
        Vector3Int maxCell = new(int.MinValue, int.MinValue, 1);
        foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(cell))
            {
                continue;
            }
            minCell.x = Mathf.Min(minCell.x, cell.x);
            minCell.y = Mathf.Min(minCell.y, cell.y);
            maxCell.x = Mathf.Max(maxCell.x, cell.x + 1);
            maxCell.y = Mathf.Max(maxCell.y, cell.y + 1);
        }
        if (minCell.x == int.MaxValue)
        {
            throw new InvalidOperationException($"{tilemap.name}: 카메라 경계의 기준 타일 필요");
        }
        Vector3 min = tilemap.CellToWorld(minCell);
        Vector3 max = tilemap.CellToWorld(maxCell);
        var bounds = new Bounds();
        bounds.SetMinMax(min, max);
        return bounds;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }
        throw new MissingReferenceException($"{scene.name}: {typeof(T).Name} 필요");
    }
}
