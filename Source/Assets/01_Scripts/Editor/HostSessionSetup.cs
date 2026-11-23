using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// HostScene 접속 패널 생성과 맵 초기화 참조 연결
/// </summary>
public static class HostSessionSetup
{
    [MenuItem("Tools/Network/Configure Host Session UI")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 대기실 UI 구성 필요");
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                throw new InvalidOperationException("열린 씬 저장 후 대기실 UI 구성 필요");
            }
        }
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/00_Scenes/HostScene.unity", OpenSceneMode.Single);
            MapManager map = UnityEngine.Object.FindAnyObjectByType<MapManager>();
            HostSessionView view = UnityEngine.Object.FindAnyObjectByType<HostSessionView>(FindObjectsInactive.Include);
            if (view == null)
            {
                view = CreateView();
            }
            var settings = new SerializedObject(map);
            settings.FindProperty("_hostSessionView").objectReferenceValue = view;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("HostScene 저장 실패");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("HOST_SESSION_UI_SETUP_OK");
        }
        finally
        {
            if (previous.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
    }

    private static HostSessionView CreateView()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/07_Font/White.asset");
        var root = new GameObject("HostSessionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 40;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        HostSessionView view = root.AddComponent<HostSessionView>();
        RectTransform panel = Rect("SessionPanel", root.transform, new Vector2(1, 1), new Vector2(-260, -340), new Vector2(480, 620));
        panel.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.1f, 0.17f, 0.94f);
        Label("Title", panel, font, "인터넷 파티", new Vector2(0, 260), new Vector2(400, 45), 30);
        TMP_Text status = Label("Status", panel, font, "새로고침으로 인터넷 방 검색", new Vector2(0, -250), new Vector2(400, 90), 22);

        RectTransform inputRoot = Rect("ServerAddress", panel, Vector2.one * 0.5f, new Vector2(0, 200), new Vector2(420, 50));
        Image background = inputRoot.gameObject.AddComponent<Image>();
        background.color = new Color(0.15f, 0.22f, 0.3f);
        RectTransform viewport = Rect("Viewport", inputRoot, Vector2.one * 0.5f, Vector2.zero, new Vector2(360, 46));
        viewport.gameObject.AddComponent<RectMask2D>();
        TextMeshProUGUI value = Label("Text", viewport, font, "", Vector2.zero, new Vector2(360, 46), 23);
        TextMeshProUGUI placeholder = Label("Placeholder", viewport, font, "방 이름 입력", Vector2.zero, new Vector2(360, 46), 23);
        placeholder.color = new Color(0.65f, 0.7f, 0.75f);
        TMP_InputField input = inputRoot.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = background;
        input.textViewport = viewport;
        input.textComponent = value;
        input.placeholder = placeholder;
        input.characterLimit = InternetRoomService.MaximumRoomNameLength;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.navigation = new Navigation { mode = Navigation.Mode.None };

        Button create = Button(panel, font, "Create", "방 생성", new Vector2(-110, 130));
        Button join = Button(panel, font, "Join", "새로고침", new Vector2(110, 130));
        Button depart = Button(panel, font, "Depart", "보스전 출발", new Vector2(-110, 130));
        Button leave = Button(panel, font, "Leave", "나가기 / 취소", new Vector2(110, 130));
        depart.gameObject.SetActive(false);
        leave.gameObject.SetActive(false);
        create.interactable = join.interactable = input.interactable = false;
        var settings = new SerializedObject(view);
        settings.FindProperty("_addressInput").objectReferenceValue = input;
        settings.FindProperty("_createButton").objectReferenceValue = create;
        settings.FindProperty("_joinButton").objectReferenceValue = join;
        settings.FindProperty("_departButton").objectReferenceValue = depart;
        settings.FindProperty("_leaveButton").objectReferenceValue = leave;
        settings.FindProperty("_statusText").objectReferenceValue = status;
        settings.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var label = Rect(name, parent, Vector2.one * 0.5f, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static Button Button(Transform parent, TMP_FontAsset font, string name, string text, Vector2 position)
    {
        RectTransform rect = Rect(name, parent, Vector2.one * 0.5f, position, new Vector2(185, 55));
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.17f, 0.32f, 0.43f);
        Button button = rect.gameObject.AddComponent<AnimatedButton>();
        button.gameObject.AddComponent<ButtonHoverMotion>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        Label("Label", rect, font, text, Vector2.zero, rect.sizeDelta, 24);
        return button;
    }
}
