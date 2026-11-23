using UnityEditor;
using UnityEngine;

// 이 스크립트는 InventoryTester 컴포넌트의 인스펙터 화면을 덮어씌웁니다.
[CustomEditor(typeof(InventoryTester))]
public class InventoryTesterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 기존에 있던 변수(PlayerInventory 연결 칸 등)를 기본적으로 그려줍니다.
        base.OnInspectorGUI();

        // 우리가 다루고 있는 타겟 스크립트 가져오기
        InventoryTester tester = (InventoryTester)target;

        EditorGUILayout.Space(15); // 빈 공간 여백 주기
        EditorGUILayout.LabelField("🛠️ 인벤토리 확장 테스트", EditorStyles.boldLabel);

        // 버튼들을 가로로 나란히 배치하기 시작
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("장비 4칸 확장", GUILayout.Height(35)))
        {
            tester.DebugExpandEquipment();
        }
        if (GUILayout.Button("소비 4칸 확장", GUILayout.Height(35)))
        {
            tester.DebugExpandConsumable();
        }
        GUILayout.EndHorizontal(); // 가로 배치 끝

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🛠️ 아이템 획득 테스트", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("장비(3001) 획득", GUILayout.Height(35)))
        {
            tester.DebugAddEquipment();
        }
        if (GUILayout.Button("소비(2001) 10개 획득", GUILayout.Height(35)))
        {
            tester.DebugAddConsumable();
        }
        GUILayout.EndHorizontal();
    }
}
