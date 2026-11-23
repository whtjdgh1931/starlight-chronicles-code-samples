using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 사냥터 난이도 UI 연결
/// </summary>
public static class HuntDifficultySetup
{
    private static readonly HuntDifficulty[] Difficulties = { HuntDifficulty.Beginner, HuntDifficulty.Intermediate, HuntDifficulty.Advanced };
    private static readonly string[] Names = { "초급", "중급", "상급" };
    private static readonly Color TextColor = new Color(1f, 0.95f, 0.8f);

    /// <summary>
    /// 사냥터 난이도 선택 화면 생성과 참조 연결
    /// </summary>
    [MenuItem("Tools/Maps/Configure Hunt Difficulty Selection")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
        {
            throw new InvalidOperationException("플레이 종료와 열린 씬 저장 후 실행 필요");
        }
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string sceneName in new[] { "TitleScene", "HomeScene" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/00_Scenes/" + sceneName + ".unity");
                var manager = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<UIManager>());
                var audio = (Component)manager.FindProperty("_audioSettingsView").objectReferenceValue;
                Canvas canvas = audio.GetComponentInParent<Canvas>(true);
                TMP_FontAsset font = audio.GetComponentInChildren<TMP_Text>(true).font;
                HuntEntryView view = canvas.GetComponentsInChildren<HuntEntryView>(true).FirstOrDefault();
                if (view == null) { view = CreateView(canvas.transform, font); }
                manager.FindProperty("_huntEntryView").objectReferenceValue = view;
                manager.ApplyModifiedPropertiesWithoutUndo();
                Save(scene);
            }

        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
        Debug.Log("HUNT_DIFFICULTY_READY: selectable tiers");
    }

    private static void Save(UnityEngine.SceneManagement.Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new InvalidOperationException("사냥터 난이도 설정 저장 실패: " + scene.name);
        }
    }

    private static HuntEntryView CreateView(Transform parent, TMP_FontAsset font)
    {
        Image overlay = Image("HuntDifficultySelection", parent, new Color(0.01f, 0.025f, 0.04f, 0.72f));
        Stretch(overlay.rectTransform);
        Image panel = Image("Panel", overlay.transform, new Color(0.055f, 0.1f, 0.12f, 0.98f));
        Place(panel.rectTransform, new Vector2(900, 520), Vector2.zero);
        Label("Title", panel.transform, font, "사냥터 선택", 34, new Vector2(0, 195), new Vector2(780, 55));
        TMP_Text level = Label("Level", panel.transform, font, string.Empty, 24, new Vector2(0, 110), new Vector2(780, 80));
        TMP_Text message = Label("Message", panel.transform, font, string.Empty, 22, new Vector2(0, -120), new Vector2(780, 65));
        Button cancel = Button("Cancel", panel.transform, font, "취소", new Vector2(0, -200), new Vector2(230, 55));
        var view = overlay.gameObject.AddComponent<HuntEntryView>();
        var settings = new SerializedObject(view);
        settings.FindProperty("_levelText").objectReferenceValue = level;
        settings.FindProperty("_messageText").objectReferenceValue = message;
        SerializedProperty rows = settings.FindProperty("_rows");
        rows.arraySize = Difficulties.Length;
        for (int index = 0; index < Difficulties.Length; index++)
        {
            Button button = Button("Difficulty_" + index, panel.transform, font, Names[index],
                new Vector2((index - 1) * 260, -15), new Vector2(230, 140));
            SerializedProperty row = rows.GetArrayElementAtIndex(index);
            row.FindPropertyRelative("Difficulty").enumValueIndex = (int)Difficulties[index];
            row.FindPropertyRelative("Button").objectReferenceValue = button;
            row.FindPropertyRelative("Label").objectReferenceValue = button.GetComponentInChildren<TMP_Text>(true);
            UnityEventTools.AddIntPersistentListener(button.onClick, view.SelectDifficulty, (int)Difficulties[index]);
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(cancel.onClick, view.RequestClose);
        view.Close();
        return view;
    }

    private static Image Image(string name, Transform parent, Color color)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        Image image = root.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string text, float size, Vector2 position, Vector2 dimensions)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false);
        var label = root.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = TextColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        Place(label.rectTransform, dimensions, position);
        return label;
    }

    private static Button Button(string name, Transform parent, TMP_FontAsset font, string text, Vector2 position, Vector2 size)
    {
        Image image = Image(name, parent, new Color(0.13f, 0.28f, 0.27f));
        Place(image.rectTransform, size, position);
        Button button = image.gameObject.AddComponent<AnimatedButton>();
        button.gameObject.AddComponent<ButtonHoverMotion>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        TMP_Text label = Label("Label", button.transform, font, text, 27, Vector2.zero, Vector2.zero);
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(8, 4);
        label.rectTransform.offsetMax = new Vector2(-8, -4);
        return button;
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
}
