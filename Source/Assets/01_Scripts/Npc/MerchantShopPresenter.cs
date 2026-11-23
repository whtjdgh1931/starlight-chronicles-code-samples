using System;
using System.Collections.Generic;
using Assets.HeroEditor4D.Common.Scripts.Collections;

/// <summary>
/// 상점의 판매 목록·인벤토리 목록과 거래 서비스 연결
/// NPC 접근 판정·골드 증감·아이템 제거·UI 생성 제외
/// </summary>
public sealed class MerchantShopPresenter
{
    private sealed class Listing
    {
        public ItemData Item;
        public InventoryItemData Instance;
        public InventoryTab Tab;
        public int Slot;
    }

    private readonly Player _player;
    private readonly MerchantView _merchant;
    private readonly MerchantShopView _view;
    private readonly ShopTradeService _trade;
    private readonly IReadOnlyList<ItemData> _products;
    private readonly IconCollection _icons;
    private readonly List<Listing> _listings = new();
    private MerchantShopView.Mode _mode;
    private MerchantShopView.Category _category;
    private Listing _selection;
    private int _page;

    public MerchantShopPresenter(Player player, MerchantView merchant, ShopTradeService trade,
        IReadOnlyList<ItemData> products, IconCollection icons)
    {
        _player = player;
        _merchant = merchant;
        _view = merchant.ShopView;
        _trade = trade;
        _products = products;
        _icons = icons;
    }

    public void Initialize()
    {
        _merchant.ShopRequested += Open;
        _view.ModeRequested += HandleMode;
        _view.CategoryRequested += HandleCategory;
        _view.RowSelected += HandleSelection;
        _view.PageRequested += HandlePage;
        _view.TradeRequested += HandleTrade;
        _view.AllRequested += HandleAll;
        _view.MenuRequested += _merchant.RequestMenu;
        _player.PlayerInventory.InventoryChanged += HandleInventoryChanged;
        _player.GoldWallet.GoldChanged += HandleGoldChanged;
    }

    public void Shutdown()
    {
        _merchant.ShopRequested -= Open;
        _view.ModeRequested -= HandleMode;
        _view.CategoryRequested -= HandleCategory;
        _view.RowSelected -= HandleSelection;
        _view.PageRequested -= HandlePage;
        _view.TradeRequested -= HandleTrade;
        _view.AllRequested -= HandleAll;
        _view.MenuRequested -= _merchant.RequestMenu;
        _player.PlayerInventory.InventoryChanged -= HandleInventoryChanged;
        _player.GoldWallet.GoldChanged -= HandleGoldChanged;
    }

    private bool CanTrade => _merchant.IsShopPageOpen && _player.CanCaptureCharacterState &&
        !_player.IsDead && !_player.IsNetworkControlled && !_player.IsNetworkJoinLocked;
    private bool CanHandleInput => CanTrade && _merchant.CanInteractWithShop;

    private void Open()
    {
        if (!_merchant.IsOpen) { return; }
        _mode = MerchantShopView.Mode.Buy;
        _category = MerchantShopView.Category.Consumable;
        _page = 0;
        _selection = null;
        _merchant.ShowShop();
        RefreshListings(true);
    }

    private void HandleMode(MerchantShopView.Mode mode)
    {
        if (!CanHandleInput || mode != MerchantShopView.Mode.Buy && mode != MerchantShopView.Mode.Sell) { return; }
        _mode = mode;
        _category = MerchantShopView.Category.Consumable;
        _page = 0;
        _selection = null;
        _merchant.ShowMessage(string.Empty);
        RefreshListings(true);
    }

    private void HandleCategory(MerchantShopView.Category category)
    {
        if (!CanHandleInput || !MerchantShopView.AllowsCategory(_mode, category)) { return; }
        _category = category;
        _page = 0;
        _selection = null;
        _merchant.ShowMessage(string.Empty);
        RefreshListings(true);
    }

    private void RefreshListings(bool selectFirst = false)
    {
        Listing previous = _selection;
        _listings.Clear();
        if (_mode == MerchantShopView.Mode.Buy)
        {
            foreach (ItemData item in _products)
            {
                if (MatchesCategory(item)) { _listings.Add(new Listing { Item = item }); }
            }
        }
        else
        {
            InventoryTab tab = _category == MerchantShopView.Category.Equipment ? InventoryTab.Equipment
                : _category == MerchantShopView.Category.Consumable ? InventoryTab.Consumable : InventoryTab.Etc;
            InventoryItemData[] items = _player.PlayerInventory.GetListByTab(tab);
            int count = _player.PlayerInventory.GetCurrentSlotCount(tab);
            for (int i = 0; i < count; i++)
            {
                InventoryItemData item = items[i];
                if (item == null || item.IsEmpty) { continue; }
                _listings.Add(new Listing { Item = item.ItemData, Instance = item, Tab = tab, Slot = i });
            }
        }
        _selection = previous == null ? null : _listings.Find(row => _mode == MerchantShopView.Mode.Buy
            ? row.Item.ID == previous.Item.ID : ReferenceEquals(row.Instance, previous.Instance));
        if (selectFirst && _listings.Count > 0) { _selection = _listings[0]; _view.ResetQuantity(); }
        _page = Math.Min(_page, PageCount - 1);
        _view.RenderTabs(_mode, _category);
        RenderPage();
        RenderDetail();
    }

