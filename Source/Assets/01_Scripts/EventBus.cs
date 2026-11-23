using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이벤트 중계용 이벤트 버스
/// </summary>
public static class EventBus
{
    #region input event
    /// <summary>
    /// NPC 대화 열기·닫기 입력
    /// </summary>
    public static event Action OnNpcInteractInput;
    public static void PublishNpcInteractInput() => OnNpcInteractInput?.Invoke();

    /// <summary>
    /// 옵션 창 열기·닫기 입력
    /// </summary>
    public static event Action OnOptionsInput;
    public static void PublishOptionsInput() => OnOptionsInput?.Invoke();

    /// <summary>
    /// 의뢰 진행 창 표시 전환 요청
    /// </summary>
    public static event Action OnQuestTrackerInput;
    public static void PublishQuestTrackerInput() => OnQuestTrackerInput?.Invoke();

    /// <summary>
    /// 이동 관련 인풋 발생 시 발행
    /// </summary>
    public static event Action<Vector2> OnMoveInput;


    /// <summary>
    /// 점프 입력 발생 시 발행
    /// </summary>
    public static event Action OnJumpInput;




    /// <summary>
    /// 이동 입력 이벤트 실행
    /// </summary>
    /// <param Name="dir"></param>
    public static void PublishMoveInput(Vector2 dir) => OnMoveInput?.Invoke(dir);

    /// <summary>
    /// 점프 입력 이벤트 실행
    /// </summary>
    public static void PublishJumpInput() => OnJumpInput?.Invoke();


    /// <summary>
    /// 기본 공격 입력 이벤트 실행
    /// </summary>
    public static event Action OnBasicAttackInput;
    public static void PublishBasicAttackInput() => OnBasicAttackInput?.Invoke();

    /// <summary>
    /// 스킬 슬롯 입력 발생
    /// </summary>
    public static event Action<int> OnSkillSlotInput;

    /// <summary>
    /// 스킬 슬롯 입력 발행
    /// </summary>
    public static void PublishSkillSlotInput(int slotIndex) => OnSkillSlotInput?.Invoke(slotIndex);


    /// <summary>
    /// 등록된 퀵슬롯 단축키 입력
    /// </summary>
    public static event Action<int> OnQuickSlotInput;
    public static void PublishQuickSlotInput(int slotIndex) => OnQuickSlotInput?.Invoke(slotIndex);

    public static event Action OnInventoryTabInput;
    public static void PublishInventoryInput() => OnInventoryTabInput?.Invoke();

    public static event Action OnInventoryWindowInputEvent;
    public static void PublishInventoryWindowInput()
            => OnInventoryWindowInputEvent?.Invoke();


    public static event Action OnStatWindowInputEvent;
    public static void PublishStatWindowInput()
    => OnStatWindowInputEvent?.Invoke();


    public static event Action OnEnhanceWindowInputEvent;
    public static void PublishEnhanceWindowInput()
    => OnEnhanceWindowInputEvent?.Invoke();

    /// <summary>
    /// 줍기 키 입력
    /// </summary>
    public static event Action OnPickupInput;
    public static void PublishPickupInput() => OnPickupInput?.Invoke();
    #endregion


    #region Equipment Event
    /// <summary>
    /// 장착 요청 이벤트(UI->Player)
    /// </summary>
    public static event Action<InventoryItemData> OnEquipRequested;
    public static void PublishEquipRequested(InventoryItemData item) => OnEquipRequested?.Invoke(item);

   /// <summary>
    /// 해제 요청 이벤트(UI -> Player)
    /// </summary>
    public static event Action<EquipSlot> OnUnequipRequested;
    public static void PublishUnEquipRequested(EquipSlot slot) => OnUnequipRequested?.Invoke(slot);

    /// <summary>
    /// 장비 변경 이벤트(Player -> UI)
    /// </summary>
    public static event Action<EquipSlot, InventoryItemData> OnEquipChanged;
    public static void PublishEquipChanged(EquipSlot slot, InventoryItemData item)
                    => OnEquipChanged?.Invoke(slot, item);
    #endregion

    /// <summary>
    /// 인벤토리 슬롯 교환 요청
    /// </summary>
    public static event Action<InventoryTab, int, int> OnInventorySwapRequested;

    public static void PublishInventorySwapRequested(InventoryTab tab, int fromIndex, int toIndex)
    {
        OnInventorySwapRequested?.Invoke(tab, fromIndex, toIndex);
    }

    /// <summary>
    /// 장비를 지정 인벤토리 슬롯으로 해제하는 요청
    /// </summary>
    public static event Action<EquipSlot, InventoryTab, int> OnUnequipToInventoryRequested;

    public static void PublishUnequipToInventoryRequested(EquipSlot sourceSlot, InventoryTab targetTab, int targetIndex)
    {
        OnUnequipToInventoryRequested?.Invoke(sourceSlot, targetTab, targetIndex);
    }

    /// <summary>
    /// 현재 소비 슬롯의 아이템 사용 요청
    /// </summary>
    public static event Action<int, InventoryItemData> OnConsumableUseRequested;
    public static void PublishConsumableUseRequested(int slotIndex, InventoryItemData item) => OnConsumableUseRequested?.Invoke(slotIndex, item);

    #region Inventory
    public static event Action<int> OnGoldChanged;
    public static void PublishGoldChanged(int currentGold)
            => OnGoldChanged?.Invoke(currentGold);

    #endregion

    #region playerStat

    public static event Action<float, float> OnPlayerHpChanged;
    public static void PublishPlayerHpChanged(float currentHp, float maxHp)
            => OnPlayerHpChanged?.Invoke(currentHp, maxHp);

    public static event Action<float, float> OnPlayerMpChanged;
    public static void PublishPlayerMpChanged(float currentMp, float maxMp)
    => OnPlayerMpChanged?.Invoke(currentMp, maxMp);

    /// <summary>
    /// 데미지 숫자 표시 요청
    /// </summary>
    public static event Action<DamageNumberRequest> OnDamageNumberRequested;

    /// <summary>
    /// 데미지 숫자 표시 요청 발행
    /// </summary>
    public static void PublishDamageNumber(DamageNumberRequest request) => OnDamageNumberRequested?.Invoke(request);

    public static event Action<Dictionary<StatType, float>> OnPlayerStatsChanged;
    public static void PublishPlayerStatsChanged(Dictionary<StatType, float> statsData)
            => OnPlayerStatsChanged?.Invoke(statsData);

    /// <summary>
    /// 경험치 획득 시 발행
    /// </summary>
    public static event Action<float> OnExpEarned;
    public static void PublishExpEarned(float exp)
            => OnExpEarned?.Invoke(exp);
    /// <summary>
    /// exp 획득 시 발행
    /// </summary>
    public static event Action<float, float> OnPlayerExpChanged;
    public static void PublishPlayerExpChanged(float currentExp, float maxExp)
            => OnPlayerExpChanged?.Invoke(currentExp, maxExp);

    /// <summary>
    /// 레벨 업 시 발행
    /// </summary>
    public static event Action<int> OnPlayerLevelChanged;
    public static void PublishPlayerLevelChanged(int level)
            => OnPlayerLevelChanged?.Invoke(level);


    #endregion
}
