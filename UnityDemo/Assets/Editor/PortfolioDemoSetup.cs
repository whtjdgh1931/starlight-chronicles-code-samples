using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PortfolioDemoSetup
{
    private const string ScenePath = "Assets/Scenes/PortfolioDemo.unity";

    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Scenes");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Demo Camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(12, 16, 26, 255);
        camera.orthographic = true;
        cameraObject.transform.position = new Vector3(0, 0, -10);
        var demo = new GameObject("Portfolio Demo").AddComponent<PortfolioDemo>();
        demo.CheckScenario();

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            throw new IOException("Could not save " + ScenePath);
        }

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Scene reloadedScene = EditorSceneManager.OpenScene(ScenePath);
        bool found = false;
        foreach (GameObject root in reloadedScene.GetRootGameObjects())
        {
            if (root.TryGetComponent(out PortfolioDemo reloaded))
            {
                reloaded.CheckScenario();
                found = true;
            }
        }

        if (!found) { throw new IOException("Reloaded scene has no demo component"); }
        Debug.Log("PORTFOLIO_DEMO_CHECKS_PASSED");
    }
}
