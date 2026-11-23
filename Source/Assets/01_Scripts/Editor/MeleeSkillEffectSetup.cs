using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 범위 베기와 돌진 잔상 프리팹 제작 및 기존 캐릭터 연결
/// 검기 메시·단발 재생 기능을 공유하는 편집기 제작 도구
/// </summary>
public static class MeleeSkillEffectSetup
{
    private const string EffectRoot = "Assets/Effects/MeleeSkills";
    private const string SwordRoot = "Assets/Effects/SwordWave";
    private const string SlashAudio = "Assets/99_Assets/Leohpaz/RPG_Essentials_Free/10_Battle_SFX/22_Slash_04.wav";
    private const string PreviewPath = "docs/Previews/MeleeSkills.png";
    private static readonly Color Cyan = new(0.25f, 0.8f, 1f, 0.7f);

    [MenuItem("Tools/Effects/Configure Melee Skill Effects")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 근접 스킬 연출 구성 필요");
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                throw new InvalidOperationException("열린 씬 저장 후 연출 구성 필요");
            }
        }
        Directory.CreateDirectory(EffectRoot);
        AssetDatabase.Refresh();
        AreaSlashEffect slash = CreateSlash();
        SwordWaveBurst dash = CreateDashStart();
        foreach (string name in new[] { "ServerPlayer", "RemotePlayer" })
        {
            string path = $"Assets/Resources/Prefabs/Character/{name}.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Connect(root, slash, dash);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        SceneSetup[] previousScenes = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/00_Scenes/HomeScene.unity", OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (PlayerAreaSkillExecutor executor in root.GetComponentsInChildren<PlayerAreaSkillExecutor>(true))
                {
                    // 프리팹에 연결된 표현 담당자는 중복 추가 제외
                    if (executor.GetComponent<AreaSkillPresentation>() == null)
                    {
                        Connect(executor.gameObject, slash, dash);
                        EditorSceneManager.MarkSceneDirty(scene);
                    }
                }
            }
            if (scene.isDirty && !EditorSceneManager.SaveScene(scene))
            {
                throw new IOException("HomeScene 연출 연결 저장 실패");
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
        Validate();
        RenderPreview();
        Debug.Log("MELEE_EFFECT_SETUP_OK: area slash, dash trails and player references configured");
    }

    private static void Connect(GameObject root, AreaSlashEffect slash, SwordWaveBurst dash)
    {
        PlayerAreaSkillExecutor areaExecutor = root.GetComponent<PlayerAreaSkillExecutor>();
        PlayerDashSkillExecutor dashExecutor = root.GetComponent<PlayerDashSkillExecutor>();
        PlayerAreaHitResolver resolver = root.GetComponent<PlayerAreaHitResolver>();
        if (areaExecutor == null || dashExecutor == null || resolver == null)
        {
            throw new InvalidOperationException($"{root.name}: 근접 스킬 실행기와 공통 판정 담당자 필요");
        }
        AreaSkillPresentation area = root.GetComponent<AreaSkillPresentation>();
        if (area == null)
        {
            area = root.AddComponent<AreaSkillPresentation>();
        }
        Set(area, "_executor", areaExecutor);
        Set(area, "_slashPrefab", slash);
        DashSkillPresentation dashPresentation = root.GetComponent<DashSkillPresentation>();
        if (dashPresentation == null)
        {
            dashPresentation = root.AddComponent<DashSkillPresentation>();
        }
        Transform previous = root.transform.Find("DashEffects");
        if (previous != null)
        {
            Object.DestroyImmediate(previous.gameObject);
        }
        var visual = new GameObject("DashEffects");
        visual.transform.SetParent(root.transform, false);
        TrailRenderer[] trails = CreateTrails(visual.transform);
        ParticleSystem particles = CreateDashParticles(visual.transform);
        Set(dashPresentation, "_executor", dashExecutor);
        Set(dashPresentation, "_playerVisual", root.GetComponent<PlayerVisual>());
        SetArray(dashPresentation, "_trails", trails);
        Set(dashPresentation, "_particles", particles);
        Set(dashPresentation, "_startPrefab", dash);
    }

    private static void Validate()
    {
        foreach (string name in new[] { "ServerPlayer", "RemotePlayer" })
        {
            GameObject root = Load<GameObject>($"Assets/Resources/Prefabs/Character/{name}.prefab");
            foreach (MonoBehaviour component in new MonoBehaviour[]
            {
                root.GetComponent<AreaSkillPresentation>(), root.GetComponent<DashSkillPresentation>()
            })
            {
                if (component == null)
                {
                    throw new InvalidOperationException($"{name}: 연출 담당자 누락");
                }
                var serialized = new SerializedObject(component);
                SerializedProperty field = serialized.GetIterator();
                while (field.NextVisible(true))
                {
                    if (field.propertyType == SerializedPropertyType.ObjectReference && field.objectReferenceValue == null)
                    {
                        throw new InvalidOperationException($"{name}: {field.propertyPath} 참조 누락");
                    }
                }
            }
            if (root.GetComponentsInChildren<TrailRenderer>(true).Length < 3)
            {
                throw new InvalidOperationException($"{name}: 돌진 잔상 누락");
            }
        }
    }

    private static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            throw new FileNotFoundException($"연출 제작 에셋 누락: {path}");
        }
        return asset;
    }

    private static void Set(Object owner, string field, Object value)
    {
        var serialized = new SerializedObject(owner);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(Object owner, string field, Object[] values)
    {
        var serialized = new SerializedObject(owner);
        SerializedProperty property = serialized.FindProperty(field);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static AreaSlashEffect CreateSlash()
    {
        GameObject root = Object.Instantiate(Load<GameObject>($"{SwordRoot}/SwordWaveLaunch.prefab"));
        root.name = "AreaSlash";
        root.SetActive(false);
        try
        {
            root.GetComponent<AudioSource>().volume = GeneratedCombatEffectSetup.AreaVolume;
            SwordWaveBurst burst = root.GetComponent<SwordWaveBurst>();
            Set(burst, "_clip", Load<AudioClip>(SlashAudio));
            var visual = new GameObject("SlashVisual");
            visual.transform.SetParent(root.transform, false);
            var renderers = new List<MeshRenderer>();
            Material material = Load<Material>($"{SwordRoot}/FantasyAdditive.mat");
            foreach (string part in new[] { "Glow", "Edge", "Core" })
            {
                var child = new GameObject(part);
                child.transform.SetParent(visual.transform, false);
                Mesh mesh = Load<Mesh>($"{SwordRoot}/SwordWave{part}.asset");
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.sortingOrder = 20 + renderers.Count;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderers.Add(renderer);
            }
            Mesh baseMesh = Load<Mesh>($"{SwordRoot}/SwordWaveGlow.asset");
            // 공격 중심에 맞춘 메시 원점 보정
            foreach (Transform child in visual.transform)
            {
                child.localPosition = -baseMesh.bounds.center;
            }
            AreaSlashEffect slash = root.AddComponent<AreaSlashEffect>();
            Set(slash, "_burst", burst);
            Set(slash, "_visual", visual.transform);
            SetArray(slash, "_renderers", renderers.ToArray());
            var settings = new SerializedObject(slash);
            settings.FindProperty("_meshSize").vector2Value = baseMesh.bounds.size;
            settings.FindProperty("_fadeDuration").floatValue = GeneratedCombatEffectSetup.AreaFadeDuration;
            settings.FindProperty("_rangeCoverage").floatValue = GeneratedCombatEffectSetup.AreaRangeCoverage;
            settings.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            return PrefabUtility.SaveAsPrefabAsset(root, $"{EffectRoot}/AreaSlash.prefab").GetComponent<AreaSlashEffect>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static SwordWaveBurst CreateDashStart()
    {
        GameObject root = Object.Instantiate(Load<GameObject>($"{SwordRoot}/SwordWaveLaunch.prefab"));
        root.name = "DashStart";
        try
        {
            root.GetComponent<AudioSource>().volume = GeneratedCombatEffectSetup.DashVolume;
            foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule main = particles.main;
                main.startSizeMultiplier *= 1.35f;
            }
            return PrefabUtility.SaveAsPrefabAsset(root, $"{EffectRoot}/DashStart.prefab").GetComponent<SwordWaveBurst>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static TrailRenderer[] CreateTrails(Transform parent)
    {
        var trails = new TrailRenderer[3];
        for (int i = 0; i < trails.Length; i++)
        {
            var child = new GameObject($"Trail{i + 1}");
            child.transform.SetParent(parent, false);
            child.transform.localPosition = new Vector3(0f, (i - 1) * 0.23f, 0f);
            TrailRenderer trail = child.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Load<Material>($"{SwordRoot}/FantasyAdditive.mat");
            trail.time = 0.16f;
            trail.minVertexDistance = 0.05f;
            trail.startWidth = i == 1 ? 0.18f : 0.08f;
            trail.endWidth = 0f;
            trail.startColor = i == 1 ? new Color(0.8f, 0.95f, 1f, 0.7f) : Cyan;
            trail.endColor = new Color(0.1f, 0.5f, 1f, 0f);
            trail.numCornerVertices = 3;
            trail.numCapVertices = 3;
            trail.emitting = false;
            trail.sortingOrder = 18;
            trails[i] = trail;
        }
        return trails;
    }

    private static ParticleSystem CreateDashParticles(Transform parent)
    {
        var child = new GameObject("DashSparks");
        child.transform.SetParent(parent, false);
        ParticleSystem particles = child.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 0.18f;
        main.startSpeed = 0.15f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        main.startColor = Cyan;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 32;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 30f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.35f;
        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Cyan, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = Load<Mesh>($"{SwordRoot}/SwordWaveSpark.asset");
        renderer.enableGPUInstancing = false;
        renderer.sharedMaterial = Load<Material>($"{SwordRoot}/FantasySpark.mat");
        renderer.sortingOrder = 19;
        return particles;
    }

    /// <summary>
    /// 별도 미리보기 씬에서 베기·돌진 잔상 형태 렌더링
    /// </summary>
    private static void RenderPreview()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        Texture2D image = null;
        RenderTexture previous = RenderTexture.active;
        var previewMeshes = new List<Mesh>();
        try
        {
            GameObject slash = Object.Instantiate(Load<GameObject>($"{EffectRoot}/AreaSlash.prefab"));
            SceneManager.MoveGameObjectToScene(slash, scene);
            slash.transform.position = new Vector3(-1.35f, 0f, 0f);
            Transform visual = slash.transform.Find("SlashVisual");
            visual.localScale = new Vector3(4f, 1.25f, 1f);
            visual.localRotation = Quaternion.Euler(0f, 0f, -25f);
            var dash = new GameObject("DashPreview");
            SceneManager.MoveGameObjectToScene(dash, scene);
            foreach (TrailRenderer trail in CreateTrails(dash.transform))
            {
                trail.time = 10000f;
                float y = trail.transform.localPosition.y;
                trail.AddPositions(new[] { new Vector3(0.25f, y - 0.1f, 0f), new Vector3(0.9f, y, 0f), new Vector3(1.8f, y + 0.1f, 0f) });
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = 31;
                }
            }
            var cameraObject = new GameObject("PreviewCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 1.25f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.04f, 0.08f, 1f);
            camera.cullingMask = 1 << 31;
            target = new RenderTexture(960, 540, 24);
            camera.targetTexture = target;
            foreach (TrailRenderer trail in dash.GetComponentsInChildren<TrailRenderer>())
            {
                // 편집 모드에서는 잔상 경로를 정적 리본 메시로 표시
                float y = trail.transform.localPosition.y;
                float width = trail.startWidth * 0.5f;
                var mesh = new Mesh
                {
                    vertices = new[]
                    {
                        new Vector3(0.25f, y - 0.1f, 0f), new Vector3(0.25f, y - 0.1f, 0f),
                        new Vector3(0.9f, y - width * 0.5f, 0f), new Vector3(0.9f, y + width * 0.5f, 0f),
                        new Vector3(1.8f, y + 0.1f - width, 0f), new Vector3(1.8f, y + 0.1f + width, 0f)
                    },
                    colors = new[]
                    {
                        new Color(0.1f, 0.5f, 1f, 0f), new Color(0.1f, 0.5f, 1f, 0f),
                        Cyan, Cyan, new Color(0.8f, 0.95f, 1f, 0.85f), new Color(0.8f, 0.95f, 1f, 0.85f)
                    },
                    triangles = new[] { 0, 2, 1, 1, 2, 3, 2, 4, 3, 3, 4, 5 }
                };
                previewMeshes.Add(mesh);
                var ribbon = new GameObject("BakedTrailPreview");
                ribbon.transform.SetParent(dash.transform, false);
                ribbon.layer = 31;
                ribbon.AddComponent<MeshFilter>().sharedMesh = mesh;
                ribbon.AddComponent<MeshRenderer>().sharedMaterial = trail.sharedMaterial;
            }
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            image.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath));
            File.WriteAllBytes(PreviewPath, image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (image != null)
            {
                Object.DestroyImmediate(image);
            }
            if (target != null)
            {
                target.Release();
                Object.DestroyImmediate(target);
            }
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (Mesh mesh in previewMeshes)
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
