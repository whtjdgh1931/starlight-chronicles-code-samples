using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// 생성한 판타지 배경과 타이틀 UI의 씬 연결
/// </summary>
public static class FantasySceneArtSetup
{
    private const string Folder = "Assets/Art/GeneratedBackgrounds/";

    [MenuItem("Tools/Art/Configure Fantasy Scene Backgrounds")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 배경 연결 필요");
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                throw new InvalidOperationException("열린 씬 저장 후 배경 연결 필요");
            }
        }
        Import("TitleVillage.png");
        Import("BossMoonlitRuins.png");
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene title = EditorSceneManager.OpenScene("Assets/00_Scenes/TitleScene.unity");
            ConfigureTitle();
            EditorSceneManager.SaveScene(title);
            Capture("title-preview.png", true);
            Scene boss = EditorSceneManager.OpenScene("Assets/00_Scenes/BossScene.unity");
            ConfigureBoss();
            EditorSceneManager.SaveScene(boss);
            Capture("boss-preview.png", false);
        }
        finally
        {
            if (previous.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("FANTASY_SCENE_ART_SETUP_OK");
    }

    private static void Import(string name)
    {
        string path = Folder + name;
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static void ConfigureTitle()
    {
        TitleSceneController controller = UnityEngine.Object.FindAnyObjectByType<TitleSceneController>();
        Button button = (Button)new SerializedObject(controller).FindProperty("_startButton").objectReferenceValue;
        Canvas canvas = button.GetComponentInParent<Canvas>();
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        Image background = Image("FantasyTitleBackground", canvas.transform, new Color(1, 1, 1, 1));
        background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "TitleVillage.png");
        RectTransform backdrop = background.rectTransform;
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
        var aspect = background.GetComponent<AspectRatioFitter>() ?? background.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        aspect.aspectRatio = background.sprite.rect.width / background.sprite.rect.height;
        backdrop.SetAsFirstSibling();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/07_Font/White.asset");
        Image plate = Image("TitlePlate", canvas.transform, new Color(0.035f, 0.1f, 0.16f, 0.38f));
        Place(plate.rectTransform, new Vector2(0.5f, 0.66f), new Vector2(970, 300));
        TextMeshProUGUI heading = Text("GameTitle", canvas.transform, font, Application.productName, 64, new Color(1f, 0.93f, 0.73f));
        Place(heading.rectTransform, new Vector2(0.5f, 0.7f), new Vector2(1100, 150));
        heading.fontStyle = FontStyles.Bold;
        heading.characterSpacing = 3;
        heading.enableAutoSizing = true;
        heading.fontSizeMin = 40;
        heading.fontSizeMax = 64;
        TextMeshProUGUI subtitle = Text("GameSubtitle", canvas.transform, font, "싱글 성장 / 협동 보스전", 30, Color.white);
        Place(subtitle.rectTransform, new Vector2(0.5f, 0.58f), new Vector2(900, 70));
        Place((RectTransform)button.transform, new Vector2(0.5f, 0.28f), new Vector2(360, 86));
        Image graphic = button.GetComponent<Image>();
        graphic.color = new Color(0.06f, 0.22f, 0.25f, 0.96f);
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.76f, 0.94f, 0.85f);
        colors.pressedColor = new Color(0.6f, 0.78f, 0.72f);
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        label.text = "모험 시작";
        label.font = font;
        label.fontSize = 34;
        label.color = new Color(1, 0.95f, 0.8f);
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        button.transform.SetAsLastSibling();
    }

    private static void ConfigureBoss()
    {
        MapCameraBounds boundary = UnityEngine.Object.FindAnyObjectByType<MapCameraBounds>();
        Rect bounds = new SerializedObject(boundary).FindProperty("_mapBounds").rectValue;
        GameObject root = GameObject.Find("FantasyBossBackground");
        if (root == null)
        {
            root = new GameObject("FantasyBossBackground", typeof(SpriteRenderer));
        }
        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "BossMoonlitRuins.png");
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        string materialPath = Folder + "BackgroundUnlit.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        renderer.sharedMaterial = material;
        renderer.sortingOrder = -30;
        float scale = Mathf.Max(bounds.width / renderer.sprite.bounds.size.x, bounds.height / renderer.sprite.bounds.size.y);
        root.transform.position = new Vector3(bounds.center.x, bounds.center.y - renderer.sprite.bounds.size.y * scale * 0.12f, 5);
        root.transform.localScale = new Vector3(scale, scale, 1);
        GameObject.Find("BossRoomBackdrop").GetComponent<TilemapRenderer>().enabled = false;
    }

    private static Image Image(string name, Transform parent, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject root = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        Image image = root.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, string content, float size, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject root = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false);
        TextMeshProUGUI label = root.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = content;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private static void Capture(string name, bool title)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-artPreviewOutput");
        if (index < 0)
        {
            return;
        }
        if (title)
        {
            UnityEngine.Object.FindAnyObjectByType<UIManager>().PlayerUI.gameObject.SetActive(false);
        }
        if (title)
        {
            TitleSceneController controller = UnityEngine.Object.FindAnyObjectByType<TitleSceneController>();
            Button button = (Button)new SerializedObject(controller).FindProperty("_startButton").objectReferenceValue;
            Canvas titleCanvas = button.GetComponentInParent<Canvas>();
            foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>())
            {
                if (canvas != titleCanvas)
                {
                    canvas.gameObject.SetActive(false);
                }
            }
        }
        Camera camera = Camera.main;
        if (!title)
        {
            Rect bounds = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<MapCameraBounds>()).FindProperty("_mapBounds").rectValue;
            camera.orthographicSize = Mathf.Min(camera.orthographicSize, bounds.height * 0.5f, bounds.width / (2f * (1920f / 1080f)));
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, camera.transform.position.z);
            foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>())
            {
                canvas.gameObject.SetActive(false);
            }
        }
        var target = new RenderTexture(1920, 1080, 24);
        camera.targetTexture = target;
        foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>())
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
            }
        }
        Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target;
        var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        texture.Apply();
        Directory.CreateDirectory(args[index + 1]);
        File.WriteAllBytes(Path.Combine(args[index + 1], name), texture.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(target);
    }
}
