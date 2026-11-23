using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 사용자 볼륨 믹서·공통 설정창·기존 재생 채널 연결
/// 믹서 생성에 필요한 에디터 내부 API는 이 도구에만 한정
/// </summary>
public static class AudioSettingsSetup
{
    public const string MixerPath = "Assets/Config/Audio/GameAudio.mixer";
    private const string BgmGroup = "BGM";
    private const string SfxGroup = "SFX";
    private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    [MenuItem("Tools/Audio/Configure Volume Settings")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
        {
            throw new InvalidOperationException("플레이 종료와 열린 씬 저장 후 오디오 설정 구성 필요");
        }
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            AudioMixer mixer = ConfigureMixer();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Effects", "Assets/Resources/Prefabs/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
                    {
                        AssignOutput(source, source.GetComponentInParent<BgmPlayer>() != null);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (string sceneName in new[] { "TitleScene", "SelectScene", "HomeScene", "HostScene", "HuntScene", "BossScene" })
            {
                Scene scene = EditorSceneManager.OpenScene($"Assets/00_Scenes/{sceneName}.unity", OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
                    {
                        AssignOutput(source, source.GetComponentInParent<BgmPlayer>() != null);
                    }
                }
                GameManager manager = Object.FindAnyObjectByType<GameManager>();
                if (manager != null)
                {
                    ConfigureInterface(manager, mixer);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new IOException($"{sceneName}: 오디오 연결 저장 실패");
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("AUDIO_SETTINGS_SETUP_OK");
        }
        finally
        {
            if (previous.Length > 0) { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }
    }

    /// <summary>
    /// 재생 채널의 믹서 출력 연결
    /// 기존 이펙트 생성 도구에서도 동일한 그룹 사용
    /// </summary>
    public static void AssignOutput(AudioSource source, bool isBgm = false)
    {
        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (mixer != null)
        {
            source.outputAudioMixerGroup = mixer.FindMatchingGroups(isBgm ? BgmGroup : SfxGroup).Single();
        }
    }

    private static AudioMixer ConfigureMixer()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MixerPath));
        AssetDatabase.Refresh();
        Type controllerType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.Audio.AudioMixerController")).First(t => t != null);
        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (mixer == null)
        {
            mixer = (AudioMixer)controllerType.GetMethod("CreateMixerControllerAtPath", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { MixerPath });
        }
        object master = controllerType.GetProperty("masterGroup", MemberFlags).GetValue(mixer);
        PropertyInfo exposedProperty = controllerType.GetProperty("exposedParameters", MemberFlags);
        Array exposed = (Array)exposedProperty.GetValue(mixer);
        Type parameterType = exposed.GetType().GetElementType();
        var parameters = exposed.Cast<object>().ToList();
        foreach (string name in new[] { BgmGroup, SfxGroup })
        {
            AudioMixerGroup group = mixer.FindMatchingGroups(name).SingleOrDefault();
            if (group == null)
            {
                group = (AudioMixerGroup)controllerType.GetMethod("CreateNewGroup", MemberFlags).Invoke(mixer, new object[] { name, false });
                controllerType.GetMethod("AddChildToParent", MemberFlags).Invoke(mixer, new[] { (object)group, master });
            }
            string parameterName = name == BgmGroup ? "BgmVolume" : "SfxVolume";
            object parameter = Activator.CreateInstance(parameterType);
            parameterType.GetField("name").SetValue(parameter, parameterName);
            object volumeGuid = group.GetType().GetMethod("GetGUIDForVolume", MemberFlags).Invoke(group, null);
            parameterType.GetField("guid").SetValue(parameter, volumeGuid);
            parameters.RemoveAll(item => (string)parameterType.GetField("name").GetValue(item) == parameterName);
            parameters.Add(parameter);
        }
        Array result = Array.CreateInstance(parameterType, parameters.Count);
        for (int i = 0; i < parameters.Count; i++) { result.SetValue(parameters[i], i); }
        exposedProperty.SetValue(mixer, result);
        ConfigureMixerView();
        EditorUtility.SetDirty(mixer);
        return mixer;
    }

