using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 생성 이미지의 스프라이트 가져오기와 기존 전투 연출 연결
/// Unity API 기반의 프리팹·보스전 UI 구성
/// </summary>
public static class GeneratedCombatEffectSetup
{
    public const float CastVolume = 0.24f;
    public const float ImpactVolume = 0.30f;
    public const float AreaVolume = 0.16f;
    public const float DashVolume = 0.22f;
    public const float AreaFadeDuration = 0.2f;
    public const float AreaRangeCoverage = 0.94f;
    private const float ImpactStartSize = 0.65f;
    private const float ImpactEndSize = 0.85f;
    private const float ImpactFlashDuration = 0.12f;
    private const float AreaShockwaveDuration = 0.16f;

    private const string EffectRoot = "Assets/Effects/GeneratedCombat";
    private const string ImageRoot = EffectRoot + "/Images";
    private const string AudioRoot = EffectRoot + "/Audio";
    private const string PlayerPath = "Assets/Resources/Prefabs/Character/ServerPlayer.prefab";
    private const string BossScenePath = "Assets/00_Scenes/BossScene.unity";
    private const string ReviveAudio = "Assets/99_Assets/Leohpaz/RPG_Essentials_Free/8_Buffs_Heals_SFX/30_Revive_03.wav";

    [MenuItem("Tools/Effects/Connect Generated Combat Effects")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 생성 이미지 연결 필요");
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                throw new InvalidOperationException("열린 씬 저장 후 생성 이미지 연결 필요");
            }
        }
        AssetDatabase.Refresh();
        Sprite hit = ImportSprite("HitFlash");
        Sprite ring = ImportSprite("ShockwaveRing");
        Sprite dust = ImportSprite("TombstoneDust");
        Sprite revive = ImportSprite("ReviveAura");
        Sprite sparkle = ImportSprite("HealBuffParticle");
        Sprite danger = ImportSprite("BossDangerZone");
        foreach (string name in new[] { "TombstoneLand", "Victory", "Defeat" })
        {
            ImportAudio(name);
        }
        Material spriteMaterial = CreateMaterial("GeneratedSprite", "Universal Render Pipeline/2D/Sprite-Unlit-Default");
        ConnectBurst("Assets/Effects/SwordWave/SwordWaveHit.prefab", "GeneratedHitFlash", hit, spriteMaterial,
            Vector2.one * ImpactStartSize, Vector2.one * ImpactEndSize, ImpactFlashDuration, 0.75f);
        ConnectBurst("Assets/Effects/MeleeSkills/AreaSlash.prefab", "GeneratedShockwave", ring, spriteMaterial,
            new Vector2(0.9f, 0.65f), new Vector2(2.7f, 1.45f), AreaShockwaveDuration, 0.45f);
        ConnectBurst("Assets/Effects/MeleeSkills/DashStart.prefab", "GeneratedShockwave", ring, spriteMaterial,
            Vector2.one * 0.35f, Vector2.one * 1.35f, 0.22f, 0.5f);
        SwordWaveBurst landing = CreateDeathEffect("TombstoneLand", dust, spriteMaterial,
            new Vector2(2.6f, 0.55f), new Vector2(3.4f, 0.72f), new Vector3(0f, 0.28f, 0f), 0.55f,
            Load<AudioClip>(AudioRoot + "/TombstoneLand.wav"), sparkle, false);
        SwordWaveBurst revival = CreateDeathEffect("Revive", revive, spriteMaterial,
            new Vector2(1.6f, 2.25f), new Vector2(1.9f, 2.55f), new Vector3(0f, 1.1f, 0f), 0.9f,
            Load<AudioClip>(ReviveAudio), sparkle, true);
        ConfigurePlayer(landing, revival);
        ConfigureBoss("Assets/Resources/Prefabs/Monster/TestBoss.prefab", danger, spriteMaterial, false);
        ConfigureBoss("Assets/Resources/Prefabs/Monster/NetworkBoss.prefab", danger, spriteMaterial, true);
        ConfigureBattleAudio();
        ConfigureCombatFeedback();
        AssetDatabase.SaveAssets();
        Debug.Log("GENERATED_COMBAT_EFFECTS_SETUP_OK: sprites=6, pooled effects connected, boss warning and result audio connected");
    }

    /// <summary>
    /// 기존 전투 프리팹의 표시 크기·소멸 시간과 기본 음량 조정
    /// 이미지·메시·캐릭터·보스 구성의 재생성 제외
    /// </summary>
    [MenuItem("Tools/Effects/Configure Combat Feedback")]
    public static void ConfigureCombatFeedback()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 전투 연출 조정 필요");
        }
        var entries = new (string Path, float Volume)[]
        {
            ("Assets/Effects/SwordWave/SwordWaveLaunch.prefab", CastVolume),
            ("Assets/Effects/SwordWave/SwordWaveHit.prefab", ImpactVolume),
            ("Assets/Effects/MeleeSkills/AreaSlash.prefab", AreaVolume),
            ("Assets/Effects/MeleeSkills/DashStart.prefab", DashVolume)
        };
        foreach (var entry in entries)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(entry.Path);
            try
            {
                root.GetComponent<AudioSource>().volume = entry.Volume;
                if (root.TryGetComponent(out AreaSlashEffect slash))
                {
                    var settings = new SerializedObject(slash);
                    settings.FindProperty("_fadeDuration").floatValue = AreaFadeDuration;
                    settings.FindProperty("_rangeCoverage").floatValue = AreaRangeCoverage;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    SpriteEffectPulse shockwave = root.transform.Find("GeneratedShockwave").GetComponent<SpriteEffectPulse>();
                    var pulse = new SerializedObject(shockwave);
                    pulse.FindProperty("_duration").floatValue = AreaShockwaveDuration;
                    pulse.ApplyModifiedPropertiesWithoutUndo();
                }
                Transform flash = root.transform.Find("GeneratedHitFlash");
                if (flash != null)
                {
                    Vector2 size = flash.GetComponent<SpriteRenderer>().sprite.bounds.size;
                    var pulse = new SerializedObject(flash.GetComponent<SpriteEffectPulse>());
                    pulse.FindProperty("_startScale").vector2Value = new Vector2(ImpactStartSize / size.x, ImpactStartSize / size.y);
                    pulse.FindProperty("_endScale").vector2Value = new Vector2(ImpactEndSize / size.x, ImpactEndSize / size.y);
                    pulse.FindProperty("_duration").floatValue = ImpactFlashDuration;
                    pulse.ApplyModifiedPropertiesWithoutUndo();
                }
                SavePrefab(root, entry.Path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("COMBAT_FEEDBACK_SETUP_OK");
    }

    private static void ConnectBurst(string path, string name, Sprite sprite, Material material,
        Vector2 startSize, Vector2 endSize, float duration, float opacity)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            CreatePulse(root.transform, name, sprite, material, startSize, endSize, Vector3.zero, duration, opacity);
            SetPulses(root.GetComponent<SwordWaveBurst>(), root.GetComponentsInChildren<SpriteEffectPulse>(true));
            SavePrefab(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigurePlayer(SwordWaveBurst landing, SwordWaveBurst revive)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            PlayerDeathPresentation presentation = root.GetComponent<PlayerDeathPresentation>();
            Set(presentation, "_landingEffectPrefab", landing);
            Set(presentation, "_reviveEffectPrefab", revive);
            presentation.ValidateConfiguration();
            SavePrefab(root, PlayerPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureBoss(string path, Sprite sprite, Material material, bool isNetwork)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            BossAttackTelegraph telegraph = root.GetComponentInChildren<BossAttackTelegraph>(true);
            var settings = new SerializedObject(telegraph);
            SpriteRenderer renderer = (SpriteRenderer)settings.FindProperty("_spriteRenderer").objectReferenceValue;
            if (settings.FindProperty("_pathMaterial").objectReferenceValue == null)
            {
                settings.FindProperty("_pathMaterial").objectReferenceValue = renderer.sharedMaterial;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.color = new Color(1f, 1f, 1f, 0.65f);
            renderer.enabled = false;
            if (isNetwork)
            {
                Set(GetOrAdd<NetworkBossTelegraph>(root), "_telegraph", telegraph);
            }
            SavePrefab(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureBattleAudio()
    {
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene scene = EditorSceneManager.OpenScene(BossScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (BossBattleView view in root.GetComponentsInChildren<BossBattleView>(true))
                {
                    AudioSource source = GetOrAdd<AudioSource>(view.gameObject);
                    AudioSettingsSetup.AssignOutput(source);
                    source.playOnAwake = false;
                    source.spatialBlend = 0f;
                    source.volume = 0.45f;
                    Set(view, "_resultAudioSource", source);
                    Set(view, "_victoryClip", Load<AudioClip>(AudioRoot + "/Victory.wav"));
                    Set(view, "_defeatClip", Load<AudioClip>(AudioRoot + "/Defeat.wav"));
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new IOException("보스전 효과음 씬 저장 실패");
            }
        }
        finally
        {
            if (previous.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
    }

    private static Sprite ImportSprite(string name)
    {
        string path = ImageRoot + "/" + name + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            throw new FileNotFoundException("생성 이미지 누락: " + path);
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 256f;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        SpriteRect[] previous = provider.GetSpriteRects();
        var rectangle = new SpriteRect
        {
            name = name,
            rect = FindVisibleRect(path),
            alignment = SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f),
            spriteID = previous.Length > 0 ? previous[0].spriteID : GUID.Generate()
        };
        provider.SetSpriteRects(new[] { rectangle });
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            new[] { new SpriteNameFileIdPair(name, rectangle.spriteID) });
        provider.Apply();
        importer.SaveAndReimport();
        return Load<Sprite>(path);
    }

    private static Rect FindVisibleRect(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            if (!texture.LoadImage(File.ReadAllBytes(Path.Combine(projectRoot, path))))
            {
                throw new InvalidDataException("생성 이미지 읽기 실패: " + path);
            }
            Color32[] pixels = texture.GetPixels32();
            int minX = texture.width;
            int minY = texture.height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a <= 8)
                    {
                        continue;
                    }
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }
            if (maxX < minX || maxY < minY)
            {
                throw new InvalidDataException("표시 영역이 없는 이미지: " + path);
            }
            // 원본 PNG 보존, 스프라이트 영역만 투명 여백 제외
            minX = Mathf.Max(0, minX - 2);
            minY = Mathf.Max(0, minY - 2);
            maxX = Mathf.Min(texture.width - 1, maxX + 2);
            maxY = Mathf.Min(texture.height - 1, maxY + 2);
            return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    private static void ImportAudio(string name)
    {
        string path = AudioRoot + "/" + name + ".wav";
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
        {
            throw new FileNotFoundException("생성 효과음 누락: " + path);
        }
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.PCM;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();
    }

    private static Material CreateMaterial(string name, string shaderName)
    {
        string path = EffectRoot + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new MissingReferenceException("연출 셰이더 누락: " + shaderName);
            }
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    private static SpriteEffectPulse CreatePulse(Transform parent, string name, Sprite sprite, Material material,
        Vector2 startSize, Vector2 endSize, Vector3 offset, float duration, float opacity)
    {
        Transform existing = parent.Find(name);
        GameObject child = existing != null ? existing.gameObject : new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = offset;
        SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(child);
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.sortingOrder = 26;
        renderer.enabled = false;
        SpriteEffectPulse pulse = GetOrAdd<SpriteEffectPulse>(child);
        var settings = new SerializedObject(pulse);
        settings.FindProperty("_renderer").objectReferenceValue = renderer;
        Vector2 size = sprite.bounds.size;
        settings.FindProperty("_startScale").vector2Value = new Vector2(startSize.x / size.x, startSize.y / size.y);
        settings.FindProperty("_endScale").vector2Value = new Vector2(endSize.x / size.x, endSize.y / size.y);
        settings.FindProperty("_duration").floatValue = duration;
        settings.FindProperty("_tint").colorValue = new Color(1f, 1f, 1f, opacity);
        settings.ApplyModifiedPropertiesWithoutUndo();
        return pulse;
    }

    private static SwordWaveBurst CreateDeathEffect(string name, Sprite sprite, Material material,
        Vector2 startSize, Vector2 endSize, Vector3 offset, float duration, AudioClip clip, Sprite sparkle, bool emitSparks)
    {
        var root = new GameObject(name);
        root.SetActive(false);
        try
        {
            PoolableObject poolable = root.AddComponent<PoolableObject>();
            AudioSource source = root.AddComponent<AudioSource>();
            AudioSettingsSetup.AssignOutput(source);
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.3f;
            ParticleSystem particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = duration;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startColor = new Color(1f, 0.88f, 0.45f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 16;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(emitSparks ? new[] { new ParticleSystem.Burst(0f, 8) } : Array.Empty<ParticleSystem.Burst>());
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.9f, 0.5f, 0.01f);
            shape.position = new Vector3(0f, 0.85f, 0f);
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = 0.7f;
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            ParticleSystem.TextureSheetAnimationModule sheet = particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;
            sheet.AddSprite(sparkle);
            ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            Material particleMaterial = CreateMaterial("GeneratedParticle", "Universal Render Pipeline/Particles/Unlit");
            particleMaterial.SetTexture("_BaseMap", sparkle.texture);
            particleMaterial.SetFloat("_Surface", 1f);
            particleMaterial.SetFloat("_ZWrite", 0f);
            particleMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            particleMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            particleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            particleMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(particleMaterial);
            particleRenderer.sharedMaterial = particleMaterial;
            particleRenderer.sortingOrder = 27;
            SpriteEffectPulse pulse = CreatePulse(root.transform, "GeneratedImage", sprite, material,
                startSize, endSize, offset, duration, 0.8f);
            SwordWaveBurst burst = root.AddComponent<SwordWaveBurst>();
            Set(burst, "_particles", particles);
            Set(burst, "_audioSource", source);
            Set(burst, "_clip", clip);
            Set(burst, "_poolableObject", poolable);
            SetPulses(burst, new[] { pulse });
            var settings = new SerializedObject(burst);
            settings.FindProperty("_lifetime").floatValue = Mathf.Max(duration, 0.75f);
            settings.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            string path = EffectRoot + "/" + name + ".prefab";
            SavePrefab(root, path);
            return Load<GameObject>(path).GetComponent<SwordWaveBurst>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void SetPulses(SwordWaveBurst owner, SpriteEffectPulse[] pulses)
    {
        var settings = new SerializedObject(owner);
        SerializedProperty array = settings.FindProperty("_spriteEffects");
        array.arraySize = pulses.Length;
        for (int i = 0; i < pulses.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = pulses[i];
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object owner, string field, Object value)
    {
        var settings = new SerializedObject(owner);
        settings.FindProperty(field).objectReferenceValue = value;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T GetOrAdd<T>(GameObject root) where T : Component
    {
        T component = root.GetComponent<T>();
        return component != null ? component : root.AddComponent<T>();
    }

    private static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            throw new FileNotFoundException("연출 에셋 누락: " + path);
        }
        return asset;
    }

    private static void SavePrefab(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
        if (!success)
        {
            throw new IOException("연출 프리팹 저장 실패: " + path);
        }
    }
}
