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
/// 청백색 검기 메시·파티클·효과음 프리팹 제작 및 기존 투사체 연결
/// 콜라이더와 피해 설정을 보존하는 재실행 가능한 편집기 도구
/// </summary>
public static class SwordWaveEffectSetup
{
    private const string AssetRoot = "Assets/Effects/SwordWave";
    private const string ProjectilePath = "Assets/Resources/Prefabs/Character/Skill/PlayerSwordWave.prefab";
    private const string PreviewPath = "Docs/Previews/SwordWave.png";
    private static readonly Color Cyan = new(0.18f, 0.72f, 1f, 0.65f);

    [MenuItem("Tools/Effects/Configure Fantasy Sword Wave")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 검기 에셋 구성 필요");
        }

        AssetDatabase.Refresh();
        Shader shader = Shader.Find("ProjectMaple/Effects/FantasyAdditive");
        if (shader == null || ShaderUtil.ShaderHasError(shader))
        {
            throw new InvalidOperationException("검기 전용 셰이더 임포트 오류");
        }

        Material material = LoadOrCreateMaterial(shader);
        Material particlesMaterial = LoadOrCreateMaterial(shader, "FantasySpark", false);
        Mesh spark = CreateSpark();
        AudioClip cast = ImportAudio("SwordWaveCast");
        AudioClip impact = ImportAudio("SwordWaveImpact");
        SwordWaveBurst castPrefab = CreateBurst("SwordWaveLaunch", particlesMaterial, spark, cast, false);
        CreateBurst("SwordWaveHit", particlesMaterial, spark, impact, true);
        ConfigureProjectile(material, particlesMaterial, spark, castPrefab);
        AssetDatabase.SaveAssets();
        ValidateAssets();
        RenderPreview();
        Debug.Log("SWORD_WAVE_SETUP_OK: prefab, mesh, particles, audio and reference validation complete");
    }

    private static void ConfigureProjectile(Material material, Material particlesMaterial, Mesh spark, SwordWaveBurst launch)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ProjectilePath);
        try
        {
            root.SetActive(false);
            BoxCollider2D collider = root.GetComponent<BoxCollider2D>();
            Vector2 originalSize = collider.size;
            Vector2 originalOffset = collider.offset;
            root.transform.Find("PlayerSwordWaveVisual").gameObject.SetActive(false);
            Transform previousVisual = root.transform.Find("FantasySwordWave");
            if (previousVisual != null) { Object.DestroyImmediate(previousVisual.gameObject); }
            var visual = new GameObject("FantasySwordWave");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = collider.offset;
            float halfHeight = collider.size.y * 0.5f;
            float halfWidth = collider.size.x * 0.5f;
            AddMesh(visual.transform, "Glow", material,
                CreateCrescent("SwordWaveGlow", halfWidth, halfHeight, halfWidth * 1.7f, new Color(0.04f, 0.38f, 1f, 0.50f)), 8);
            AddMesh(visual.transform, "Edge", material,
                CreateCrescent("SwordWaveEdge", halfWidth * 0.97f, halfHeight * 0.98f, halfWidth * 0.65f, Cyan), 9);
            AddMesh(visual.transform, "Core", material,
                CreateCrescent("SwordWaveCore", halfWidth * 0.95f, halfHeight * 0.96f, halfWidth * 0.20f, new Color(0.88f, 0.97f, 1f, 1f)), 10);
            var trailObject = new GameObject("Trail");
            trailObject.transform.SetParent(visual.transform, false);
            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = 0.13f;
            trail.minVertexDistance = 0.04f;
            trail.startWidth = halfHeight * 0.34f;
            trail.endWidth = 0f;
            trail.startColor = new Color(0.15f, 0.65f, 1f, 0.45f);
            trail.endColor = new Color(0.05f, 0.3f, 1f, 0f);
            trail.emitting = false;
            trail.sortingOrder = 7;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            var dust = new GameObject("FlightParticles");
            dust.transform.SetParent(visual.transform, false);
            ParticleSystem flight = AddParticles(dust, particlesMaterial, spark, true, 24, 0.24f, 0.15f, 0.06f);
            SwordWavePresentation presenter = root.GetComponent<SwordWavePresentation>();
            if (presenter == null) { presenter = root.AddComponent<SwordWavePresentation>(); }
            Set(presenter, "_projectile", root.GetComponent<PlayerProjectile>());
            Set(presenter, "_trail", trail);
            Set(presenter, "_flightParticles", flight);
            Set(presenter, "_launchPrefab", launch);
            if (collider.size != originalSize || collider.offset != originalOffset)
            {
                throw new InvalidOperationException("검기 피해 범위 변경 감지");
            }
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, ProjectilePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ValidateAssets()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath);
        var presenter = new SerializedObject(prefab.GetComponent<SwordWavePresentation>());
        foreach (string name in new[] { "_projectile", "_trail", "_flightParticles", "_launchPrefab" })
        {
            if (presenter.FindProperty(name).objectReferenceValue == null)
            {
                throw new InvalidOperationException($"검기 연출 참조 누락: {name}");
            }
        }
        foreach (string name in new[] { "SwordWaveLaunch", "SwordWaveHit" })
        {
            var burst = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>($"{AssetRoot}/{name}.prefab").GetComponent<SwordWaveBurst>());
            foreach (string field in new[] { "_particles", "_audioSource", "_clip", "_poolableObject" })
            {
                if (burst.FindProperty(field).objectReferenceValue == null)
                {
                    throw new InvalidOperationException($"검기 단발 효과 참조 누락: {name}.{field}");
                }
            }
        }
    }

    private static Material LoadOrCreateMaterial(Shader shader, string name = "FantasyAdditive", bool useVertexColor = true)
    {
        string path = $"{AssetRoot}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_Tint", useVertexColor ? Color.white : new Color(2.6f, 3.6f, 4f, 1f));
        material.SetFloat("_UseVertexColor", useVertexColor ? 1f : 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static AudioClip ImportAudio(string name)
    {
        string path = $"{AssetRoot}/Audio/{name}.wav";
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        importer.forceToMono = true;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.preloadAudioData = true;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    /// <summary>
    /// 기존 메시 에셋은 GUID를 유지하며 갱신
    /// </summary>
    private static Mesh SaveMesh(Mesh mesh, string name)
    {
        mesh.name = name;
        mesh.RecalculateBounds();
        string path = $"{AssetRoot}/{name}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        EditorUtility.CopySerialized(mesh, existing);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static Mesh CreateCrescent(string name, float width, float height, float thickness, Color color)
    {
        const int segments = 40;
        var vertices = new Vector3[(segments + 1) * 2];
        var colors = new Color[vertices.Length];
        var triangles = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, (float)i / segments);
            float taper = Mathf.Max(0f, Mathf.Cos(angle));
            float x = width * taper;
            float y = height * Mathf.Sin(angle);
            vertices[i * 2] = new Vector3(x, y, 0f);
            vertices[i * 2 + 1] = new Vector3(x - thickness * Mathf.Pow(taper, 1.3f), y, 0f);
            colors[i * 2] = new Color(color.r, color.g, color.b, color.a * taper);
            colors[i * 2 + 1] = new Color(color.r, color.g, color.b, 0f);
            if (i == segments) { continue; }
            int v = i * 2;
            int t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }

        var mesh = new Mesh { vertices = vertices, colors = colors, triangles = triangles };
        return SaveMesh(mesh, name);
    }

    private static Mesh CreateSpark()
    {
        const int points = 8;
        var vertices = new Vector3[points + 1];
        var colors = new Color[points + 1];
        var triangles = new int[points * 3];
        colors[0] = Color.white;
        for (int i = 0; i < points; i++)
        {
            float angle = i * Mathf.PI / 4f;
            float radius = i % 2 == 0 ? 0.5f : 0.10f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            colors[i + 1] = new Color(1f, 1f, 1f, 0f);
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % points + 1;
        }
        return SaveMesh(new Mesh { vertices = vertices, colors = colors, triangles = triangles }, "SwordWaveSpark");
    }

    private static ParticleSystem AddParticles(
        GameObject owner, Material material, Mesh mesh, bool loop, int count,
        float lifetime, float speed, float size)
    {
        ParticleSystem particles = owner.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = loop;
        main.duration = 0.6f;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.65f, lifetime);
        main.startSpeed = speed;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(Cyan, Color.white);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;
        particles.useAutoRandomSeed = false;
        particles.randomSeed = 1031;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = loop ? count : 0;
        if (!loop) { emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) }); }
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.06f;
        ParticleSystem.SizeOverLifetimeModule sizes = particles.sizeOverLifetime;
        sizes.enabled = true;
        sizes.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        ParticleSystem.ColorOverLifetimeModule colors = particles.colorOverLifetime;
        colors.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Cyan, 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
        colors.color = fade;
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = mesh;
        renderer.enableGPUInstancing = false;
        renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position,
            ParticleSystemVertexStream.Color
        });
        renderer.sharedMaterial = material;
        renderer.sortingOrder = 12;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return particles;
    }

    private static SwordWaveBurst CreateBurst(string name, Material material, Mesh spark, AudioClip clip, bool impact)
    {
        var root = new GameObject(name);
        root.SetActive(false);
        try
        {
            ParticleSystem particles = AddParticles(root, material, spark, false, impact ? 14 : 8,
                impact ? 0.34f : 0.22f, impact ? 2.2f : 1.5f, 0.16f);
            var flash = new GameObject("Flash");
            flash.transform.SetParent(root.transform, false);
            AddParticles(flash, material, spark, false, 1, 0.14f, 0f, impact ? 0.9f : 0.65f);
            AudioSource audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            AudioSettingsSetup.AssignOutput(audio);
            audio.loop = false;
            audio.volume = impact ? GeneratedCombatEffectSetup.ImpactVolume : GeneratedCombatEffectSetup.CastVolume;
            audio.spatialBlend = 0.5f;
            audio.dopplerLevel = 0f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = 2f;
            audio.maxDistance = 18f;
            audio.priority = 140;
            PoolableObject poolable = root.AddComponent<PoolableObject>();
            SwordWaveBurst burst = root.AddComponent<SwordWaveBurst>();
            Set(burst, "_particles", particles);
            Set(burst, "_audioSource", audio);
            Set(burst, "_clip", clip);
            Set(burst, "_poolableObject", poolable);
            root.SetActive(true);
            return PrefabUtility.SaveAsPrefabAsset(root, $"{AssetRoot}/{name}.prefab").GetComponent<SwordWaveBurst>();
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void AddMesh(Transform parent, string name, Material material, Mesh mesh, int order)
    {
        var part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static void Set(Object owner, string name, Object value)
    {
        var settings = new SerializedObject(owner);
        settings.FindProperty(name).objectReferenceValue = value;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 사용자 씬 저장 없이 별도 씬에서 제작 결과 렌더링
    /// </summary>
    private static void RenderPreview()
    {
        Scene previewScene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        Texture2D image = null;
        RenderTexture previousTarget = RenderTexture.active;
        try
        {
            GameObject sword = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath));
            SceneManager.MoveGameObjectToScene(sword, previewScene);
            sword.transform.position = new Vector3(-0.7f, 0f, 0f);
            GameObject hit = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"{AssetRoot}/SwordWaveHit.prefab"));
            SceneManager.MoveGameObjectToScene(hit, previewScene);
            hit.transform.position = new Vector3(0.7f, 0f, 0f);
            foreach (GameObject root in previewScene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) { child.gameObject.layer = 31; }
            }
            foreach (ParticleSystem particles in hit.GetComponentsInChildren<ParticleSystem>())
            {
                particles.Simulate(0.055f, false, true);
            }
            var cameraObject = new GameObject("PreviewCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.scene = previewScene;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 1.05f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.04f, 0.08f, 1f);
            camera.cullingMask = 1 << 31;
            target = new RenderTexture(960, 540, 24);
            camera.targetTexture = target;
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
            RenderTexture.active = previousTarget;
            if (image != null) { Object.DestroyImmediate(image); }
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }
}
