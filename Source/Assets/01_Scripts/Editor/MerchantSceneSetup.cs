using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 공통 상인 화면과 마을의 싱글플레이 NPC 배치
/// 원본 상인 이미지와 다른 UI 구성 보존
/// </summary>
public static class MerchantSceneSetup
{
    private const string MerchantSpritePath = "Assets/Art/NPC/TravelMerchant.png";
    private const string MerchantMaterialPath = "Assets/Art/NPC/TravelMerchantUnlit.mat";
    private const string MerchantPrefabPath = "Assets/Resources/Prefabs/UI/MerchantPrefab.prefab";
    private const string ShopPrefabPath = "Assets/Resources/Prefabs/UI/MerchantShopPrefab.prefab";
    private const string QuestTabSpritePath = "Assets/99_Assets/Fantasy Wooden GUI  Free/normal_ui_set A/TextBTN_Medium.png";
    private const string HintBackgroundPath = "Assets/99_Assets/Fantasy Wooden GUI  Free/normal_ui_set A/IRONY TITLE  empty.png";
    private const float MerchantVisualHeight = 1.25f;
    private const float HintClearance = 0.35f;
    private const float PanelWidth = 1100f;
    private const float PanelHeight = 650f;
    private static readonly Color PanelColor = new Color(0.055f, 0.1f, 0.12f, 0.98f);
    private static readonly Color ButtonColor = new Color(0.13f, 0.28f, 0.27f);
    private static readonly Color TextColor = new Color(1f, 0.95f, 0.8f);

