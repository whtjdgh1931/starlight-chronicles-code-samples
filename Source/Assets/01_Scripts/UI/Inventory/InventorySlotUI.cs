using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IItemDragSource, IPointerDownHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private Image _iconImage;
    [SerializeField] private TextMeshProUGUI _countText;

    private InventoryItemData _inventoryItemData;
    private ItemTooltip _tooltip;

    internal void BindTooltip(ItemTooltip tooltip) => _tooltip = tooltip;


    private int _slotIndex;
    private InventoryTab _currentTab;
    private bool _isLocked;

    private readonly ItemDragVisual _dragVisual = new();


    private Action<InventoryTab, int, int> _swapRequested;
    private Action<EquipSlot, InventoryTab, int> _unequipToInventoryRequested;

    private bool _isDragCancelled;


    /// <summary>
    /// 인벤토리 슬롯 초기화
    /// </summary>
    public void Initialize(
         Action<InventoryTab, int, int> swapRequested,
         Action<EquipSlot, InventoryTab, int> unequipToInventoryRequested)
    {
        _swapRequested = swapRequested;
        _unequipToInventoryRequested = unequipToInventoryRequested;

        // 필수 표시 참조는 초기화에서 한 번 검증
        if (_backgroundImage == null || _iconImage == null || _countText == null)
        {
            throw new MissingReferenceException($"{name}: 슬롯 배경·아이콘·수량 텍스트 참조 필요");
        }
    }

    public void SetSlotIndex(int index, InventoryTab tab)
    {
        _slotIndex = index;
        _currentTab = tab;
    }

    public void SetLocked(bool isLocked)
    {
        _isLocked = isLocked;
        _backgroundImage.color = _isLocked ? Color.gray : Color.white;


        if (_isLocked)
        {
            _countText.text = Constants.STR_Blank;
        }
    }

    public void UpdateSlot(InventoryItemData itemData, Sprite iconSprite)
    {
        _tooltip?.Hide(this);
        _inventoryItemData = itemData;
        _iconImage.sprite = iconSprite;

        bool hasItem = !_isLocked && itemData != null && !itemData.IsEmpty;
        _iconImage.color = hasItem ? iconSprite != null ? Color.white : Color.gray : Color.clear;
        _countText.text = hasItem && itemData.Count > 1 ? itemData.Count.ToString() : Constants.STR_Blank;
    }

    public bool TryGetDrag(out ItemDrag itemDrag)
    {
        if (_isLocked || _isDragCancelled || _inventoryItemData == null || _inventoryItemData.IsEmpty)
        {
            itemDrag = null;
            return false;
        }

        itemDrag = ItemDrag.FromInventory(_inventoryItemData, _currentTab, _slotIndex);
        return true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left ||
            eventData.clickCount != 2 || _isLocked || _isDragCancelled)
        {
            return;
        }
        // 빈 슬롯의 사용 요청 제외
        if (_inventoryItemData == null || _inventoryItemData.IsEmpty) { return; }
        switch (_inventoryItemData.ItemData.GetItemType())
        {
            case GameItemType.Equipment:
                EventBus.PublishEquipRequested(_inventoryItemData);
                break;
            case GameItemType.Consumable:
                EventBus.PublishConsumableUseRequested(_slotIndex, _inventoryItemData);
                break;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isDragCancelled = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _tooltip?.Hide(this);
        if (!TryGetDrag(out _))
        {
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>().rootCanvas;
        _dragVisual.Begin(_iconImage, canvas);
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
        if (!_isLocked && !eventData.dragging && _inventoryItemData != null &&
            !_inventoryItemData.IsEmpty && !_inventoryItemData.IsDestroyed)
        {
            _tooltip?.Show(this, _inventoryItemData, _iconImage.sprite, eventData.position, compare: true);
        }
    }

    public void OnPointerExit(PointerEventData eventData) => _tooltip?.Hide(this);

    private void OnDisable() => _tooltip?.Hide(this);

    public void OnDrop(PointerEventData eventData)
    {
        if (_isLocked || !ItemDragResolver.TryResolve(eventData, out ItemDrag itemDrag))
        {
            return;
        }

        switch (itemDrag.Source)
        {
            case ItemDrag.SourceType.Inventory:
                HandleInventoryDrop(itemDrag);
                break;

            case ItemDrag.SourceType.Equipment:
                HandleEquipmentDrop(itemDrag);
                break;
        }
    }

    private void HandleInventoryDrop(ItemDrag itemDrag)
    {
        if (itemDrag.InventoryTab != _currentTab || itemDrag.InventoryIndex == _slotIndex)
        {
            return;
        }

        _swapRequested?.Invoke(_currentTab, itemDrag.InventoryIndex, _slotIndex);
    }

    private void HandleEquipmentDrop(ItemDrag itemDrag)
    {
        bool hasItem = _inventoryItemData != null && !_inventoryItemData.IsEmpty;

        if (_currentTab != InventoryTab.Equipment || hasItem)
        {
            return;
        }

        _unequipToInventoryRequested?.Invoke(itemDrag.EquipSlot, _currentTab, _slotIndex);
    }

    /// <summary>
    /// 연결 해제 시 드래그 종료와 슬롯 데이터 정리
    /// </summary>
    public void ClearBinding()
    {
        _dragVisual.End();
        _isDragCancelled = true;
        SetLocked(true);
        UpdateSlot(null, null);
    }
}
