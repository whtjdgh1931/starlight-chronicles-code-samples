using Assets.HeroEditor4D.Common.Scripts.Collections;
using System.Collections.Generic;
using UnityEngine;


public class EquipUI : MonoBehaviour
{
    [SerializeField] private List<EquipSlotUI> _equipSlots;
    [SerializeField] private IconCollection _iconCollection;
    [SerializeField] private CanvasGroup _interactionGroup;
    private ItemTooltip _tooltip;

    internal void BindTooltip(ItemTooltip tooltip) => _tooltip = tooltip;

    public void Initialize()
    {
        if (_interactionGroup == null)
        {
            throw new MissingReferenceException($"{name}: 장비 조작 영역 CanvasGroup 참조 필요");
        }
        gameObject.SetActive(true);
        SetInteractionLocked(true);
        if (_iconCollection == null)
        {
            _iconCollection = Resources.Load<IconCollection>(Constants.STR_IconCollection);
        }

        foreach (var slot in _equipSlots)
        {
            slot.Initialize();
            slot.BindTooltip(_tooltip);
            slot.OnSlotDoubleClicked -= HandleSlotDoubleClicked;
            slot.OnSlotDoubleClicked += HandleSlotDoubleClicked;
        }

        EventBus.OnEquipChanged -= UpdateUISlot;
        EventBus.OnEquipChanged += UpdateUISlot;

    }

    /// <summary>
    /// 장비 표시를 유지한 장착·해제·드래그 조작 잠금
    /// </summary>
    public void SetInteractionLocked(bool locked)
    {
        _interactionGroup.interactable = !locked;
        _interactionGroup.blocksRaycasts = !locked;
        if (locked) { _tooltip?.Hide(); }
    }

    private void UpdateUISlot(EquipSlot targetSlot, InventoryItemData inventoryItemData)
    {
        _tooltip?.Hide();
        ItemData itemData = inventoryItemData?.ItemData;

        foreach (EquipSlotUI slot in _equipSlots)
        {
            if (slot.SlotType != targetSlot)
            {
                continue;
            }

            Sprite icon = null;
            if (itemData != null && !string.IsNullOrEmpty(itemData.IconId))
            {
                icon = _iconCollection.GetIcon(itemData.IconId);
            }

            slot.UpdateSlotUI(inventoryItemData, icon);
            break;
        }
    }

    private void HandleSlotDoubleClicked(EquipSlot slotType)
    {
        EventBus.PublishUnEquipRequested(slotType);
    }

    private void OnDestroy()
    {
        EventBus.OnEquipChanged -= UpdateUISlot;
    }

}
