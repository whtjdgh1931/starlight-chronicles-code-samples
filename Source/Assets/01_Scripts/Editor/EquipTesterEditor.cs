using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EquipTester))]
public class EquipTesterEditor : Editor
{
    private static void DrawResultButtons(EquipTester tester)
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("성공", GUILayout.Height(32)))
        {
            tester.ForceEnhanceSuccess();
        }

        if (GUILayout.Button("하락", GUILayout.Height(32)))
        {
            tester.ForceEnhanceDrop();
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("파괴", GUILayout.Height(32)))
        {
            tester.ForceEnhanceDestroy();
        }

        if (GUILayout.Button("실패", GUILayout.Height(32)))
        {
            tester.ForceEnhanceFail();
        }

        EditorGUILayout.EndHorizontal();
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EquipTester tester = (EquipTester)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("강화 결과 테스트", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                    "결과를 선택한 뒤 강화창의 강화 버튼을 눌러 테스트합니다.",
                    MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            DrawResultButtons(tester);
        }
    }
}