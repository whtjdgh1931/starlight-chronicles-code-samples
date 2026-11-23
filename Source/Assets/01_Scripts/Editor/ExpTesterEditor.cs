using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ExpTester))]
public class ExpTesterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI(); // 입력창 표시

        ExpTester tester = (ExpTester)target;
        GUILayout.Space(10);

        // 1번 버튼: 경험치 획득
        if (GUILayout.Button("경험치 획득 (Add Exp)", GUILayout.Height(30)))
        {
            tester.GiveExp();
        }

        GUILayout.Space(5);

        // 2번 버튼: 강제 레벨업
        if (GUILayout.Button("강제 레벨업 (Force Level Up)", GUILayout.Height(30)))
        {
            tester.ForceLevelUp();
        }
    }
}
