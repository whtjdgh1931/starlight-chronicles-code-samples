using System;
using UnityEditor;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// 기존 검기 외형을 유지하는 NGO 검기 프리팹 구성과 서버 플레이어 연결
/// </summary>
public static class NetworkPlayerProjectileSetup
{
    private const string SourcePath = "Assets/Resources/Prefabs/Character/Skill/PlayerSwordWave.prefab";
    private const string TargetPath = "Assets/Resources/Prefabs/Character/Skill/NetworkSwordWave.prefab";
    private const string PlayerPath = "Assets/Resources/Prefabs/Character/ServerPlayer.prefab";

    [MenuItem("Tools/Network/Configure Player Projectile")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 검기 구성 필요");
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath) == null &&
            !AssetDatabase.CopyAsset(SourcePath, TargetPath))
        {
            throw new InvalidOperationException("검기 프리팹 복사 실패");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(TargetPath);
        try
        {
            root.name = "NetworkSwordWave";
            SwordWavePresentation presentation = root.GetComponent<SwordWavePresentation>();
            var visual = new SerializedObject(presentation);
            visual.FindProperty("_projectile").objectReferenceValue = null;
            visual.ApplyModifiedPropertiesWithoutUndo();
            Remove<PlayerProjectile>(root);
            Remove<ProjectileMotion>(root);
            Remove<PoolableObject>(root);

            GetOrAdd<NetworkObject>(root);
            NetworkTransform networkTransform = GetOrAdd<NetworkTransform>(root);
            var transformSettings = new SerializedObject(networkTransform);
            transformSettings.FindProperty("AuthorityMode").intValue = 0;
            transformSettings.FindProperty("InLocalSpace").boolValue = false;
            transformSettings.FindProperty("Interpolate").boolValue = true;
            transformSettings.ApplyModifiedPropertiesWithoutUndo();
            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            body.simulated = false;
            NetworkPlayerProjectile projectile = GetOrAdd<NetworkPlayerProjectile>(root);
            var settings = new SerializedObject(projectile);
            settings.FindProperty("_body").objectReferenceValue = body;
            settings.FindProperty("_presentation").objectReferenceValue = presentation;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Save(root, TargetPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath);
        Register(prefab);
        root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            var executor = new SerializedObject(root.GetComponent<PlayerProjectileSkillExecutor>());
            executor.FindProperty("_networkProjectilePrefab").objectReferenceValue = prefab.GetComponent<NetworkPlayerProjectile>();
            executor.ApplyModifiedPropertiesWithoutUndo();
            Save(root, PlayerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        ConfigureBossProjectile();
        AssetDatabase.SaveAssets();
        Debug.Log("NETWORK_PLAYER_PROJECTILE_SETUP_OK");
    }

    private static void ConfigureBossProjectile()
    {
        const string path = "Assets/Resources/Arrow/NetworkBossArrow.prefab";
        const string bossPath = "Assets/Resources/Prefabs/Monster/NetworkBoss.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null &&
            !AssetDatabase.CopyAsset("Assets/Resources/Arrow/MonsterArrowPrefab.prefab", path))
        {
            throw new InvalidOperationException("보스 투사체 복사 실패");
        }
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = "NetworkBossArrow";
            Remove<MonsterProjectile>(root);
            Remove<ProjectileMotion>(root);
            Remove<PoolableObject>(root);
            GetOrAdd<NetworkObject>(root);
            NetworkTransform networkTransform = GetOrAdd<NetworkTransform>(root);
            var transformSettings = new SerializedObject(networkTransform);
            transformSettings.FindProperty("AuthorityMode").intValue = 0;
            transformSettings.FindProperty("Interpolate").boolValue = true;
            transformSettings.ApplyModifiedPropertiesWithoutUndo();
            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            body.simulated = false;
            var settings = new SerializedObject(GetOrAdd<NetworkBossProjectile>(root));
            settings.FindProperty("_body").objectReferenceValue = body;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Save(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Register(prefab);
        root = PrefabUtility.LoadPrefabContents(bossPath);
        try
        {
            var emitter = new SerializedObject(root.GetComponentInChildren<BossFanProjectileEmitter>(true));
            emitter.FindProperty("_networkProjectilePrefab").objectReferenceValue = prefab.GetComponent<NetworkBossProjectile>();
            emitter.ApplyModifiedPropertiesWithoutUndo();
            Save(root, bossPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Register(GameObject prefab)
    {
        var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
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

    private static T GetOrAdd<T>(GameObject root) where T : Component
    {
        T component = root.GetComponent<T>();
        return component != null ? component : root.AddComponent<T>();
    }

    private static void Remove<T>(GameObject root) where T : Component
    {
        T component = root.GetComponent<T>();
        if (component != null)
        {
            UnityEngine.Object.DestroyImmediate(component);
        }
    }

    private static void Save(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
        if (!success)
        {
            throw new InvalidOperationException($"프리팹 저장 실패: {path}");
        }
    }
}
