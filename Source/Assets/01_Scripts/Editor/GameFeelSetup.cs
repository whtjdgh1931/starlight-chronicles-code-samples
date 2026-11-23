using System;
using System.IO;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 카메라 표시 영역·배경과 싱글·온라인 플레이어 이동 수치 구성
/// 바닥·발판·충돌체 배치와 외부 애니메이션 원본 보존
/// </summary>
public static class GameFeelSetup
{
    private const float CameraHalfHeight = 6f;
    private const float GravityScale = 3f;
    private const float NormalJumpPower = 8.282512f;
    private const float HighJumpPower = 9.465727f;
    private const string BackdropName = "CameraCoverageBackdrop";

    [MenuItem("Tools/Gameplay/Configure Camera Movement And Audio")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
        {
            throw new InvalidOperationException("플레이 종료와 열린 씬 저장 후 조작감 구성 필요");
        }
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string name in new[] { "ServerPlayer", "RemotePlayer" })
            {
                string path = $"Assets/Resources/Prefabs/Character/{name}.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ConfigurePlayer(root.GetComponent<Player>());
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (string name in new[] { "HomeScene", "HostScene", "HuntScene", "BossScene" })
            {
                Scene scene = EditorSceneManager.OpenScene($"Assets/00_Scenes/{name}.unity", OpenSceneMode.Single);
                MapCameraBounds bounds = Object.FindAnyObjectByType<MapCameraBounds>();
                var settings = new SerializedObject(bounds);
                Rect rect = settings.FindProperty("_mapBounds").rectValue;
                rect.height = Mathf.Max(rect.height, CameraHalfHeight * 2f);
                settings.FindProperty("_mapBounds").rectValue = rect;
                settings.FindProperty("_limitVertically").boolValue = true;
                settings.ApplyModifiedPropertiesWithoutUndo();
                Camera camera = (Camera)settings.FindProperty("_outputCamera").objectReferenceValue;
                camera.orthographicSize = CameraHalfHeight;
                CinemachineVirtualCamera virtualCamera = bounds.GetComponent<CinemachineVirtualCamera>();
                LensSettings lens = virtualCamera.m_Lens;
                lens.OrthographicSize = CameraHalfHeight;
                virtualCamera.m_Lens = lens;
                foreach (Player player in Object.FindObjectsByType<Player>(FindObjectsInactive.Include))
                {
                    ConfigurePlayer(player);
                }
                if (name != "BossScene") { ConfigureBackdrop(scene, rect); }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) { throw new IOException($"{name}: 조작감 설정 저장 실패"); }
            }
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (previous.Length > 0) { EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }
        AudioSettingsSetup.Configure();
        Debug.Log("GAME_FEEL_SETUP_OK");
    }

    private static void ConfigurePlayer(Player player)
    {
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        body.gravityScale = GravityScale;
        var move = new SerializedObject(player.GetComponent<PlayerMove>());
        move.FindProperty("_useLocalJumpSettings").boolValue = true;
        move.FindProperty("_normalJumpPower").floatValue = NormalJumpPower * Mathf.Sqrt(GravityScale / 2f);
        move.FindProperty("_highJumpPower").floatValue = HighJumpPower * Mathf.Sqrt(GravityScale / 2f);
        move.FindProperty("_doubleJumpUpPower").floatValue = HighJumpPower;
        move.FindProperty("_groundAcceleration").floatValue = 70f;
        move.FindProperty("_groundDeceleration").floatValue = 95f;
        move.FindProperty("_airAcceleration").floatValue = 9f;
        move.FindProperty("_airDeceleration").floatValue = 7.5f;
        move.ApplyModifiedPropertiesWithoutUndo();
        var presentation = new SerializedObject(player.GetComponent<DashSkillPresentation>());
        presentation.FindProperty("_playerVisual").objectReferenceValue = player.GetComponent<PlayerVisual>();
        presentation.ApplyModifiedPropertiesWithoutUndo();
        var dash = new SerializedObject(player.GetComponent<PlayerDashSkillExecutor>());
        dash.FindProperty("_startupDuration").floatValue = 0.1f;
        dash.FindProperty("_recoveryDuration").floatValue = 0.1f;
        dash.FindProperty("_speedProfile").animationCurveValue = new AnimationCurve(
            new Keyframe(0f, 0.85f), new Keyframe(0.2f, 1f), new Keyframe(0.7f, 0.35f), new Keyframe(1f, 0f));
        dash.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(body);
    }

    private static void ConfigureBackdrop(Scene scene, Rect bounds)
    {
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == BackdropName);
        if (root == null) { root = new GameObject(BackdropName, typeof(SpriteRenderer)); }
        SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/GeneratedBackgrounds/PixelValleyBackdrop.png");
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/GeneratedBackgrounds/BackgroundUnlit.mat");
        renderer.sortingOrder = -1000;
        float scale = Mathf.Max(bounds.width / renderer.sprite.bounds.size.x, bounds.height / renderer.sprite.bounds.size.y);
        root.transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.center.y, 10f), Quaternion.identity);
        root.transform.localScale = new Vector3(scale, scale, 1f);
    }
}
