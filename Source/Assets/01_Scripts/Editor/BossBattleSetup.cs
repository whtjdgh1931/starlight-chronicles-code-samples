using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 비석 프리팹·서버 플레이어 생명 상태·보스전 UI 구성
/// 카메라 설정 변경 제외
/// </summary>
public static class BossBattleSetup
{
    private const string AssetFolder = "Assets/Effects/Death";
    private const string TombstonePath = AssetFolder + "/Tombstone.prefab";
    private const string PlayerPath = "Assets/Resources/Prefabs/Character/ServerPlayer.prefab";
    private const string BossScenePath = "Assets/00_Scenes/BossScene.unity";
    private const int SafePortalId = 6;

    [MenuItem("Tools/Network/Configure Boss Death And Revive")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 사망·부활 구성 필요");
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                throw new InvalidOperationException("열린 씬 저장 후 사망·부활 구성 필요");
            }
        }
        if (!AssetDatabase.IsValidFolder(AssetFolder))
        {
            AssetDatabase.CreateFolder("Assets/Effects", "Death");
        }
        GameObject tombstone = CreateTombstone();
        ConfigurePlayer(tombstone);
        SceneSetup[] previousScenes = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene scene = EditorSceneManager.OpenScene(BossScenePath, OpenSceneMode.Single);
            NetworkBossBattleController battle = FindOptional<NetworkBossBattleController>(scene);
            if (battle == null)
            {
                battle = CreateBattleScene(scene);
            }
            var encounter = new SerializedObject(FindRequired<NetworkBossEncounterController>(scene));
            encounter.FindProperty("_battle").objectReferenceValue = battle;
            encounter.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("보스전 사망·부활 씬 저장 실패");
            }
        }
        finally
        {
            if (previousScenes.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("BOSS_BATTLE_DEATH_REVIVE_SETUP_OK");
    }

    private static void ConfigurePlayer(GameObject tombstone)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            bool isNew = root.GetComponent<NetworkPlayerLifeController>() == null;
            var life = GetOrAdd<NetworkPlayerLifeController>(root);
            var presentation = GetOrAdd<PlayerDeathPresentation>(root);
            var visualSettings = new SerializedObject(presentation);
            visualSettings.FindProperty("_tombstonePrefab").objectReferenceValue = tombstone;
            if (isNew)
            {
                var body = root.GetComponent<CapsuleCollider2D>();
                visualSettings.FindProperty("_tombstoneOffset").vector3Value =
                    new Vector3(0f, body.offset.y - body.size.y * 0.5f, 0f);
            }
            visualSettings.ApplyModifiedPropertiesWithoutUndo();
            presentation.ValidateConfiguration();
            var lifeSettings = new SerializedObject(life);
            lifeSettings.FindProperty("_presentation").objectReferenceValue = presentation;
            lifeSettings.FindProperty("_groundMask").intValue = 1 << Constants.LAYER_Ground;
            if (isNew)
            {
                // 기존 HeroEditor4D 사망 클립과 비석 낙하 시간
                lifeSettings.FindProperty("_deathAnimationDuration").floatValue = 0.5f;
                lifeSettings.FindProperty("_tombstoneFallDuration").floatValue = 0.5f;
            }
            lifeSettings.ApplyModifiedPropertiesWithoutUndo();
            var controller = new SerializedObject(root.GetComponent<NetworkPlayerController>());
            controller.FindProperty("_life").objectReferenceValue = life;
            controller.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath, out bool success);
            if (!success)
            {
                throw new InvalidOperationException("서버 플레이어 생명 상태 저장 실패");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject CreateTombstone()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(TombstonePath);
        if (existing != null)
        {
            return existing;
        }
        var root = new GameObject("Tombstone", typeof(PoolableObject));
        try
        {
            Material stone = CreateMaterial("TombstoneStone", new Color(0.34f, 0.39f, 0.47f));
            Material mark = CreateMaterial("TombstoneMark", new Color(0.12f, 0.17f, 0.24f));
            CreateMeshPart(root.transform, "Stone", new[]
            {
                new Vector3(-0.42f, 0f), new Vector3(0.42f, 0f),
                new Vector3(0.42f, 0.9f), new Vector3(0.32f, 1.15f),
                new Vector3(0.15f, 1.3f), new Vector3(-0.15f, 1.3f),
                new Vector3(-0.32f, 1.15f), new Vector3(-0.42f, 0.9f)
            }, stone, 20);
            CreateRectangle(root.transform, "CrossVertical", new Vector2(0f, 0.8f), new Vector2(0.07f, 0.36f), mark);
            CreateRectangle(root.transform, "CrossHorizontal", new Vector2(0f, 0.85f), new Vector2(0.3f, 0.07f), mark);
            CreateRectangle(root.transform, "Base", new Vector2(0f, 0.06f), new Vector2(1f, 0.12f), stone);
            return PrefabUtility.SaveAsPrefabAsset(root, TombstonePath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Material CreateMaterial(string name, Color color)
    {
        string path = $"{AssetFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            throw new InvalidOperationException("URP Unlit 셰이더 필요");
        }
        material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void CreateRectangle(Transform parent, string name, Vector2 center, Vector2 size, Material material)
    {
        Vector2 min = center - size * 0.5f;
        Vector2 max = center + size * 0.5f;
        CreateMeshPart(parent, name, new[]
        {
            new Vector3(min.x, min.y, -0.01f), new Vector3(max.x, min.y, -0.01f),
            new Vector3(max.x, max.y, -0.01f), new Vector3(min.x, max.y, -0.01f)
        }, material, 21);
    }

    private static void CreateMeshPart(Transform parent, string name, Vector3[] vertices, Material material, int order)
    {
        string path = $"{AssetFolder}/{name}.asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            var triangles = new List<int>();
            for (int i = 1; i < vertices.Length - 1; i++)
            {
                triangles.Add(0);
                triangles.Add(i + 1);
                triangles.Add(i);
            }
            mesh = new Mesh { name = name, vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
        }
        var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        part.transform.SetParent(parent, false);
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
    }

    private static NetworkBossBattleController CreateBattleScene(Scene scene)
    {
        var stateRoot = new GameObject("NetworkBossBattle", typeof(NetworkObject), typeof(NetworkBossBattleController));
        NetworkObject networkObject = stateRoot.GetComponent<NetworkObject>();
        networkObject.SynchronizeTransform = false;
        var battle = stateRoot.GetComponent<NetworkBossBattleController>();
        Portal safePortal = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Portal portal in root.GetComponentsInChildren<Portal>(true))
            {
                if (portal.PortalID == SafePortalId)
                {
                    safePortal = portal;
                }
            }
        }
        if (safePortal == null)
        {
            throw new MissingReferenceException("보스맵 안전 부활 포털 필요");
        }
        BossBattleView view = CreateView();
        var settings = new SerializedObject(battle);
        settings.FindProperty("_safeRevivePoint").objectReferenceValue = safePortal.transform;
        settings.FindProperty("_view").objectReferenceValue = view;
        settings.ApplyModifiedPropertiesWithoutUndo();
        return battle;
    }

    private static BossBattleView CreateView()
    {
        var root = new GameObject("BossBattleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/07_Font/White.asset");
        var view = root.AddComponent<BossBattleView>();
        RectTransform counter = Panel("DeathCount", root.transform, new Vector2(1f, 1f), new Vector2(-245f, -65f), new Vector2(430f, 65f));
        TextMeshProUGUI count = Text("CountText", counter, font, "남은 부활 기회: 5", 30f);
        RectTransform revive = Panel("RevivePanel", root.transform, new Vector2(0.5f, 0f), new Vector2(0f, 145f), new Vector2(420f, 85f));
        Button button = revive.gameObject.AddComponent<AnimatedButton>();
        button.gameObject.AddComponent<ButtonHoverMotion>();
        button.targetGraphic = revive.GetComponent<Image>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        TextMeshProUGUI buttonText = Text("ReviveText", revive, font, "부활 준비 중", 34f);
        RectTransform gameOver = Panel("GameOverPanel", root.transform, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        gameOver.anchorMin = Vector2.zero;
        gameOver.anchorMax = Vector2.one;
        gameOver.offsetMin = Vector2.zero;
        gameOver.offsetMax = Vector2.zero;
        gameOver.GetComponent<Image>().color = new Color(0.02f, 0.025f, 0.04f, 0.85f);
        TextMeshProUGUI title = Text("GameOverTitle", gameOver, font, "GAME OVER", 72f);
        title.rectTransform.anchorMin = new Vector2(0.1f, 0.5f);
        title.rectTransform.anchorMax = new Vector2(0.9f, 0.7f);
        TextMeshProUGUI explanation = Text("GameOverReason", gameOver, font, "공용 부활 기회를 모두 사용했습니다", 34f);
        explanation.rectTransform.anchorMin = new Vector2(0.1f, 0.3f);
        explanation.rectTransform.anchorMax = new Vector2(0.9f, 0.5f);
        var settings = new SerializedObject(view);
        settings.FindProperty("_counterRoot").objectReferenceValue = counter.gameObject;
        settings.FindProperty("_deathCountText").objectReferenceValue = count;
        settings.FindProperty("_reviveButton").objectReferenceValue = button;
        settings.FindProperty("_reviveButtonText").objectReferenceValue = buttonText;
        settings.FindProperty("_gameOverRoot").objectReferenceValue = gameOver.gameObject;
        settings.ApplyModifiedPropertiesWithoutUndo();
        counter.gameObject.SetActive(false);
        revive.gameObject.SetActive(false);
        gameOver.gameObject.SetActive(false);
        return view;
    }

    private static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        root.GetComponent<Image>().color = new Color(0.08f, 0.13f, 0.2f, 0.9f);
        return rect;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, string text, float size)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        TextMeshProUGUI label = root.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static T GetOrAdd<T>(GameObject root) where T : Component
    {
        T component = root.GetComponent<T>();
        return component != null ? component : root.AddComponent<T>();
    }

    private static T FindOptional<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }
        return null;
    }

    private static T FindRequired<T>(Scene scene) where T : Component
    {
        return FindOptional<T>(scene) ?? throw new MissingReferenceException($"{scene.name}: {typeof(T).Name} 필요");
    }
}
