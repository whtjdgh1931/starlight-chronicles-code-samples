using Assets.HeroEditor4D.Common.Scripts.Collections;
using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 자유 등록 퀵슬롯의 입력·데이터와 개별 표시 연결
/// 실제 사용은 기존 스킬·소비 아이템 요청 경로에 위임
/// </summary>
public sealed class QuickSlotBar : MonoBehaviour
{
    private struct Binding
    {
        public QuickSlotKind Kind;
        public int SkillIndex;
        public ItemData Item;
    }

    [SerializeField] private QuickSlotView[] _slots;
    [SerializeField] private SkillQuickSlotSource[] _skillSources;
    [SerializeField] private GameObject _skillList;
    [SerializeField] private Button _skillListButton;
    [SerializeField] private Sprite[] _skillIcons;
    [SerializeField] private string[] _keys;
    [SerializeField] private IconCollection _icons;

    private Player _player;
    private Binding[] _bindings;

    private bool CanInteractWithSlots =>
        isActiveAndEnabled && _player != null && !_player.IsDead && !_player.IsNetworkJoinLocked;

    /// <summary>
    /// Inspector 슬롯 요청을 한 번 연결하고 초기 숨김
    /// </summary>
    public void Initialize()
    {
        if (_slots.Length != PlayerQuickSlots.SlotCount)
        {
            throw new InvalidOperationException($"{name}: 퀵슬롯 UI는 {PlayerQuickSlots.SlotCount}칸 구성 필요");
        }
        _bindings = new Binding[_slots.Length];
        for (int i = 0; i < _slots.Length; i++)
        {
            int index = i;
            _slots[i].Initialize();
            _slots[i].Requested += () => RequestSlot(index);
            _slots[i].ItemDropped += itemId => RegisterItem(index, itemId);
            _slots[i].SkillDropped += skillIndex => RegisterSkill(index, skillIndex);
            _slots[i].Cleared += () => ClearSlot(index);
            _slots[i].DragFinished += target => HandleSlotDragFinished(index, target);
        }
        _skillListButton.onClick.AddListener(ToggleSkillList);
        _skillList.SetActive(false);
        enabled = false;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 캐릭터 교체 전 구독 해제와 새 소유자 데이터 연결
    /// </summary>
    public void Bind(Player player)
    {
        Unbind();
        _player = player;
        _player.PlayerInventory.InventoryChanged += HandleInventoryChanged;
        _player.QuickSlots.Changed += HandleQuickSlotChanged;
        EventBus.OnQuickSlotInput += RequestSlot;
        for (int i = 0; i < _skillSources.Length; i++)
        {
            bool hasSkill = _player.TryGetSlotSkill(i, out SkillData skill);
            _skillSources[i].gameObject.SetActive(hasSkill);
            if (hasSkill) { _skillSources[i].Bind(i, skill, _skillIcons[i]); }
        }
        for (int i = 0; i < _slots.Length; i++) { RestoreBinding(i); }
        RefreshSkillRegistrations();
        enabled = true;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 소유권 해제 시 구독·스킬 목록·퀵슬롯 표시 종료
    /// </summary>
    public void Unbind()
    {
        EventBus.OnQuickSlotInput -= RequestSlot;
        if (_player != null)
        {
            _player.PlayerInventory.InventoryChanged -= HandleInventoryChanged;
            _player.QuickSlots.Changed -= HandleQuickSlotChanged;
        }
        _player = null;
        foreach (QuickSlotView slot in _slots) { slot.ResetCooldownFeedback(); }
        _skillList.SetActive(false);
        enabled = false;
        gameObject.SetActive(false);
    }

    private void ToggleSkillList()
    {
        if (enabled) { _skillList.SetActive(!_skillList.activeSelf); }
    }

    private void RegisterSkill(int slotIndex, int skillIndex)
    {
        if (!CanInteractWithSlots || !_player.TryGetSlotSkill(skillIndex, out SkillData skill)) { return; }
        _player.QuickSlots.SetSlot(slotIndex, QuickSlotKind.Skill, skill.ID);
    }

    private void RegisterItem(int slotIndex, int itemId)
    {
        if (!CanInteractWithSlots || !TryFindItem(itemId, out _, out _)) { return; }
        _player.QuickSlots.SetSlot(slotIndex, QuickSlotKind.Consumable, itemId);
    }

    /// <summary>
    /// 슬롯 간 이동·교환 또는 슬롯 밖 등록 해제
    /// </summary>
    private void HandleSlotDragFinished(int sourceIndex, QuickSlotView target)
    {
        if (!CanInteractWithSlots) { return; }
        if (target == null)
        {
            ClearSlot(sourceIndex);
            return;
        }
        int targetIndex = Array.IndexOf(_slots, target);
        if (targetIndex < 0) { return; }
        _player.QuickSlots.SwapSlots(sourceIndex, targetIndex);
    }

    private void ClearSlot(int slotIndex)
    {
        if (!CanInteractWithSlots) { return; }
        _player.QuickSlots.SetSlot(slotIndex, QuickSlotKind.Empty, 0);
    }

    /// <summary>
    /// 캐릭터 등록 ID를 현재 실행 데이터로 변환한 UI 캐시 갱신
    /// </summary>
    private void RestoreBinding(int slotIndex)
    {
        QuickSlotSaveData saved = _player.QuickSlots.GetSlot(slotIndex);
        Binding binding = default;
        if (saved.Kind == QuickSlotKind.Skill && _player.TryFindSkillSlot(saved.ContentId, out int skillIndex))
        {
            binding = new Binding { Kind = QuickSlotKind.Skill, SkillIndex = skillIndex };
        }
        else if (saved.Kind == QuickSlotKind.Consumable && _player.TryGetConsumableData(saved.ContentId, out ItemData item))
        {
            binding = new Binding { Kind = QuickSlotKind.Consumable, Item = item };
        }
        _slots[slotIndex].ResetCooldownFeedback();
        _bindings[slotIndex] = binding;
        RefreshSlot(slotIndex);
        RefreshCooldown(slotIndex);
    }

    /// <summary>
    /// 변경된 퀵슬롯과 스킬 목록 표시 갱신
    /// </summary>
    private void HandleQuickSlotChanged(int slotIndex)
    {
        RestoreBinding(slotIndex);
        RefreshSkillRegistrations();
    }

    /// <summary>
    /// 현재 퀵슬롯 등록에 따른 스킬 목록 밝기 갱신
    /// </summary>
    private void RefreshSkillRegistrations()
    {
        for (int i = 0; i < _skillSources.Length; i++)
        {
            if (!_player.TryGetSlotSkill(i, out SkillData skill)) { continue; }
            bool registered = false;
            for (int slotIndex = 0; slotIndex < PlayerQuickSlots.SlotCount; slotIndex++)
            {
                QuickSlotSaveData slot = _player.QuickSlots.GetSlot(slotIndex);
                if (slot.Kind == QuickSlotKind.Skill && slot.ContentId == skill.ID)
                {
                    registered = true;
                    break;
                }
            }
            _skillSources[i].SetRegistered(registered);
        }
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < _slots.Length; i++) { RefreshSlot(i); }
        RefreshCooldowns();
    }

    /// <summary>
    /// 변경된 한 칸의 아이콘·수량만 반영
    /// 다른 칸의 쿨타임 완료 연출 유지
    /// </summary>
    private void RefreshSlot(int index)
    {
        Binding binding = _bindings[index];
        switch (binding.Kind)
        {
            case QuickSlotKind.Skill:
                _player.TryGetSlotSkill(binding.SkillIndex, out SkillData skill);
                _slots[index].Render(_skillIcons[binding.SkillIndex], _keys[index], skill.SkillName, "");
                break;
            case QuickSlotKind.Consumable:
                _slots[index].Render(_icons.GetIcon(binding.Item.IconId), _keys[index], binding.Item.Name, _player.PlayerInventory.CountItems(binding.Item).ToString());
                break;
            default:
                _slots[index].Render(null, _keys[index], "등록 가능", "");
                _slots[index].ResetCooldownFeedback();
                break;
        }
    }

    /// <summary>
    /// 인벤토리 슬롯 위치 대신 아이템 ID로 현재 묶음 검색
    /// 정렬·교환 후에도 동일 종류 사용
    /// </summary>
    private bool TryFindItem(int itemId, out int index, out InventoryItemData item)
    {
        InventoryItemData[] items = _player.PlayerInventory.GetListByTab(InventoryTab.Consumable);
        int count = _player.PlayerInventory.GetCurrentSlotCount(InventoryTab.Consumable);
        for (int i = 0; i < count; i++)
        {
            if (items[i] != null && !items[i].IsEmpty && !items[i].IsDestroyed && items[i].ItemData.ID == itemId)
            {
                index = i;
                item = items[i];
                return true;
            }
        }
        index = -1;
        item = null;
        return false;
    }

    private void RequestSlot(int slotIndex)
    {
        if (!CanInteractWithSlots || slotIndex < 0 || slotIndex >= _bindings.Length) { return; }
        Binding binding = _bindings[slotIndex];
        if (binding.Kind == QuickSlotKind.Skill)
        {
            EventBus.PublishSkillSlotInput(binding.SkillIndex);
        }
        else if (binding.Kind == QuickSlotKind.Consumable && _player.CanUseItems && TryFindItem(binding.Item.ID, out int index, out InventoryItemData item))
        {
            EventBus.PublishConsumableUseRequested(index, item);
        }
    }

    private void HandleInventoryChanged(InventoryTab tab, InventoryItemData[] items, int slotCount)
    {
        if (tab == InventoryTab.Consumable) { RefreshSlots(); }
    }

    private void Update()
    {
        RefreshCooldowns();
    }

    private void RefreshCooldowns()
    {
        // 수량·아이콘은 이벤트로 갱신, 프레임 갱신은 쿨타임만 반영
        for (int i = 0; i < _slots.Length; i++) { RefreshCooldown(i); }
    }

    private void RefreshCooldown(int index)
    {
        Binding binding = _bindings[index];
        if (binding.Kind == QuickSlotKind.Skill && _player.TryGetSlotSkill(binding.SkillIndex, out SkillData skill))
        {
            _slots[index].SetCooldown(_player.GetSkillCooldownRemaining(binding.SkillIndex), skill.Cooldown);
        }
        else if (binding.Kind == QuickSlotKind.Consumable)
        {
            _slots[index].SetCooldown(_player.GetItemCooldownRemaining(binding.Item.ID), binding.Item.Cooldown);
        }
    }

    private void OnDestroy()
    {
        Unbind();
    }
}
