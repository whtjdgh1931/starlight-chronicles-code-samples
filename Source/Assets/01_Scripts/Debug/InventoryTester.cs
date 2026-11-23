using UnityEngine;

/// <summary>
/// 인벤토리 확장이랑 아이템 획득 테스트용 (나중에 스크립트만 삭제하면 됨)
/// </summary>
public class InventoryTester : MonoBehaviour
{
    [Header("인벤토리 연결")]
    [SerializeField] private PlayerInventory _playerInventory;

    private void Awake()
    {
        // 인스펙터에서 연결 안 했을 경우를 대비해 자동 찾기
        if (_playerInventory == null)
            _playerInventory = FindAnyObjectByType<PlayerInventory>();
    }

    [ContextMenu("디버그: 장비 탭 4칸 확장")]
    public void DebugExpandEquipment()
    {
        if (_playerInventory != null)
        {
            _playerInventory.ExpandInventory(InventoryTab.Equipment, 4);
            Debug.Log("장비 탭 4칸 확장 완료!");
        }
    }

    [ContextMenu("디버그: 소비 탭 4칸 확장")]
    public void DebugExpandConsumable()
    {
        if (_playerInventory != null)
        {
            _playerInventory.ExpandInventory(InventoryTab.Consumable, 4);
            Debug.Log("소비 탭 4칸 확장 완료!");
        }
    }

    [ContextMenu("디버그: 장비(3001) 획득")]
    public void DebugAddEquipment()
    {
        var dataManager = GameManager.Instance?.DataManager;
        if (dataManager != null && dataManager.ItemTable.TryGetValue(3001, out var item))
        {
            _playerInventory?.AddItem(item, 1);
            // 사용자님의 ItemData.Name (대문자 N) 에 완벽히 맞췄습니다.
            Debug.Log($"테스트 장비 '{item.Name}' 획득 완료!");
        }
        else
        {
            Debug.LogWarning("DataManager에서 3001번 장비 아이템을 찾을 수 없습니다.");
        }
    }

    [ContextMenu("디버그: 소비템(2001) 10개 획득")]
    public void DebugAddConsumable()
    {
        var dataManager = GameManager.Instance?.DataManager;
        // 2001번이 실제 소비 아이템 ID가 맞는지 확인해 주세요!
        if (dataManager != null && dataManager.ItemTable.TryGetValue(2001, out var item))
        {
            _playerInventory?.AddItem(item, 10);
            Debug.Log($"테스트 소비템 '{item.Name}' 10개 획득 완료!");
        }
        else
        {
            Debug.LogWarning("DataManager에서 2001번 소비 아이템을 찾을 수 없습니다.");
        }
    }
}