    private bool MatchesCategory(ItemData item)
    {
        if (_category == MerchantShopView.Category.Consumable) { return item.GetItemType() == GameItemType.Consumable; }
        if (item.GetItemType() != GameItemType.Equipment) { return false; }
        EquipSlot slot = item.GetEquipSlot();
        return _category == MerchantShopView.Category.Weapon && slot == EquipSlot.Weapon ||
            _category == MerchantShopView.Category.Helmet && slot == EquipSlot.Helmet ||
            _category == MerchantShopView.Category.Armor && slot == EquipSlot.Armor;
    }

    private int PageCount => Math.Max(1, (_listings.Count + _view.PageSize - 1) / _view.PageSize);
    private int UnitPrice(Listing row) => _mode == MerchantShopView.Mode.Buy ? row.Item.Price : _trade.GetSellPrice(row.Item);

    private void RenderPage()
    {
        for (int i = 0; i < _view.PageSize; i++)
        {
            int index = _page * _view.PageSize + i;
            if (index >= _listings.Count) { _view.HideRow(i); continue; }
            Listing row = _listings[index];
            string name = row.Item.Name;
            if (_mode == MerchantShopView.Mode.Sell)
            {
                if (row.Instance.EnhanceLevel > 0) { name += $" +{row.Instance.EnhanceLevel}"; }
                name += $" / {row.Instance.Count:N0}개";
            }
            _view.SetRow(i, _icons.GetIcon(row.Item.IconId), name, $"{UnitPrice(row):N0} G", ReferenceEquals(row, _selection));
        }
        _view.SetPage(_page, PageCount, _listings.Count == 0);
    }

    private void RenderDetail()
    {
        _view.SetGold(_player.GoldWallet.CurrentGold);
        if (_selection == null) { _view.ClearDetail(); return; }
        int price = UnitPrice(_selection);
        int maximum = _mode == MerchantShopView.Mode.Sell ? _selection.Instance.Count
            : price <= 0 ? 0 : _player.GoldWallet.CurrentGold / price;
        if (_selection.Item.GetItemType() == GameItemType.Equipment) { maximum = Math.Min(1, maximum); }
        string detail = $"{_selection.Item.Description}\n\n단가 {price:N0} G";
        if (_mode == MerchantShopView.Mode.Buy && _selection.Item.ReqLevel > 0)
        {
            detail += $"\n착용 레벨 {_selection.Item.ReqLevel}";
        }
        if (_mode == MerchantShopView.Mode.Sell)
        {
            detail += $"\n보유 {_selection.Instance.Count:N0}개";
            if (price == 0) { detail += "\n판매할 수 없는 아이템"; }
        }
        bool canTrade = CanTrade && price > 0 && maximum > 0 &&
            (_selection.Instance == null || !_selection.Instance.IsDestroyed);
        string displayName = _selection.Item.Name;
        if (_mode == MerchantShopView.Mode.Sell && _selection.Instance.EnhanceLevel > 0)
        {
            displayName += $" +{_selection.Instance.EnhanceLevel}";
        }
        _view.SetDetail(_icons.GetIcon(_selection.Item.IconId), displayName, detail, maximum, canTrade);
    }

    private void HandleSelection(int row)
    {
        int index = _page * _view.PageSize + row;
        if (!CanHandleInput || row < 0 || row >= _view.PageSize || index >= _listings.Count) { return; }
        _selection = _listings[index];
        _view.ResetQuantity();
        _merchant.ShowMessage(string.Empty);
        RenderPage();
        RenderDetail();
    }

    private void HandlePage(int direction)
    {
        if (!CanHandleInput) { return; }
        _page = Math.Max(0, Math.Min(PageCount - 1, _page + Math.Sign(direction)));
        _selection = null;
        RenderPage();
        RenderDetail();
    }

    private void HandleTrade(int count)
    {
        if (!CanHandleInput || _selection == null) { return; }
        // 인벤토리 변경 이벤트로 화면을 재구성하기 전 요청 대상 보관
        Listing selected = _selection;
        string message;
        bool succeeded = _mode == MerchantShopView.Mode.Buy
            ? _trade.TryBuy(selected.Item, count, out message)
            : _trade.TrySell(selected.Tab, selected.Slot, selected.Instance, count, out message);
        RefreshListings();
        _merchant.ShowMessage(message);
        if (succeeded && _selection != null)
        {
            int row = _listings.IndexOf(_selection) - _page * _view.PageSize;
            _view.PulseTradeRow(row);
        }
    }

    private void HandleAll()
    {
        if (_mode == MerchantShopView.Mode.Sell && _selection != null) { HandleTrade(_selection.Instance.Count); }
    }

    private void HandleInventoryChanged(InventoryTab tab, InventoryItemData[] items, int count)
    {
        if (_merchant.IsShopPageOpen) { RefreshListings(); }
    }

    private void HandleGoldChanged(int gold)
    {
        if (_merchant.IsShopPageOpen) { RenderDetail(); }
    }
}
