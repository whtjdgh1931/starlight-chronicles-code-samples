using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 기존 몬스터 색상 변형과 사냥터 5개 스폰 구역 구성
/// </summary>
public static class HuntSpawnSetup
{
    private const string PrefabFolder = "Assets/Resources/Prefabs/Monster";
    private const string VariantFolder = PrefabFolder + "/HuntVariants";
    private const string HuntScenePath = "Assets/00_Scenes/HuntScene.unity";
    private static readonly HuntSpawnZone[] Zones = {
        HuntSpawnZone.WestCenter, HuntSpawnZone.EastCenter, HuntSpawnZone.Lower,
        HuntSpawnZone.WestCanopy, HuntSpawnZone.EastCanopy
    };
    private static readonly string[][] Platforms = {
        new[] { "WesternForestExtension", "WesternClearing", "TreehouseLookout" },
        new[] { "EasternForestExtension", "EasternHuntingClearing", "TimberCrossing" },
        new[] { "LowerWestRest", "LowerEastRest", "LowerForestFloor" },
        new[] { "CanopyWestRest", "CanopyWestUpper", "CanopyBridge" },
        new[] { "CanopyEastLanding", "CanopyEastRest", "CanopyEastUpper" }
    };

    /// <summary>
    /// 저장된 사냥터에 시트 구역과 몬스터 프리팹 연결
    /// 원본 몬스터 프리팹과 다른 씬의 스포너 보존
    /// </summary>
    [MenuItem("Tools/Monsters/Configure Hunt Spawn Groups")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
        {
            throw new InvalidOperationException("플레이 종료와 열린 씬 저장 후 실행 필요");
        }

        if (!AssetDatabase.IsValidFolder(VariantFolder))
        {
            AssetDatabase.CreateFolder(PrefabFolder, "HuntVariants");
        }
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(HuntScenePath);
            foreach (MonsterSpawner oldSpawner in UnityEngine.Object.FindObjectsByType<MonsterSpawner>(FindObjectsInactive.Include))
            {
                oldSpawner.gameObject.SetActive(false);
            }
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "HuntSpawnGroups");
            if (root == null)
            {
                root = new GameObject("HuntSpawnGroups");
            }
            var controller = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<MapMonsterController>());
            ConfigureCatalog(controller);
            SerializedProperty spawners = controller.FindProperty("_spawners");
            spawners.arraySize = Platforms.Length;
            for (int group = 0; group < Platforms.Length; group++)
            {
                string groupName = "HuntGroup_" + (group + 1);
                Transform existing = root.transform.Find(groupName);
                GameObject target = existing == null ? new GameObject(groupName, typeof(MonsterSpawner)) : existing.gameObject;
                target.transform.SetParent(root.transform, false);
                target.SetActive(true);
                var spawner = target.GetComponent<MonsterSpawner>();
                var settings = new SerializedObject(spawner);
                settings.FindProperty("_huntZone").intValue = (int)Zones[group];
                settings.FindProperty("_spawnEdgeMargin").floatValue = 0.75f;
                SerializedProperty platforms = settings.FindProperty("_spawnPlatforms");
                platforms.arraySize = Platforms[group].Length;
                for (int index = 0; index < Platforms[group].Length; index++)
                {
                    Collider2D platform = GameObject.Find(Platforms[group][index]).GetComponentInChildren<Collider2D>();
                    platforms.GetArrayElementAtIndex(index).objectReferenceValue = platform;
                    if (index == 0)
                    {
                        target.transform.position = platform.bounds.center + Vector3.up;
                    }
                }
                settings.ApplyModifiedPropertiesWithoutUndo();
                spawners.GetArrayElementAtIndex(group).objectReferenceValue = spawner;
            }
            controller.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("사냥터 스폰 구역 저장 실패");
            }
            AssetDatabase.SaveAssets();
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
        Debug.Log("HUNT_SPAWN_GROUPS_READY: zone and prefab bindings");
    }

    private static void ConfigureCatalog(SerializedObject controller)
    {
        var ids = new LocalGameDataProvider().CreateHuntSpawnRows()
            .Select(rule => rule.MonsterID).Distinct().OrderBy(id => id).ToArray();
        SerializedProperty catalog = controller.FindProperty("_huntMonsterPrefabs");
        var registered = new HashSet<int>();
        for (int i = 0; i < catalog.arraySize; i++)
        {
            SerializedProperty entry = catalog.GetArrayElementAtIndex(i);
            int id = entry.FindPropertyRelative("MonsterID").intValue;
            if (id <= 0 || entry.FindPropertyRelative("Prefab").objectReferenceValue == null || !registered.Add(id))
            {
                throw new InvalidOperationException($"사냥터 몬스터 {id}: 기존 프리팹 연결 확인 필요");
            }
        }

        foreach (int id in ids)
        {
            if (registered.Contains(id)) { continue; }
            MonsterBase prefab = LoadVariant(id);
            if (prefab == null)
            {
                CreateVariant(id);
                prefab = LoadVariant(id);
            }
            if (prefab == null) { throw new MissingReferenceException($"사냥터 몬스터 {id}: 프리팹 필요"); }

            int index = catalog.arraySize;
            catalog.InsertArrayElementAtIndex(index);
            SerializedProperty entry = catalog.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("MonsterID").intValue = id;
            entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        }
    }

    private static MonsterBase LoadVariant(int monsterID)
    {
        string path = monsterID switch
        {
            1013 => PrefabFolder + "/MonsterArcher.prefab",
            1014 => PrefabFolder + "/MonsterSword.prefab",
            _ => VariantFolder + "/HuntMonster_" + monsterID + ".prefab"
        };
        return AssetDatabase.LoadAssetAtPath<MonsterBase>(path);
    }

    /// <summary>
    /// 기존 외형과 애니메이션을 재사용하고 종류별 색상 분리
    /// 원본과 몬스터 ID가 다른 능동 공격은 제외하고 접촉 피해 사용
    /// </summary>
    private static void CreateVariant(int monsterID)
    {
        string source = monsterID switch
        {
            1021 => "ForestSlime",
            1022 => "ThornMushroom",
            1023 or 1006 or 1011 => "ForestWolf",
            1010 or 1012 => "MonsterWarrior",
            1005 or 1007 or 1008 or 1009 => "ThornMushroom",
            _ => "Slime"
        };
        Color tint = monsterID switch
        {
            1001 => new Color(1f, 0.85f, 0.55f),
            1002 => new Color(0.45f, 0.7f, 1f),
            1003 => new Color(1f, 0.45f, 0.45f),
            1021 => new Color(0.65f, 1f, 0.65f),
            1004 => new Color(0.55f, 0.9f, 1f),
            1005 => new Color(1f, 0.65f, 0.3f),
            1022 => new Color(0.9f, 0.65f, 1f),
            1006 => new Color(1f, 0.6f, 0.75f),
            1012 => new Color(0.7f, 0.85f, 0.45f),
            1023 => new Color(0.75f, 0.85f, 1f),
            1007 => new Color(0.35f, 1f, 0.45f),
            1008 => new Color(0.7f, 0.45f, 1f),
            1009 => new Color(0.5f, 0.7f, 0.6f),
            1010 => new Color(0.8f, 0.6f, 0.4f),
            1011 => new Color(1f, 0.35f, 0.2f),
            _ => Color.white
        };
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabFolder + "/" + source + ".prefab");
        try
        {
            contents.name = "HuntMonster_" + monsterID;
            foreach (SpriteRenderer renderer in contents.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.color *= tint;
            }
            foreach (MonsterAttackBehaviour attack in contents.GetComponentsInChildren<MonsterAttackBehaviour>(true))
            {
                var attackSettings = new SerializedObject(attack);
                int patternID = attackSettings.FindProperty("_patternId").intValue;
                if (patternID / 100 != monsterID)
                {
                    var monsterSettings = new SerializedObject(contents.GetComponent<MonsterBase>());
                    monsterSettings.FindProperty("_attackBehaviour").objectReferenceValue = null;
                    monsterSettings.ApplyModifiedPropertiesWithoutUndo();
                    UnityEngine.Object.DestroyImmediate(attack);
                }
            }
            PrefabUtility.SaveAsPrefabAsset(contents, VariantFolder + "/HuntMonster_" + monsterID + ".prefab");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}
