using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 장비 장착용 UI 클래스
/// </summary>
public class EquipSlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IItemDragSource, IDropHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [SerializeField] private EquipSlot _slotType;
    public EquipSlot SlotType => _slotType;


    [SerializeField] private Image _itemIcon;
    private readonly ItemDragVisual _dragVisual = new();

    private InventoryItemData _equippedItem;
    private ItemTooltip _tooltip;

    internal void BindTooltip(ItemTooltip tooltip) => _tooltip = tooltip;

    /// <summary>
    /// 클릭시 발생하는 이벤트
    /// </summary>
    public event Action<EquipSlot> OnSlotDoubleClicked;

    public void Initialize()
    {
        UpdateSlotUI(null, null);
    }

    public void UpdateSlotUI(InventoryItemData item, Sprite iconSprite)
    {
        _tooltip?.Hide(this);
        _equippedItem = item;
        _itemIcon.sprite = iconSprite;
        bool hasItem = item != null && !item.IsEmpty;

        if (hasItem)
        {
            _itemIcon.color = iconSprite != null ? Color.white : Color.red;
            return;
        }
        _itemIcon.color = new Color(1f, 1f, 1f, 0f);

    }

    public bool TryGetDrag(out ItemDrag itemDrag)
    {
        if (_equippedItem == null || _equippedItem.IsEmpty || _equippedItem.IsDestroyed)
        {
            itemDrag = null;
            return false;
        }

        itemDrag = ItemDrag.FromEquipment(_equippedItem, _slotType);
        return true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        bool isLeftDoubleClick =
                eventData.button == PointerEventData.InputButton.Left &&
                eventData.clickCount == 2;

        if (!isLeftDoubleClick || _equippedItem == null || _equippedItem.IsEmpty)
        {
            return;
        }

        OnSlotDoubleClicked?.Invoke(_slotType);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _tooltip?.Hide(this);
        if (!TryGetDrag(out _))
        {
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>().rootCanvas;
        _dragVisual.Begin(_itemIcon, canvas);
    }

    public void OnDrag(PointerEventData eventData)
    {
        _dragVisual.Move(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _dragVisual.End();
    }

    public void OnPointerEnter(PointerEventData eventData) => OnPointerMove(eventData);

    public void OnPointerMove(PointerEventData eventData)
    {
        if (!eventData.dragging && _equippedItem != null && !_equippedItem.IsEmpty && !_equippedItem.IsDestroyed)
        {
            _tooltip?.Show(this, _equippedItem, _itemIcon.sprite, eventData.position, compare: false);
        }
    }

    public void OnPointerExit(PointerEventData eventData) => _tooltip?.Hide(this);

    private void OnDisable() => _tooltip?.Hide(this);

    public void OnDrop(PointerEventData eventData)
    {
        if (!ItemDragResolver.TryResolve(eventData, out ItemDrag itemDrag))
        {
            return;
        }

        if (itemDrag.Source != ItemDrag.SourceType.Inventory)
        {
            return;
        }

        EquipSlot itemSlot = itemDrag.Item.ItemData.GetEquipSlot();

        if (itemSlot != _slotType)
        {
            return;
        }

        EventBus.PublishEquipRequested(itemDrag.Item);
    }
}
