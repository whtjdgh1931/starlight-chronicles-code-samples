using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 기존 보스 결과 패널과 인벤토리의 보상 수령 버튼 연결
/// 카메라·맵·전투 수치 변경 제외
/// </summary>
public static class BossBattleResultSetup
{
    [MenuItem("Tools/Network/Configure Boss Battle Results")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 결과 UI 구성 필요");
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                throw new InvalidOperationException("열린 씬 저장 후 결과 UI 구성 필요");
            }
        }
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (GameSceneId id in new[] { GameSceneId.TitleScene, GameSceneId.HomeScene, GameSceneId.HostScene, GameSceneId.HuntScene, GameSceneId.BossScene })
            {
                Scene scene = EditorSceneManager.OpenScene($"Assets/00_Scenes/{id}.unity", OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (InventoryUI ui in root.GetComponentsInChildren<InventoryUI>(true))
                    {
                        ConfigureInventory(ui);
                    }
                    foreach (BossBattleView view in root.GetComponentsInChildren<BossBattleView>(true))
                    {
                        ConfigureBattle(view);
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException($"{id}: 결과 UI 저장 실패");
                }
            }
        }
        finally
        {
            if (previous.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("BOSS_BATTLE_RESULTS_SETUP_OK");
    }

    /// <summary>
    /// 보스 체력바의 검정 배경·빨강 하단·초록 상단 구성
    /// 기존 체력바 참조와 씬 GUID 유지
    /// </summary>
    [MenuItem("Tools/Network/Configure Boss Health Bars")]
    public static void ConfigureHealthBars()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 체력바 구성 필요");
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                throw new InvalidOperationException("열린 씬 저장 후 체력바 구성 필요");
            }
        }
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/00_Scenes/BossScene.unity", OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (BossHudView view in root.GetComponentsInChildren<BossHudView>(true))
                {
                    var settings = new SerializedObject(view);
                    Image green = (Image)settings.FindProperty("_healthFill").objectReferenceValue;
                    var redProperty = settings.FindProperty("_secondPhaseFill");
                    Image red = (Image)redProperty.objectReferenceValue;
                    if (red == null)
                    {
                        var bar = new GameObject("SecondPhaseFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                        bar.layer = green.gameObject.layer;
                        bar.transform.SetParent(green.transform.parent, false);
                        red = bar.GetComponent<Image>();
                        EditorUtility.CopySerialized(green, red);
                        RectTransform target = red.rectTransform;
                        RectTransform source = green.rectTransform;
                        target.anchorMin = source.anchorMin;
                        target.anchorMax = source.anchorMax;
                        target.pivot = source.pivot;
                        target.sizeDelta = source.sizeDelta;
                        target.anchoredPosition3D = source.anchoredPosition3D;
                        target.localRotation = source.localRotation;
                        target.localScale = source.localScale;
                        redProperty.objectReferenceValue = red;
                    }
                    green.transform.parent.GetComponent<Image>().color = Color.black;
                    green.color = Color.green;
                    red.color = Color.red;
                    green.type = red.type = Image.Type.Filled;
                    green.fillMethod = red.fillMethod = Image.FillMethod.Horizontal;
                    green.fillOrigin = red.fillOrigin = (int)Image.OriginHorizontal.Left;
                    green.fillAmount = red.fillAmount = 1f;
                    green.raycastTarget = red.raycastTarget = false;
                    red.transform.SetAsFirstSibling();
                    green.transform.SetAsLastSibling();
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("보스 체력바 씬 저장 실패");
            }
        }
        finally
        {
            if (previous.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
        Debug.Log("BOSS_HEALTH_BARS_SETUP_OK");
    }

    private static void ConfigureBattle(BossBattleView view)
    {
        var settings = new SerializedObject(view);
        GameObject panel = (GameObject)settings.FindProperty("_gameOverRoot").objectReferenceValue;
        TextMeshProUGUI title = panel.transform.Find("GameOverTitle").GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI details = panel.transform.Find("GameOverReason").GetComponent<TextMeshProUGUI>();
        details.fontSize = 28f;
        details.rectTransform.anchorMin = new Vector2(0.1f, 0.34f);
        details.rectTransform.anchorMax = new Vector2(0.9f, 0.55f);
        Button returnButton = Button(panel.transform, "ReturnToSelection", title.font, "돌아가기",
            new Vector2(0.5f, 0.23f), new Vector2(-175f, 0f), new Vector2(310f, 65f));
        Button retryButton = Button(panel.transform, "RetryResultSave", title.font, "저장 재시도",
            new Vector2(0.5f, 0.23f), new Vector2(175f, 0f), new Vector2(310f, 65f));
        settings.FindProperty("_resultTitle").objectReferenceValue = title;
        settings.FindProperty("_resultDetails").objectReferenceValue = details;
        settings.FindProperty("_returnButton").objectReferenceValue = returnButton;
        settings.FindProperty("_retrySaveButton").objectReferenceValue = retryButton;
        settings.ApplyModifiedPropertiesWithoutUndo();
        panel.SetActive(false);
    }

    private static void ConfigureInventory(InventoryUI ui)
    {
        var settings = new SerializedObject(ui);
        CanvasGroup group = (CanvasGroup)settings.FindProperty("_canvasGroup").objectReferenceValue;
        TextMeshProUGUI gold = (TextMeshProUGUI)settings.FindProperty("_goldText").objectReferenceValue;
        Button reward = Button(group.transform, "ClaimBossRewards", gold.font, "보상 수령 (0)",
            new Vector2(0.5f, 0f), new Vector2(0f, -38f), new Vector2(320f, 52f));
        settings.FindProperty("_rewardButton").objectReferenceValue = reward;
        settings.FindProperty("_rewardText").objectReferenceValue = reward.GetComponentInChildren<TextMeshProUGUI>(true);
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Button Button(Transform parent, string name, TMP_FontAsset font, string label,
        Vector2 anchor, Vector2 position, Vector2 size)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing.GetComponent<Button>();
        }
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(AnimatedButton), typeof(ButtonHoverMotion));
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = root.GetComponent<Image>();
        image.color = new Color(0.13f, 0.24f, 0.34f, 1f);
        Button button = root.GetComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var textRect = (RectTransform)textObject.transform;
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = label;
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return button;
    }
}

