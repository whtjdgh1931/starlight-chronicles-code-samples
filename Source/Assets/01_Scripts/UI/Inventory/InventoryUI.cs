using Assets.HeroEditor4D.Common.Scripts.Collections;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private UIWindowMotion _windowMotion;
    [SerializeField] private CanvasGroup _slotsCanvasGroup;
    [SerializeField] private ToggleGroup _tabGroup;
    [SerializeField] private List<Toggle> _tabToggles;
    [SerializeField] private TextMeshProUGUI _goldText;
    [SerializeField] private Button _rewardButton;
    [SerializeField] private TextMeshProUGUI _rewardText;

    [Header("슬롯 자동 생성")]
    [SerializeField] private InventorySlotUI _slotPrefab;
    [SerializeField] private Transform _slotContentParent;

    private PlayerInventory _inventory;
    private bool _isInteractionLocked;
    private ItemTooltip _tooltip;

    internal void BindTooltip(ItemTooltip tooltip) => _tooltip = tooltip;

    /// <summary>
    /// 교환·장비 변경·보관 보상 수령 잠금과 소비 탭 입력 유지
    /// </summary>
    public void SetInteractionLocked(bool locked)
    {
        _isInteractionLocked = locked;
        RefreshSlotInteraction();
        if (_inventory != null) { RefreshBossRewardButton(); }
        else { _rewardButton.gameObject.SetActive(false); }
    }

    /// <summary>
    /// 소비 탭의 사용·퀵슬롯 등록 입력 유지
    /// 교환·장비 변경은 기존 요청 잠금으로 제한
    /// </summary>
    private void RefreshSlotInteraction()
    {
        bool interactive = !_isInteractionLocked || _currentTab == InventoryTab.Consumable;
        _slotsCanvasGroup.interactable = interactive;
        _slotsCanvasGroup.blocksRaycasts = interactive;
    }

    private List<InventorySlotUI> _slots = new List<InventorySlotUI>();

    [Header("Resources")]
    [SerializeField] private IconCollection _iconCollection;

    private InventoryTab _currentTab = InventoryTab.Equipment;
    public InventoryTab CurrentTab => _currentTab;


    /// <summary>
    /// 창 입력을 받을 수 있는 활성 상태 준비와 초기 숨김
    /// 데이터 연결 전 슬롯 조작과 보관 보상 수령 차단
    /// </summary>
    public void InitializeWindow()
    {
        if (_canvasGroup == null || _windowMotion == null || _slotsCanvasGroup == null ||
            _slotPrefab == null || _slotContentParent == null || _iconCollection == null)
        {
            throw new MissingReferenceException($"{name}: 창·슬롯 영역·슬롯 프리팹·아이콘 컬렉션 참조 필요");
        }
        _windowMotion.Initialize();
        SetInteractionLocked(true);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 인벤토리 UI 초기화
    /// </summary>
    public void Initialize(PlayerInventory inventory)
    {
        if (inventory == null)
        {
            throw new ArgumentNullException(nameof(inventory));
        }

        UnbindInventory();

        _inventory = inventory;
        _inventory.InventoryChanged += HandleInventoryChanged;
        _rewardButton.onClick.AddListener(HandleBossRewardsClicked);
        RefreshBossRewardButton();


        if (_slots.Count == 0)
        {
            for (int i = 0; i < _inventory.MaxSlot; i++)
            {
                InventorySlotUI newSlot = Instantiate(_slotPrefab, _slotContentParent);
                newSlot.Initialize(HandleSwapRequested, HandleUnequipToInventoryRequested);
                newSlot.BindTooltip(_tooltip);
                _slots.Add(newSlot);
            }
        }

        for (int i = 0; i < _tabToggles.Count; i++)
        {
            int index = i;

            InventoryTab mappedTab = (InventoryTab)(index + 1);
            _tabToggles[i].group = _tabGroup;
            _tabToggles[i].interactable = true;
            _tabToggles[i].SetIsOnWithoutNotify(mappedTab == _currentTab);
            _tabToggles[i].onValueChanged.RemoveAllListeners();
            _tabToggles[i].onValueChanged.AddListener((isOn) =>
            {
                if (isOn)
                {
                    SwitchTab(mappedTab);
                }
            });
        }

        if (isActiveAndEnabled)
        {
            ConnectInputEvents();
        }

        _inventory.RequestTabData(_currentTab);
        SetWindowActive(false);
    }

    private void RefreshBossRewardButton()
    {
        int count = _inventory.PendingBossRewards.Count;
        _rewardButton.gameObject.SetActive(count > 0);
        _rewardButton.interactable = !_isInteractionLocked && count > 0;
        _rewardText.text = $"이전 보상 수령 ({count})";
    }

    private void HandleBossRewardsClicked()
    {
        if (_isInteractionLocked) { return; }
        Player player = GameManager.Instance.PlaySessionController.ActivePlayer;
        bool success = player.TryClaimBossRewards(out int claimed, out string error);
        RefreshBossRewardButton();
        if (!success)
        {
            _rewardText.text = "보상 수령 불가";
            Debug.LogWarning(error, this);
        }
        else if (claimed == 0 && _inventory.PendingBossRewards.Count > 0)
        {
            _rewardText.text = "인벤토리 공간 부족";
        }
    }

    /// <summary>
    /// 활성 UI의 창 입력과 탭 입력 및 골드 알림 연결
    /// </summary>
    private void ConnectInputEvents()
    {
        DisconnectInputEvents();
        EventBus.OnInventoryWindowInputEvent += ToggleWindow;
        EventBus.OnInventoryTabInput += CycleTab;
        EventBus.OnGoldChanged += UpdateGoldUI;
    }

    private void OnEnable()
    {
        if (_inventory != null)
        {
            ConnectInputEvents();
        }
    }

    /// <summary>
    /// 인벤토리 연결 해제와 입력 및 슬롯 표시 정리
    /// </summary>
    public void UnbindInventory()
    {
        DisconnectInputEvents();
        DisconnectInventory();
        _rewardButton.onClick.RemoveListener(HandleBossRewardsClicked);

        foreach (Toggle tabToggle in _tabToggles)
        {
            tabToggle.onValueChanged.RemoveAllListeners();
            tabToggle.interactable = false;
        }

        foreach (InventorySlotUI slot in _slots)
        {
            slot.ClearBinding();
        }

        _goldText.text = Constants.STR_Blank;
        SetInteractionLocked(true);
        SetWindowActive(false);
    }

    public void ToggleWindow()
    {

        if (_inventory == null)
        {
            return;
        }
        bool nextState = !_windowMotion.IsVisible;
        SetWindowActive(nextState, false);
        if (nextState)
        {
            RefreshBossRewardButton();
            _inventory.RequestTabData(_currentTab);
        }
    }

    /// <summary>
    /// 탭 변경
    /// </summary>
    /// <param Name="tab"></param>
    public void SwitchTab(InventoryTab tab)
    {
        if (tab == InventoryTab.None)
        {
            return;
        }
        _currentTab = tab;
        RefreshSlotInteraction();

        int index = (int)_currentTab - 1;

        //토글 비주얼 동기화
        if (!_tabToggles[index].isOn)
        {
            _tabToggles[index].isOn = true;
        }
        _inventory.RequestTabData(_currentTab);
    }

    /// <summary>
    /// 인벤토리 tab 순환
    /// </summary>
    public void CycleTab()
    {
        int nextTabIdx = ((int)_currentTab) % _tabToggles.Count;
        _tabToggles[nextTabIdx].isOn = true;
    }

    /// <summary>
    /// 아이템 목록 렌더
    /// </summary>
    /// <param Name="items"></param>
    public void Render(InventoryItemData[] items, int currentSlotCount)
    {
     
        for (int i = 0; i < _slots.Count; i++)
        {
            _slots[i].SetSlotIndex(i, _currentTab);

            if (i < currentSlotCount)
            {
                _slots[i].SetLocked(false);
                if (i < items.Length && items[i] != null && !items[i].IsEmpty)
                {
                    var item = items[i].ItemData;
                    Sprite icon = null;
                    if (!string.IsNullOrEmpty(item.IconId))
                    {
                        icon = _iconCollection.GetIcon(item.IconId);
                    }
                    _slots[i].UpdateSlot(items[i], icon);
                }
                else
                {
                    _slots[i].UpdateSlot(null, null);
                }
            }
            else
            {
                _slots[i].UpdateSlot(null, null);
                _slots[i].SetLocked(true);
            }
        }
    }

    public void UpdateGoldUI(int currentGold)
    {
        _goldText.text = $"Gold : {currentGold}G";
    }

    private void SetWindowActive(bool active, bool immediate = true)
    {
        if (!active) { _tooltip?.Hide(); }
        _windowMotion.SetVisible(active, immediate);
    }

    private void HandleUnequipToInventoryRequested(EquipSlot sourceSlot, InventoryTab targetTab, int targetIndex)
    {
        if (!_isInteractionLocked) { EventBus.PublishUnequipToInventoryRequested(sourceSlot, targetTab, targetIndex); }
    }

    private void HandleSwapRequested(InventoryTab tab, int fromIndex, int toIndex)
    {
        if (!_isInteractionLocked) { EventBus.PublishInventorySwapRequested(tab, fromIndex, toIndex); }
    }

    private void HandleInventoryChanged(InventoryTab tab, InventoryItemData[] items, int slotCount)
    {
        if (tab == _currentTab)
        {
            Render(items, slotCount);
        }
    }

    /// <summary>
    /// 인벤토리 변경 구독과 참조 해제
    /// </summary>
    private void DisconnectInventory()
    {
        if (_inventory != null)
        {
            _inventory.InventoryChanged -= HandleInventoryChanged;
            _inventory = null;
        }
    }

    /// <summary>
    /// 창 입력과 탭 입력 및 골드 알림 해제
    /// </summary>
    private void DisconnectInputEvents()
    {
        EventBus.OnInventoryWindowInputEvent -= ToggleWindow;
        EventBus.OnInventoryTabInput -= CycleTab;
        EventBus.OnGoldChanged -= UpdateGoldUI;
    }

    private void OnDisable()
    {
        DisconnectInputEvents();
    }

    private void OnDestroy()
    {
        DisconnectInputEvents();
        DisconnectInventory();
    }
}
