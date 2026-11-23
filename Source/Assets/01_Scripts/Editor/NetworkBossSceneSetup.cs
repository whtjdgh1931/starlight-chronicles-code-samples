using System;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 공동 보스맵 이동 설정과 포털·씬·세션 참조 확인
/// </summary>
public static class NetworkBossSceneSetup
{
    [MenuItem("Tools/Network/Configure Boss Scene Transition")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 공동 씬 이동 설정 필요");
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                throw new InvalidOperationException("열린 씬 저장 후 공동 이동 설정 필요");
            }
        }
        SceneSetup[] previousScenes = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string name in new[] { "TitleScene", "HomeScene" })
            {
                Scene scene = EditorSceneManager.OpenScene($"Assets/00_Scenes/{name}.unity", OpenSceneMode.Single);
                NetworkSessionController session = FindInScene<NetworkSessionController>(scene);
                NetworkManager manager = session.GetComponent<NetworkManager>();
                manager.NetworkConfig.EnableSceneManagement = true;
                var serialized = new SerializedObject(session);
                serialized.FindProperty("_bossScene").intValue = (int)GameSceneId.BossScene;
                serialized.FindProperty("_bossArrivalPortalId").intValue = 6;
                serialized.FindProperty("_sceneTransitionTimeoutSeconds").floatValue = 60f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException($"{name}: 공동 씬 설정 저장 실패");
                }
            }

            Scene boss = EditorSceneManager.OpenScene("Assets/00_Scenes/BossScene.unity", OpenSceneMode.Single);
            FindInScene<MapManager>(boss);
            FindInScene<NetworkBossEncounterController>(boss);
            bool hasArrivalPortal = false;
            foreach (GameObject root in boss.GetRootGameObjects())
            {
                foreach (Portal portal in root.GetComponentsInChildren<Portal>(true))
                {
                    hasArrivalPortal |= portal.PortalID == 6 && portal.gameObject.activeInHierarchy;
                }
            }
            if (!hasArrivalPortal)
            {
                throw new InvalidOperationException("BossScene: 활성 도착 포털 6 필요");
            }
            bool inBuild = false;
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                inBuild |= scene.enabled && scene.path == "Assets/00_Scenes/BossScene.unity";
            }
            if (!inBuild)
            {
                throw new InvalidOperationException("Build Settings에 BossScene 등록 필요");
            }
            Debug.Log("NETWORK_BOSS_SCENE_SETUP_OK: scene management, session settings, boss map and arrival portal configured");
        }
        finally
        {
            if (previousScenes.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            }
        }
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
        throw new InvalidOperationException($"{scene.name}: {typeof(T).Name} 누락");
    }
}
