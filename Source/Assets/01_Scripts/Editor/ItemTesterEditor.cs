using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemTester))]
public class ItemTesterEditor : Editor
{
    private void AddTestItem(int itemId, int count)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("게임 실행 중(Play Mode)일 때만 아이템을 추가할 수 있습니다!");
            return;
        }

        PlayerInventory inventory = FindAnyObjectByType<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogWarning("PlayerInventory를 찾을 수 없습니다.");
            return;
        }

        var dataManager = GameManager.Instance?.DataManager;
        if (dataManager != null && dataManager.ItemTable.TryGetValue(itemId, out var item))
        {
            inventory.AddItem(item, count);
            Debug.Log($"<color=green>[테스트]</color> '{item.Name}' 아이템 {count}개가 추가되었습니다.");
        }
        else
        {
            Debug.LogWarning($"DataManager에서 ID {itemId}인 아이템을 찾을 수 없습니다. 구글 시트 데이터가 업로드/다운로드되었는지 확인해주세요.");
        }
    }

    public override void OnInspectorGUI()
    {
        // 1. 상단의 ItemId와 Count 입력 필드를 렌더링합니다.
        base.OnInspectorGUI();

        ItemTester tester = (ItemTester)target;

        EditorGUILayout.Space(15);
        GUILayout.Label("[1] 수동 입력 추가", EditorStyles.boldLabel);

        // 2. 인펙터에 입력한 ID와 개수로 아이템을 추가하는 버튼
        if (GUILayout.Button($"ID {tester.ItemId} 아이템 {tester.Count}개 추가", GUILayout.Height(30)))
        {
            AddTestItem(tester.ItemId, tester.Count);
        }

        EditorGUILayout.Space(15);
        GUILayout.Label("[2] 빠른 프리셋 추가 (ID 입력 없이 즉시 획득)", EditorStyles.boldLabel);

        // 3. 소비 아이템 프리셋 버튼
        if (GUILayout.Button("소비 아이템(빨간포션) 10개 추가", GUILayout.Height(35)))
        {
            AddTestItem(2001, 10);
        }

        EditorGUILayout.Space(10);

        // 4. 기타 아이템 프리셋 버튼
        if (GUILayout.Button("기타 아이템(달팽이의 껍질) 10개 추가", GUILayout.Height(35)))
        {
            AddTestItem(6001, 10);
        }
    }
}