    /// <summary>
    /// 믹서 편집 창에서 전체 그룹을 표시하는 기본 뷰 구성
    /// 기존 사용자 뷰가 존재하면 보존
    /// </summary>
    public static void ConfigureMixerView()
    {
        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        Type controllerType = mixer.GetType();
        PropertyInfo viewsProperty = controllerType.GetProperty("views", MemberFlags);
        Array views = (Array)viewsProperty.GetValue(mixer);
        if (views.Length == 0)
        {
            Type viewType = views.GetType().GetElementType();
            object view = Activator.CreateInstance(viewType);
            viewType.GetField("name").SetValue(view, "All");
            AudioMixerGroup[] groups = mixer.FindMatchingGroups(string.Empty);
            Type guidType = viewType.GetField("guids").FieldType.GetElementType();
            Array guids = Array.CreateInstance(guidType, groups.Length);
            for (int i = 0; i < groups.Length; i++)
            {
                guids.SetValue(groups[i].GetType().GetProperty("groupID", MemberFlags).GetValue(groups[i]), i);
            }
            viewType.GetField("guids").SetValue(view, guids);
            Array configured = Array.CreateInstance(viewType, 1);
            configured.SetValue(view, 0);
            viewsProperty.SetValue(mixer, configured);
            controllerType.GetProperty("currentViewIndex", MemberFlags).SetValue(mixer, 0);
            EditorUtility.SetDirty(mixer);
            AssetDatabase.SaveAssetIfDirty(mixer);
        }
        Debug.Log("AUDIO_MIXER_VIEW_OK");
    }

    private static void ConfigureInterface(GameManager manager, AudioMixer mixer)
    {
        AudioSettingsController settings = manager.GetComponent<AudioSettingsController>();
        if (settings == null) { settings = manager.gameObject.AddComponent<AudioSettingsController>(); }
        Set(settings, "_mixer", mixer);
        Set(manager, "_audioSettings", settings);
        Transform previous = manager.transform.Find("AudioSettingsCanvas");
        if (previous != null) { Object.DestroyImmediate(previous.gameObject); }
        var canvasRoot = new GameObject("AudioSettingsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot.transform.SetParent(manager.transform, false);
        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/07_Font/White.asset");
        Image overlay = Image("AudioSettingsPanel", canvasRoot.transform, new Color(0.01f, 0.025f, 0.04f, 0.85f));
        Stretch(overlay.rectTransform);
        Image plate = Image("Plate", overlay.transform, new Color(0.045f, 0.1f, 0.14f, 1f));
        Place(plate.rectTransform, new Vector2(620, 360), Vector2.zero);
        Label("Title", plate.transform, font, "사운드 설정", 34, new Vector2(0, 115));
        Slider bgm = CreateSlider(plate.transform, font, "BGM", 35);
        Slider sfx = CreateSlider(plate.transform, font, "효과음", -40);
        AudioSettingsView view = overlay.gameObject.AddComponent<AudioSettingsView>();
        Set(view, "_settings", settings);
        Set(view, "_bgmSlider", bgm);
        Set(view, "_sfxSlider", sfx);
        Button close = CreateButton("Close", plate.transform, font, "닫기", new Vector2(0, -125));
        UnityEventTools.AddPersistentListener(close.onClick, view.Close);
        Button open = CreateButton("OpenAudioSettings", canvasRoot.transform, font, "사운드", Vector2.zero);
        RectTransform openRect = (RectTransform)open.transform;
        openRect.anchorMin = openRect.anchorMax = new Vector2(1, 0);
        openRect.anchoredPosition = new Vector2(-105, 52);
        UnityEventTools.AddPersistentListener(open.onClick, view.Open);
        UIManager ui = manager.GetComponent<UIManager>();
        Set(ui, "_audioSettingsView", view);
        overlay.gameObject.SetActive(false);
    }

    private static Slider CreateSlider(Transform parent, TMP_FontAsset font, string label, float y)
    {
        Label(label + "Label", parent, font, label, 26, new Vector2(-200, y));
        Image track = Image(label + "Slider", parent, new Color(0.17f, 0.24f, 0.3f));
        Place(track.rectTransform, new Vector2(330, 18), new Vector2(60, y));
        Image fill = Image("Fill", track.transform, new Color(0.35f, 0.83f, 0.7f));
        Stretch(fill.rectTransform);
        fill.raycastTarget = false;
        Image handle = Image("Handle", track.transform, new Color(1f, 0.91f, 0.68f));
        Place(handle.rectTransform, new Vector2(20, 34), Vector2.zero);
        Slider slider = track.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        return slider;
    }

    private static Button CreateButton(string name, Transform parent, TMP_FontAsset font, string text, Vector2 position)
    {
        Image image = Image(name, parent, new Color(0.09f, 0.25f, 0.26f));
        Place(image.rectTransform, new Vector2(160, 50), position);
        Button button = image.gameObject.AddComponent<AnimatedButton>();
        button.gameObject.AddComponent<ButtonHoverMotion>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        Label("Label", image.transform, font, text, 26, Vector2.zero);
        return button;
    }

    private static void Label(string name, Transform parent, TMP_FontAsset font, string text, float size, Vector2 position)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false);
        TextMeshProUGUI label = root.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        Place(label.rectTransform, new Vector2(180, 50), position);
    }

    private static Image Image(string name, Transform parent, Color color)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        Image image = root.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static void Place(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Set(Object target, string name, Object value)
    {
        var settings = new SerializedObject(target);
        settings.FindProperty(name).objectReferenceValue = value;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }
}
