using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 기존 음악 배정과 BGM 프리팹·시작 씬 참조 구성
/// 재실행 시 지정한 기본 곡과 볼륨 재적용
/// </summary>
public static class BgmSetup
{
    private const string CatalogPath = "Assets/Config/Audio/DefaultBgmCatalog.asset";
    private const string PrefabPath = "Assets/Resources/Prefabs/Audio/BgmPlayer.prefab";
    private const string MusicRoot = "Assets/99_Assets/Casual & Relaxing Game Music";

    [MenuItem("Tools/Audio/Configure Scene BGM")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 BGM 구성 필요");
        }

        // 저장하지 않은 사용자 씬 변경 보호
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                throw new InvalidOperationException("열린 씬 저장 후 BGM 구성 필요");
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        AssetDatabase.Refresh();
        BgmCatalog catalog = ConfigureCatalog();
        GameObject prefab = ConfigurePrefab(catalog);
        SceneSetup[] previousScenes = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            ConnectScene("Assets/00_Scenes/TitleScene.unity", prefab);
            ConnectScene("Assets/00_Scenes/HomeScene.unity", prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("BGM_SETUP_OK: six scene entries, two channels and both GameManager references configured");
        }
        finally
        {
            // 배치 실행의 초기 빈 씬 목록 제외
            if (previousScenes.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            }
        }
    }

    private static BgmCatalog ConfigureCatalog()
    {
        BgmCatalog catalog = AssetDatabase.LoadAssetAtPath<BgmCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<BgmCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        var serialized = new SerializedObject(catalog);
        serialized.FindProperty("_fadeDuration").floatValue = 1f;
        SerializedProperty entries = serialized.FindProperty("_entries");
        entries.arraySize = 6;
        Assign(entries, 0, GameSceneId.TitleScene, "Happy", 0.22f);
        Assign(entries, 1, GameSceneId.SelectScene, "Happy", 0.22f);
        Assign(entries, 2, GameSceneId.HomeScene, "Forest", 0.22f);
        Assign(entries, 3, GameSceneId.HostScene, "Forest", 0.22f);
        Assign(entries, 4, GameSceneId.HuntScene, "Mystery", 0.20f);
        Assign(entries, 5, GameSceneId.BossScene, "Darkness", 0.25f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return catalog;
    }

    private static GameObject ConfigurePrefab(BgmCatalog catalog)
    {
        var root = new GameObject("BgmPlayer");
        try
        {
            BgmPlayer player = root.AddComponent<BgmPlayer>();
            AudioSource first = CreateChannel(root.transform, "FirstChannel");
            AudioSource second = CreateChannel(root.transform, "SecondChannel");
            var serialized = new SerializedObject(player);
            serialized.FindProperty("_catalog").objectReferenceValue = catalog;
            serialized.FindProperty("_firstSource").objectReferenceValue = first;
            serialized.FindProperty("_secondSource").objectReferenceValue = second;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void ConnectScene(string path, GameObject prefab)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        GameManager manager = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            GameManager candidate = root.GetComponentInChildren<GameManager>(true);
            if (candidate != null)
            {
                if (manager != null)
                {
                    throw new InvalidOperationException($"{path}: GameManager 중복");
                }
                manager = candidate;
            }
        }

        if (manager == null)
        {
            throw new InvalidOperationException($"{path}: GameManager 누락");
        }

        BgmPlayer player = manager.GetComponentInChildren<BgmPlayer>(true);
        if (player == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, manager.transform);
            player = instance.GetComponent<BgmPlayer>();
        }

        var serialized = new SerializedObject(manager);
        serialized.FindProperty("_bgmPlayer").objectReferenceValue = player;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new IOException($"{path}: BGM 연결 저장 실패");
        }
    }

    private static void Assign(SerializedProperty entries, int index, GameSceneId scene, string music, float volume)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{MusicRoot}/{music}.wav");
        if (clip == null)
        {
            throw new FileNotFoundException($"BGM 원본 누락: {music}");
        }

        SerializedProperty entry = entries.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("_scene").intValue = (int)scene;
        entry.FindPropertyRelative("_clip").objectReferenceValue = clip;
        entry.FindPropertyRelative("_volume").floatValue = volume;
    }

    private static AudioSource CreateChannel(Transform parent, string name)
    {
        var channel = new GameObject(name);
        channel.transform.SetParent(parent, false);
        AudioSource source = channel.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.priority = 0;
        AudioSettingsSetup.AssignOutput(source, true);
        return source;
    }
}
