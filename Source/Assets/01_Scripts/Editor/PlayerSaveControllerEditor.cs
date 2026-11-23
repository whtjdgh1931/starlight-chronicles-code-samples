using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerSaveController))]
public sealed class PlayerSaveControllerEditor : Editor
{
    private static void DrawButtons(PlayerSaveController controller)
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("저장", GUILayout.Height(32)))
        {
            TrySave(controller);
        }

        if (GUILayout.Button("불러오기", GUILayout.Height(32)))
        {
            Load(controller);
        }

        EditorGUILayout.EndHorizontal();
    }

    private static void TrySave(PlayerSaveController controller)
    {
        if (controller.TrySave(out string errorMessage))
        {
            Debug.Log("플레이어 데이터 저장에 성공했습니다.", controller);
            return;
        }

        Debug.LogError($"플레이어 데이터 저장에 실패했습니다. {errorMessage}", controller);
    }

    private static void Load(PlayerSaveController controller)
    {
        CharacterSaveReadResult result = controller.Load(out string errorMessage);

        switch (result)
        {
            case CharacterSaveReadResult.Success:
                Debug.Log("본 저장 파일을 불러왔습니다.", controller);
                break;

            case CharacterSaveReadResult.BackupSuccess:
                Debug.LogWarning("백업 저장 파일을 불러왔습니다.", controller);
                break;

            case CharacterSaveReadResult.NotFound:
                Debug.LogWarning("불러올 저장 파일이 없습니다.", controller);
                break;

            case CharacterSaveReadResult.Failed:
                Debug.LogError($"저장 데이터 불러오기에 실패했습니다. {errorMessage}", controller);
                break;
        }
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        PlayerSaveController controller = (PlayerSaveController)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("저장 시스템 테스트", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "저장과 불러오기는 Play Mode에서만 테스트할 수 있습니다.",
                MessageType.Info);
        }
        else if (!controller.IsInitialized)
        {
            EditorGUILayout.HelpBox(
                "저장 시스템 초기화를 기다리고 있습니다.",
                MessageType.Info);
        }

        bool canExecute = Application.isPlaying && controller.IsInitialized;

        using (new EditorGUI.DisabledScope(!canExecute))
        {
            DrawButtons(controller);
        }
    }
}
