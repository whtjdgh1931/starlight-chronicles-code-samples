using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;
using Cinemachine;
public static class ForestSceneSetup
{
    private const string ArtFolder = "Assets/Art/GeneratedBackgrounds/";

    /// <summary>
    /// 사냥터 확장과 두 맵의 배경·상하 카메라 여유 구성
    /// 기존 플랫폼 위치와 게임 데이터 보존
    /// </summary>
    [MenuItem("Tools/Art/Configure Forest And Outpost")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("플레이 종료와 열린 씬 저장 후 실행 필요");
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string name in new[] { "HuntScene", "HostScene" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/00_Scenes/" + name + ".unity");
                bool hunt = name == "HuntScene";
                var boundary = UnityEngine.Object.FindAnyObjectByType<MapCameraBounds>();
                var settings = new SerializedObject(boundary);
                Rect bounds = hunt ? new Rect(-25f, -16f, 50f, 34f) : new Rect(-13f, -10f, 27f, 42f);
                settings.FindProperty("_mapBounds").rectValue = bounds;
                settings.FindProperty("_limitVertically").boolValue = true;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var vcam = boundary.GetComponent<CinemachineVirtualCamera>();
                var body = vcam.GetCinemachineComponent<CinemachineFramingTransposer>();
                if (body != null)
                {
                    body.m_DeadZoneHeight = 0.05f;
                    body.m_YDamping = 0.3f;
                    EditorUtility.SetDirty(body);
                }
                if (hunt) { ExpandHuntingGround(); ExpandVerticalRoutes(); }
                ConfigureBackground(hunt ? "HuntEnchantedForest.png" : "HostStarlightOutpost.png", bounds);
                if (!hunt)
                {
                    var old = GameObject.Find("WaitingRoomBackdrop");
                    if (old != null) old.GetComponent<TilemapRenderer>().enabled = false;
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("씬 저장 실패: " + name);
                Capture(name);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("FOREST_OUTPOST_SETUP_OK: Hunt width 34 -> 50, Hunt vertical 34, Host vertical 42");
        }
        finally
        {
            if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }

    private static void ExpandHuntingGround()
    {
        var tilemap = GameObject.Find("GroundTilemap").GetComponent<Tilemap>();
        // 기존 지면 양끝의 4개 행을 확장 구간에 복사
        for (int y = -8; y < -4; y++)
        {
            TileBase left = tilemap.GetTile(new Vector3Int(-17, y, 0));
            TileBase right = tilemap.GetTile(new Vector3Int(16, y, 0));
            for (int x = -25; x < -17; x++) tilemap.SetTile(new Vector3Int(x, y, 0), left);
            for (int x = 17; x < 25; x++) tilemap.SetTile(new Vector3Int(x, y, 0), right);
        }
        var floor = GameObject.Find("PhysicsPlatforms/GroundPlatforms/Platform_01");
        var edge = floor.GetComponent<EdgeCollider2D>();
        edge.points = new[] {
            (Vector2)floor.transform.InverseTransformPoint(new Vector3(-25, -4, 0)),
            (Vector2)floor.transform.InverseTransformPoint(new Vector3(25, -4, 0)) };
        // 최하단 지면은 아래로 통과하지 않는 충돌면
        edge.usedByEffector = false;
        floor.GetComponent<PlatformEffector2D>().enabled = false;
        MoveX("LeftWall", -26.5f);
        MoveX("RightWall", 26.5f);
        MoveX("Portal_ToHome", -23.8f);
        MoveX("Portal_ToBossWaitingRoom", 23.8f);
        AddClearing("WesternClearing", "WesternForestExtension", new Vector3(-20f, -2.5f, 0));
        AddClearing("EasternHuntingClearing", "EasternForestExtension", new Vector3(20f, -1.2f, 0));
        tilemap.CompressBounds();
    }

    /// <summary>
    /// 기존 지면 아래 숲길과 나무 위로 이어지는 계단형 동선 구성
    /// 일반 점프 높이보다 낮은 1.5 단위 간격으로 연결
    /// </summary>
    private static void ExpandVerticalRoutes()
    {
        Tilemap ground = GameObject.Find("GroundTilemap").GetComponent<Tilemap>();
        TileBase top = ground.GetTile(new Vector3Int(0, -5, 0));
        TileBase fill = ground.GetTile(new Vector3Int(0, -7, 0));
        if (fill == null) fill = ground.GetTile(new Vector3Int(0, -12, 0));
        if (top == null || fill == null) throw new InvalidOperationException("사냥터 지면 원본 타일 필요");
        for (int x = -25; x < 25; x++)
        {
            for (int y = -8; y < -5; y++) ground.SetTile(new Vector3Int(x, y, 0), null);
            for (int y = -14; y < -10; y++) ground.SetTile(new Vector3Int(x, y, 0), y == -11 ? top : fill);
        }
        var middle = GameObject.Find("PhysicsPlatforms/GroundPlatforms/Platform_01");
        middle.GetComponent<EdgeCollider2D>().usedByEffector = true;
        middle.GetComponent<PlatformEffector2D>().enabled = true;
        var bottom = GameObject.Find("LowerForestFloor");
        if (bottom == null)
        {
            bottom = new GameObject("LowerForestFloor", typeof(BoxCollider2D));
            bottom.transform.SetParent(middle.transform.parent, false);
        }
        bottom.layer = Constants.LAYER_Ground;
        bottom.tag = Constants.TAG_Ground;
        bottom.transform.position = new Vector3(0, -12, 0);
        bottom.GetComponent<BoxCollider2D>().size = new Vector2(50, 4);

        // 아래 길에서 기존 지면으로 복귀 가능한 좌우 동선
        AddClearing("MossySteppingStone", "LowerWestStep1", new Vector3(-19, -8.5f, 0));
        AddClearing("WesternClearing", "LowerWestRest", new Vector3(-16, -7, 0));
        AddClearing("MossySteppingStone", "LowerWestStep2", new Vector3(-13, -5.5f, 0));
        AddClearing("MossySteppingStone", "LowerEastStep1", new Vector3(19, -8.5f, 0));
        AddClearing("EasternHuntingClearing", "LowerEastRest", new Vector3(16, -7, 0));
        AddClearing("MossySteppingStone", "LowerEastStep2", new Vector3(13, -5.5f, 0));

        // 기존 전망대에서 시작하는 위쪽 순환 동선
        AddClearing("MossySteppingStone", "CanopyWestStep1", new Vector3(-5, 2, 0));
        AddClearing("WesternClearing", "CanopyWestRest", new Vector3(-8, 3.5f, 0));
        AddClearing("MossySteppingStone", "CanopyWestStep2", new Vector3(-12, 5, 0));
        AddClearing("WesternClearing", "CanopyWestUpper", new Vector3(-9, 6.5f, 0));
        AddClearing("MossySteppingStone", "CanopyCenterStep1", new Vector3(-5, 8, 0));
        AddClearing("TimberCrossing", "CanopyBridge", new Vector3(0, 9.5f, 0));
        AddClearing("MossySteppingStone", "CanopyCenterStep2", new Vector3(4, 11, 0));
        AddClearing("EasternHuntingClearing", "CanopyEastUpper", new Vector3(8, 9.5f, 0));
        AddClearing("MossySteppingStone", "CanopyEastStep3", new Vector3(12, 8, 0));
        AddClearing("EasternHuntingClearing", "CanopyEastRest", new Vector3(15, 6.5f, 0));
        AddClearing("MossySteppingStone", "CanopyEastStep2", new Vector3(11, 5, 0));
        AddClearing("EasternHuntingClearing", "CanopyEastLanding", new Vector3(8, 3.5f, 0));
        AddClearing("MossySteppingStone", "CanopyEastStep1", new Vector3(5, 1.8f, 0));
        ground.CompressBounds();
    }

    private static void MoveX(string name, float x)
    {
        Transform target = GameObject.Find(name).transform;
        Vector3 position = target.position;
        position.x = x;
        target.position = position;
    }

    private static void AddClearing(string source, string name, Vector3 position)
    {
        GameObject root = GameObject.Find(name);
        if (root == null)
        {
            GameObject original = GameObject.Find(source);
            root = UnityEngine.Object.Instantiate(original, original.transform.parent);
            root.name = name;
        }
        root.transform.position = position;
    }

    private static void ConfigureBackground(string file, Rect bounds)
    {
        string path = ArtFolder + file;
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.maxTextureSize = 4096;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var root = GameObject.Find("CameraCoverageBackdrop");
        if (root == null) root = new GameObject("CameraCoverageBackdrop", typeof(SpriteRenderer));
        var renderer = root.GetComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        foreach (var sprite in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include))
        {
            if (sprite.sprite == null) continue;
            if (sprite == renderer || sprite.sharedMaterial.name == "BackgroundUnlit" ||
                sprite.sharedMaterial.name == "ForestSpriteUnlit")
            {
                string texturePath = AssetDatabase.GetAssetPath(sprite.sprite.texture);
                string materialPath = ArtFolder + "Sprite_" + AssetDatabase.AssetPathToGUID(texturePath) + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.mainTexture = sprite.sprite.texture;
                EditorUtility.SetDirty(material);
                sprite.sharedMaterial = material;
            }
        }
        renderer.sortingOrder = -1000;
        float scale = Mathf.Max(bounds.width / renderer.sprite.bounds.size.x, bounds.height / renderer.sprite.bounds.size.y);
        root.transform.position = new Vector3(bounds.center.x, bounds.center.y, 10f);
        root.transform.localScale = new Vector3(scale, scale, 1);
    }

    private static void Capture(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int outputIndex = Array.IndexOf(args, "-artPreviewOutput");
        if (outputIndex < 0 || outputIndex + 1 >= args.Length) return;
        string output = args[outputIndex + 1];
        Camera camera = Camera.main;
        camera.transform.position = new Vector3(0, 1, -10);
        camera.orthographicSize = 6;
        var target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
            new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        texture.Apply();
        File.WriteAllBytes(Path.Combine(output, name + "-background-preview.png"), texture.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(texture);
        UnityEngine.Object.DestroyImmediate(target);
    }
}
