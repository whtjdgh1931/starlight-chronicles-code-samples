using UnityEngine;
using UnityEngine.InputSystem;

public class EquipTester : MonoBehaviour
{

    private enum EnhanceTestResult
    {
        Success,
        Drop,
        Destroy,
        Fail
    }


    private const int FirstWeaponId = 3001;
    private const int SecondWeaponId = 3002;
    private const int ArmorId = 5001;

    /// <summary>
    /// 다음 강화 결과를 성공으로 고정합니다.
    /// </summary>
    public void ForceEnhanceSuccess()
    {
        SetEnhanceTestResult(EnhanceTestResult.Success);
    }

    /// <summary>
    /// 다음 강화 결과를 하락으로 고정합니다.
    /// </summary>
    public void ForceEnhanceDrop()
    {
        SetEnhanceTestResult(EnhanceTestResult.Drop);
    }

    /// <summary>
    /// 다음 강화 결과를 파괴로 고정합니다.
    /// </summary>
    public void ForceEnhanceDestroy()
    {
        SetEnhanceTestResult(EnhanceTestResult.Destroy);
    }

    /// <summary>
    /// 다음 강화 결과를 일반 실패로 고정합니다.
    /// </summary>
    public void ForceEnhanceFail()
    {
        SetEnhanceTestResult(EnhanceTestResult.Fail);
    }

    /// <summary>
    /// 장비 인벤토리에서 지정한 ID의 실제 아이템 인스턴스를 찾습니다.
    /// 강화 수치와 파괴 상태가 보존된 인스턴스를 반환합니다.
    /// </summary>
    private static bool TryGetOwnedEquipment(
        PlayerInventory inventory,
        int itemId,
        out InventoryItemData ownedItem)
    {
        InventoryItemData[] items = inventory.GetListByTab(InventoryTab.Equipment);
        int slotCount = inventory.GetCurrentSlotCount(InventoryTab.Equipment);

        for (int i = 0; i < slotCount; i++)
        {
            InventoryItemData candidate = items[i];

            if (candidate != null && !candidate.IsEmpty && candidate.ItemData.ID == itemId)
            {
                ownedItem = candidate;
                return true;
            }
        }

        ownedItem = null;
        return false;
    }

    /// <summary>
    /// 플레이어 인벤토리가 보유한 장비 인스턴스로 장착을 요청합니다.
    /// </summary>
    private void TryEquipOwnedItem(int itemId)
    {
        GameObject playerObject =
                GameObject.FindWithTag(Constants.TAG_Player);

        if (playerObject == null ||
                !playerObject.TryGetComponent(
                        out Player player))
        {
            return;
        }

        if (!TryGetOwnedEquipment(
                        player.PlayerInventory,
                        itemId,
                        out InventoryItemData ownedItem))
        {
            Debug.LogWarning(
                    $"[테스트] 인벤토리에 장비가 없습니다. ID: {itemId}");
            return;
        }

        EventBus.PublishEquipRequested(ownedItem);
        LogPlayerStats();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            TryEquipOwnedItem(FirstWeaponId);
        }

        if (keyboard.digit4Key.wasPressedThisFrame)
        {
            TryEquipOwnedItem(SecondWeaponId);
        }

        if (keyboard.digit2Key.wasPressedThisFrame)
        {
            TryEquipOwnedItem(ArmorId);
        }

        if (keyboard.digit3Key.wasPressedThisFrame)
        {
            EventBus.PublishUnEquipRequested(
                    EquipSlot.Weapon);

            LogPlayerStats();
        }

        if (keyboard.digit5Key.wasPressedThisFrame)
        {
            SetEnhanceTestResult(EnhanceTestResult.Success);
        }

        if (keyboard.digit6Key.wasPressedThisFrame)
        {
            SetEnhanceTestResult(EnhanceTestResult.Drop);
        }

        if (keyboard.digit7Key.wasPressedThisFrame)
        {
            SetEnhanceTestResult(EnhanceTestResult.Destroy);
        }

        if (keyboard.digit8Key.wasPressedThisFrame)
        {
            SetEnhanceTestResult(EnhanceTestResult.Fail);
        }
    }

    private void LogPlayerStats()
    {
        GameObject playerObj = GameObject.FindWithTag(Constants.TAG_Player);
        if (playerObj == null || !playerObj.TryGetComponent<Player>(out var player) || player.PlayerStat == null)
        {
            return;
        }
        float hp = player.PlayerStat.GetTotalStat(StatType.MaxHp);
        float atk = player.PlayerStat.GetTotalStat(StatType.Atk);
        float def = player.PlayerStat.GetTotalStat(StatType.Def);
        Debug.Log($"[최종 스탯] HP: {hp} | ATK: {atk} | DEF: {def}");
    }

    private static void SetEnhanceTestResult(EnhanceTestResult result)
    {
        DataManager dataManager = GameManager.Instance.DataManager;

        foreach (EnhanceData enhanceData in dataManager.EnhanceProbTable.Values)
        {
            enhanceData.SuccessRate = result == EnhanceTestResult.Success ? 1f : 0f;
            enhanceData.DropRate = result == EnhanceTestResult.Drop ? 1f : 0f;
            enhanceData.DestroyRate = result == EnhanceTestResult.Destroy ? 1f : 0f;
        }

        Debug.Log($"[강화 테스트] 다음 강화 결과: {result}");
    }
}
