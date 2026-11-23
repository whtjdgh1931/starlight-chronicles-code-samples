using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 서버 플레이어 프리팹의 머리 위 HP바 생성과 참조 연결
/// </summary>
public static class PlayerHealthBarSetup
{
    private const string PrefabPath = "Assets/Resources/Prefabs/Character/ServerPlayer.prefab";
    private const string BarName = "PlayerHealthBar";
    private static readonly Vector2 BarSize = new(100f, 10f);
    private static readonly Vector3 BarPosition = new(0f, 0.85f, 0f);
    private const float BarScale = 0.01f;
    private const int SortingOrder = 100;

    [MenuItem("Tools/Network/Configure Player Health Bar")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 HP바 구성 필요");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform existing = root.transform.Find(BarName);
            PlayerHealthBar view = existing != null
                ? existing.GetComponent<PlayerHealthBar>() : CreateBar(root.transform);
            if (view == null)
            {
                throw new MissingReferenceException("기존 PlayerHealthBar 오브젝트의 표시 컴포넌트 필요");
            }

            var controller = new SerializedObject(root.GetComponent<NetworkPlayerController>());
            controller.FindProperty("_healthBar").objectReferenceValue = view;
            controller.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log("PLAYER_HEALTH_BAR_SETUP_OK");
    }

    /// <summary>
    /// 방향 전환용 시각 객체와 분리된 루트 자식 UI 구성
    /// 재실행 시 기존 배치와 색상 유지
    /// </summary>
    private static PlayerHealthBar CreateBar(Transform parent)
    {
        var bar = new GameObject(BarName, typeof(RectTransform), typeof(Canvas));
        var rect = (RectTransform)bar.transform;
        rect.SetParent(parent, false);
        rect.localPosition = BarPosition;
        rect.localScale = Vector3.one * BarScale;
        rect.sizeDelta = BarSize;
        Canvas canvas = bar.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;

        Image background = CreateImage("Background", rect, new Color(0.08f, 0.1f, 0.14f, 0.9f));
        Image fill = CreateImage("Fill", rect, new Color(0.25f, 0.85f, 0.4f, 1f));
        Slider slider = bar.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.targetGraphic = background;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };

        PlayerHealthBar view = bar.AddComponent<PlayerHealthBar>();
        var settings = new SerializedObject(view);
        settings.FindProperty("_root").objectReferenceValue = bar;
        settings.FindProperty("_slider").objectReferenceValue = slider;
        settings.ApplyModifiedPropertiesWithoutUndo();
        bar.SetActive(false);
        return view;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = child.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}