    /// <summary>
    /// 두 진입 씬의 공통 화면과 HomeScene 상인 참조 저장
    /// 실행 전 열린 씬 저장 필요
    /// </summary>
    [MenuItem("Tools/NPC/Configure Merchant")]
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
                UIManager manager = UnityEngine.Object.FindAnyObjectByType<UIManager>();
                var settings = new SerializedObject(manager);
                var audio = (Component)settings.FindProperty("_audioSettingsView").objectReferenceValue;
                Canvas canvas = audio.GetComponentInParent<Canvas>(true);
                TMP_FontAsset font = audio.GetComponentInChildren<TMP_Text>(true).font;
                MerchantView view = canvas.GetComponentsInChildren<MerchantView>(true).FirstOrDefault();
                if (view == null)
                {
                    view = CreateView(canvas.transform, font);
                }
                SetReference(settings, "_merchantView", view);
                settings.ApplyModifiedPropertiesWithoutUndo();
                if (sceneName == "HomeScene")
                {
                    ConfigureNpc(font);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException("상인 씬 저장 실패: " + sceneName);
                }
            }
            AssetDatabase.SaveAssets();
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
        Debug.Log("MERCHANT_SETUP_READY: UIManager view, Home NPC, five quests");
    }

    private static MerchantView CreateView(Transform parent, TMP_FontAsset font)
    {
        Image overlay = Image("MerchantDialogue", parent, new Color(0.01f, 0.025f, 0.04f, 0.7f));
        Stretch(overlay.rectTransform);
        Image panel = Image("Panel", overlay.transform, PanelColor);
        Place(panel.rectTransform, new Vector2(PanelWidth, PanelHeight), Vector2.zero);
        Label("Title", panel.transform, font, "여행 상인", 32f, new Vector2(0, 280), new Vector2(850, 55));
        Image line = Image("TitleLine", panel.transform, new Color(0.7f, 0.6f, 0.35f));
        Place(line.rectTransform, new Vector2(1000, 2), new Vector2(0, 242));

        var view = overlay.gameObject.AddComponent<MerchantView>();
        RectTransform menu = Rect("MenuRoot", panel.transform);
        Stretch(menu);
        RectTransform quests = Rect("QuestRoot", panel.transform);
        Stretch(quests);
        Label("Greeting", menu, font,
            "반가워요, 모험가님!\n숲길에서 도움이 필요한 일이 있어요.\n어떤 이야기를 해 볼까요?", 26f,
            new Vector2(120, 70), new Vector2(600, 175));
        Image portrait = Image("Portrait", menu, Color.white);
        portrait.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MerchantSpritePath);
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        Place(portrait.rectTransform, new Vector2(200, 255), new Vector2(-340, 30));
        Button openQuests = Button("Quests", menu, font, "퀘스트", new Vector2(-80, -100), new Vector2(240, 60));
        Button shop = Button("Shop", menu, font, "상점", new Vector2(200, -100), new Vector2(240, 60));
        Button close = Button("Close", panel.transform, font, "닫기", new Vector2(475, 282), new Vector2(100, 42));
        Button back = Button("Back", quests, font, "처음으로", new Vector2(375, -186), new Vector2(250, 52));
        Button action = Button("QuestAction", quests, font, "수락", new Vector2(-300, -186), new Vector2(270, 52));
        TMP_Text detail = ScrollText("QuestDetail", quests, font, new Vector2(-175, 35), new Vector2(650, 375));
        TMP_Text message = Label("Feedback", panel.transform, font, string.Empty, 21f,
            new Vector2(0, -267), new Vector2(1000, 76));
        message.alignment = TextAlignmentOptions.MidlineLeft;
        message.overflowMode = TextOverflowModes.Overflow;

        var settings = new SerializedObject(view);
        SetReference(settings, "_menuRoot", menu.gameObject);
        SetReference(settings, "_questRoot", quests.gameObject);
        SetReference(settings, "_detailText", detail);
        SetReference(settings, "_messageText", message);
        SetReference(settings, "_actionButton", action);
        SetReference(settings, "_actionLabel", action.GetComponentInChildren<TMP_Text>(true));
        Sprite tabSprite = AssetDatabase.LoadAssetAtPath<Sprite>(QuestTabSpritePath)
            ?? throw new MissingReferenceException("의뢰 탭 버튼 이미지 필요");
        var tabSize = new Vector2(146, 44);
        UnityEngine.UI.Button unfinishedTab = Button("UnfinishedQuests", quests, font, "미완료",
            new Vector2(298, 189), tabSize);
        UnityEngine.UI.Button completedTab = Button("CompletedQuests", quests, font, "완료",
            new Vector2(452, 189), tabSize);
        unfinishedTab.image.sprite = completedTab.image.sprite = tabSprite;
        var tabColors = ColorBlock.defaultColorBlock;
        tabColors.disabledColor = new Color(0.78f, 1f, 0.72f);
        unfinishedTab.colors = completedTab.colors = tabColors;
        unfinishedTab.image.color = completedTab.image.color = Color.white;
        UnityEventTools.AddBoolPersistentListener(unfinishedTab.onClick, view.SelectQuestTab, false);
        UnityEventTools.AddBoolPersistentListener(completedTab.onClick, view.SelectQuestTab, true);
        SetReference(settings, "_unfinishedTab", unfinishedTab);
        SetReference(settings, "_completedTab", completedTab);

        SerializedProperty rows = settings.FindProperty("_questRows");
        rows.arraySize = 5;
        for (int index = 0; index < rows.arraySize; index++)
        {
            Button button = Button("Quest_" + index, quests, font, string.Empty,
                new Vector2(375, 133 - index * 64), new Vector2(300, 56));
            SerializedProperty row = rows.GetArrayElementAtIndex(index);
            row.FindPropertyRelative("Button").objectReferenceValue = button;
            row.FindPropertyRelative("Label").objectReferenceValue = button.GetComponentInChildren<TMP_Text>(true);
            UnityEventTools.AddIntPersistentListener(button.onClick, view.SelectQuest, index);
        }
        GameObject shopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefabPath)
            ?? throw new MissingReferenceException("미리 구성한 상점 프리팹 필요");
        var shopRoot = (GameObject)PrefabUtility.InstantiatePrefab(shopPrefab, panel.transform);
        SetReference(settings, "_shopView", shopRoot.GetComponent<MerchantShopView>());
        settings.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(shop.onClick, view.RequestShop);
        UnityEventTools.AddPersistentListener(openQuests.onClick, view.RequestQuests);
        UnityEventTools.AddPersistentListener(close.onClick, view.RequestClose);
        UnityEventTools.AddPersistentListener(back.onClick, view.RequestMenu);
        UnityEventTools.AddPersistentListener(action.onClick, view.RequestAction);
        view.Close();
        return view;
    }

    private static void ConfigureNpc(TMP_FontAsset font)
    {
        GameObject root = GameObject.Find("TravelMerchant");
        if (root == null)
        {
            root = PrefabUtility.LoadPrefabContents(MerchantPrefabPath);
            try
            {
                BuildNpc(root, font);
                PrefabUtility.SaveAsPrefabAsset(root, MerchantPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MerchantPrefabPath));
            root.name = "TravelMerchant";
        }
        Collider2D floor = GameObject.Find("PhysicsPlatforms/GroundPlatforms/Platform_01").GetComponent<Collider2D>();
        root.transform.position = new Vector3(-3.5f, floor.bounds.max.y, 0);
        var map = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<MapManager>());
        SetReference(map, "_merchantController", root.GetComponent<MerchantInteractionController>());
        map.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildNpc(GameObject root, TMP_FontAsset font)
    {
        root.name = "TravelMerchant";
        var area = root.AddComponent<CircleCollider2D>();
        area.isTrigger = true;
        area.radius = 2f;
        area.offset = new Vector2(0, 1f);
        var npc = root.AddComponent<MerchantNpc>();
        var controller = root.AddComponent<MerchantInteractionController>();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MerchantSpritePath);
        var visual = new GameObject("Visual", typeof(SpriteRenderer));
        visual.transform.SetParent(root.transform, false);
        var renderer = visual.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 30;
        float scale = MerchantVisualHeight / sprite.bounds.size.y;
        visual.transform.localScale = Vector3.one * scale;
        // 이미지 사각형 대신 발바닥에 설정된 피벗 기준 유지
        visual.transform.localPosition = Vector3.zero;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MerchantMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(material, MerchantMaterialPath);
        }
        material.mainTexture = sprite.texture;
        renderer.sharedMaterial = material;
        EditorUtility.SetDirty(material);
        var hint = new GameObject("InteractionHint", typeof(TextMeshPro));
        hint.transform.SetParent(root.transform, false);
        hint.transform.localPosition = new Vector3(0, MerchantVisualHeight + HintClearance, 0);
        var text = hint.GetComponent<TextMeshPro>();
        text.font = font;
        text.text = "F : 대화";
        text.fontSize = 3f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = TextColor;
        text.rectTransform.sizeDelta = new Vector2(5, 0.6f);
        hint.GetComponent<MeshRenderer>().sortingOrder = 100;
        hint.SetActive(false);
        ApplyAppearance(root);
        var npcSettings = new SerializedObject(npc);
        SetReference(npcSettings, "_interactionArea", area);
        SetReference(npcSettings, "_interactionHint", hint);
        npcSettings.ApplyModifiedPropertiesWithoutUndo();
        var controllerSettings = new SerializedObject(controller);
        SetReference(controllerSettings, "_npc", npc);
        SerializedProperty ids = controllerSettings.FindProperty("_questIds");
        ids.arraySize = 5;
        for (int index = 0; index < 5; index++)
        {
            ids.GetArrayElementAtIndex(index).intValue = 1001 + index;
        }
        int[] productIds = new LocalGameDataProvider().CreateItemTable().Values
            .Where(item => (item.GetItemType() == GameItemType.Consumable || item.GetItemType() == GameItemType.Equipment) && item.ID != 3003 && item.ID != 5002)
            .OrderBy(item => item.ID).Select(item => item.ID).ToArray();
        SerializedProperty products = controllerSettings.FindProperty("_shopItemIds");
        products.arraySize = productIds.Length;
        for (int index = 0; index < productIds.Length; index++) { products.GetArrayElementAtIndex(index).intValue = productIds[index]; }
        controllerSettings.FindProperty("_sellRate").floatValue = 0.5f;
        controllerSettings.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 기존 상인 프리팹의 외형과 안내판만 갱신
    /// 상호작용 콜라이더와 대화 연결 보존
    /// </summary>
    [MenuItem("Tools/NPC/Apply Merchant Appearance")]
    public static void ApplyMerchantAppearance()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MerchantPrefabPath);
        try
        {
            ApplyAppearance(root);
            PrefabUtility.SaveAsPrefabAsset(root, MerchantPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ApplyAppearance(GameObject root)
    {
        Transform visual = root.transform.Find("Visual");
        var renderer = visual.GetComponent<SpriteRenderer>();
        // 발바닥 피벗을 유지한 외형만 축소
        visual.localPosition = Vector3.zero;
        visual.localScale = Vector3.one * (MerchantVisualHeight / renderer.sprite.bounds.size.y);

        Transform hint = root.transform.Find("InteractionHint");
        hint.localPosition = new Vector3(0f, MerchantVisualHeight + HintClearance, 0f);
        var text = hint.GetComponent<TextMeshPro>();
        text.rectTransform.sizeDelta = new Vector2(1.4f, 0.4f);
        text.color = TextColor;
        text.GetComponent<MeshRenderer>().sortingOrder = 100;

        // 기존 안내 오브젝트의 활성 상태로 글자와 배경을 함께 제어
        Transform background = hint.Find("Background");
        if (background == null)
        {
            background = new GameObject("Background", typeof(SpriteRenderer)).transform;
            background.SetParent(hint, false);
        }
        var backdrop = background.GetComponent<SpriteRenderer>();
        backdrop.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HintBackgroundPath);
        backdrop.sharedMaterial = renderer.sharedMaterial;
        backdrop.sortingLayerID = text.GetComponent<MeshRenderer>().sortingLayerID;
        backdrop.sortingOrder = 99;
        Vector3 size = backdrop.sprite.bounds.size;
        background.localPosition = Vector3.zero;
        background.localRotation = Quaternion.identity;
        background.localScale = new Vector3(1.8f / size.x, 0.55f / size.y, 1f);
    }

    private static TMP_Text ScrollText(string name, Transform parent, TMP_FontAsset font, Vector2 position, Vector2 size)
    {
        Image viewport = Image(name, parent, new Color(0.075f, 0.14f, 0.15f));
        Place(viewport.rectTransform, size, position);
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        TMP_Text text = Label("Content", viewport.transform, font, string.Empty, 22f, Vector2.zero, Vector2.zero);
        RectTransform content = text.rectTransform;
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = new Vector2(18, 0);
        content.offsetMax = new Vector2(-18, -14);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.overflowMode = TextOverflowModes.Overflow;
        ContentSizeFitter fitter = text.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport.rectTransform;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return text;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        return (RectTransform)root.transform;
    }

    private static Image Image(string name, Transform parent, Color color)
    {
        RectTransform rect = Rect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string text, float size, Vector2 position, Vector2 dimensions)
    {
        RectTransform rect = Rect(name, parent);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = TextColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        Place(rect, dimensions, position);
        return label;
    }

    private static Button Button(string name, Transform parent, TMP_FontAsset font, string text, Vector2 position, Vector2 size)
    {
        Image image = Image(name, parent, ButtonColor);
        Place(image.rectTransform, size, position);
        Button button = image.gameObject.AddComponent<AnimatedButton>();
        button.gameObject.AddComponent<ButtonHoverMotion>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        TMP_Text label = Label("Label", button.transform, font, text, 22f, Vector2.zero, Vector2.zero);
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

    private static void SetReference(SerializedObject target, string name, UnityEngine.Object value)
    {
        target.FindProperty(name).objectReferenceValue = value;
    }
}
