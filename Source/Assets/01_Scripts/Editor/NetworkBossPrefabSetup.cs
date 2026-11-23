using System;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 기존 보스 설정 기반 네트워크 프리팹 생성과 BossScene 연결
/// Unity 에셋 API를 통한 GUID 및 직렬화 참조 보존
/// </summary>
public static class NetworkBossPrefabSetup
{
    private const string BossScenePath = "Assets/00_Scenes/BossScene.unity";
    private const string TargetPrefabPath = "Assets/Resources/Prefabs/Monster/NetworkBoss.prefab";
    private const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";

    [MenuItem("Tools/Network/Configure Boss Prefab")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 네트워크 보스 구성 필요");
        }

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene scene = EditorSceneManager.OpenScene(BossScenePath, OpenSceneMode.Single);
            BossSpawner localSpawner = FindSingle<BossSpawner>(scene);
            BossHudController hud = FindSingle<BossHudController>(scene);
            MapMonsterController map = FindSingle<MapMonsterController>(scene);
            var sourceSettings = new SerializedObject(localSpawner);
            var sourceBoss = (BossMonster)sourceSettings.FindProperty("_bossPrefab").objectReferenceValue;
            var spawnPoint = (Transform)sourceSettings.FindProperty("_spawnPoint").objectReferenceValue;
            int bossId = sourceSettings.FindProperty("_bossId").intValue;

            if (sourceBoss == null || spawnPoint == null || bossId <= 0)
            {
                throw new InvalidOperationException("기존 보스 프리팹·생성 위치·ID 설정 필요");
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath) == null &&
                !AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceBoss), TargetPrefabPath))
            {
                throw new InvalidOperationException("네트워크 보스 프리팹 복제 실패");
            }

            ConfigurePrefab(bossId);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath);

            // 저장된 에셋 GUID를 기준으로 NGO 자체 식별 해시 갱신
            prefab.GetComponent<NetworkObject>().SendMessage("OnValidate", SendMessageOptions.RequireReceiver);
            PrefabUtility.SavePrefabAsset(prefab);
            RegisterPrefab(prefab);

            NetworkBossEncounterController encounter = localSpawner.GetComponent<NetworkBossEncounterController>();
            if (encounter == null)
            {
                encounter = localSpawner.gameObject.AddComponent<NetworkBossEncounterController>();
            }

            var encounterSettings = new SerializedObject(encounter);
            encounterSettings.FindProperty("_bossHud").objectReferenceValue = hud;
            encounterSettings.FindProperty("_bossPrefab").objectReferenceValue = prefab.GetComponent<NetworkBossController>();
            encounterSettings.FindProperty("_spawnPoint").objectReferenceValue = spawnPoint;
            encounterSettings.ApplyModifiedPropertiesWithoutUndo();

            var mapSettings = new SerializedObject(map);
            mapSettings.FindProperty("_networkBossEncounterController").objectReferenceValue = encounter;
            mapSettings.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("BossScene 저장 실패");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TargetPrefabPath, ImportAssetOptions.ForceUpdate);
            ValidatePrefab();
            Debug.Log("NETWORK_BOSS_SETUP_COMPLETE: NetworkBoss.prefab, DefaultNetworkPrefabs, BossScene");
        }
        finally
        {
            if (!Application.isBatchMode)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
        }
    }

    private static void ConfigurePrefab(int bossId)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(TargetPrefabPath);
        try
        {
            root.name = "NetworkBoss";
            BossMonster boss = root.GetComponent<BossMonster>();
            if (boss == null)
            {
                throw new InvalidOperationException("프리팹 루트의 BossMonster 필요");
            }

            foreach (PoolableObject poolable in root.GetComponents<PoolableObject>())
            {
                UnityEngine.Object.DestroyImmediate(poolable);
            }

            NetworkObject networkObject = GetOrAdd<NetworkObject>(root);
            NetworkTransform networkTransform = GetOrAdd<NetworkTransform>(root);
            NetworkBossController controller = GetOrAdd<NetworkBossController>(root);

            var objectSettings = new SerializedObject(networkObject);
            objectSettings.FindProperty("SynchronizeTransform").boolValue = true;
            objectSettings.FindProperty("ActiveSceneSynchronization").boolValue = false;
            objectSettings.ApplyModifiedPropertiesWithoutUndo();

            var transformSettings = new SerializedObject(networkTransform);
            transformSettings.FindProperty("AuthorityMode").intValue = 0;
            transformSettings.FindProperty("InLocalSpace").boolValue = false;
            transformSettings.FindProperty("Interpolate").boolValue = true;
            transformSettings.ApplyModifiedPropertiesWithoutUndo();

            var controllerSettings = new SerializedObject(controller);
            controllerSettings.FindProperty("_boss").objectReferenceValue = boss;
            controllerSettings.FindProperty("_bossId").intValue = bossId;
            controllerSettings.ApplyModifiedPropertiesWithoutUndo();

            // 네트워크 초기화 전의 물리·AI 실행 방지
            boss.Rb.simulated = false;
            root.GetComponent<MonsterFSM>().enabled = false;
            boss.StatusEffects.enabled = false;
            boss.BodyCollider.enabled = false;

            PrefabUtility.SaveAsPrefabAsset(root, TargetPrefabPath, out bool success);
            if (!success)
            {
                throw new InvalidOperationException("네트워크 보스 프리팹 저장 실패");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RegisterPrefab(GameObject prefab)
    {
        var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
        if (list == null)
        {
            throw new InvalidOperationException("기존 NGO 프리팹 목록 필요");
        }

        var settings = new SerializedObject(list);
        SerializedProperty entries = settings.FindProperty("List");
        for (int i = 0; i < entries.arraySize; i++)
        {
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab)
            {
                return;
            }
        }

        int index = entries.arraySize;
        entries.InsertArrayElementAtIndex(index);
        SerializedProperty entry = entries.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("Override").intValue = 0;
        entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null;
        entry.FindPropertyRelative("SourceHashToOverride").longValue = 0;
        entry.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(list);
    }

    private static void ValidatePrefab()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath);
        if (root.GetComponentsInChildren<NetworkObject>(true).Length != 1 ||
            root.GetComponent<NetworkBossController>().Boss != root.GetComponent<BossMonster>() ||
            root.GetComponent<PoolableObject>() != null)
        {
            throw new InvalidOperationException("네트워크 보스 구성 확인 실패");
        }

        var networkSettings = new SerializedObject(root.GetComponent<NetworkObject>());
        if (networkSettings.FindProperty("GlobalObjectIdHash").longValue == 0)
        {
            throw new InvalidOperationException("네트워크 보스 프리팹 식별 해시 생성 필요");
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
            {
                throw new InvalidOperationException($"누락된 스크립트: {child.name}");
            }
        }
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        return target.TryGetComponent(out T component) ? component : target.AddComponent<T>();
    }

    private static T FindSingle<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
    }
}
