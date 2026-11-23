using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미리 배치된 상점의 탭·상품 행·수량 입력과 거래 요청 표시
/// 거래 계산과 인벤토리 변경 제외
/// </summary>
public sealed class MerchantShopView : MonoBehaviour
{
    public enum Mode { Buy, Sell }
    public enum Category { Consumable, Weapon, Helmet, Armor, Equipment, Etc }

    [Serializable]
    private sealed class Tab
    {
        public Category Category;
        public Button Button;
    }

    [Serializable]
    private sealed class Row
    {
        public Image Background;
        public Image Icon;
        public TMP_Text Name;
        public TMP_Text Price;
    }

    [Serializable]
    private sealed class ActionButton
    {
        public Button Button;
        public TMP_Text Label;
    }

    [SerializeField] private Button _buyTab;
    [SerializeField] private Button _sellTab;
    [SerializeField] private Tab[] _tabs;
    [SerializeField] private Row[] _rows;
    [SerializeField] private TMP_Text _goldText;
    [SerializeField] private TMP_Text _pageText;
    [SerializeField] private TMP_Text _emptyText;
    [SerializeField] private Button _previousPage;
    [SerializeField] private Button _nextPage;
    [SerializeField] private Image _detailIcon;
    [SerializeField] private TMP_Text _detailName;
    [SerializeField] private TMP_Text _detailText;
    [SerializeField] private TMP_InputField _quantityInput;
    [SerializeField] private ActionButton _one;
    [SerializeField] private ActionButton _quantity;
    [SerializeField] private ActionButton _all;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _selectedColor = new Color(1f, 0.82f, 0.45f);
    private Tween _tradePulse;
    private RectTransform _pulsedIcon;
    private Vector3 _pulseBaseScale;

    public int PageSize => _rows.Length;
    public event Action<Mode> ModeRequested;
    public event Action<Category> CategoryRequested;
    public event Action<int> RowSelected;
    public event Action<int> PageRequested;
    public event Action<int> TradeRequested;
    public event Action AllRequested;
    public event Action MenuRequested;

    /// <summary>
    /// 현재 방식에 표시할 종류 탭 구분
    /// </summary>
    public static bool AllowsCategory(Mode mode, Category category)
    {
        return category == Category.Consumable || (mode == Mode.Buy
            ? category == Category.Weapon || category == Category.Helmet || category == Category.Armor
            : category == Category.Equipment || category == Category.Etc);
    }

    /// <summary>
    /// 구매·판매 및 종류별 탭의 선택 상태 반영
    /// </summary>
    public void RenderTabs(Mode mode, Category category)
    {
        _buyTab.image.color = mode == Mode.Buy ? _selectedColor : _normalColor;
        _sellTab.image.color = mode == Mode.Sell ? _selectedColor : _normalColor;
        foreach (Tab tab in _tabs)
        {
            tab.Button.gameObject.SetActive(AllowsCategory(mode, tab.Category));
            tab.Button.image.color = tab.Category == category ? _selectedColor : _normalColor;
        }
        _one.Label.text = mode == Mode.Buy ? "1개 구매" : "1개 판매";
        _quantity.Label.text = mode == Mode.Buy ? "수량 구매" : "수량 판매";
        _all.Button.gameObject.SetActive(mode == Mode.Sell);
    }

    public void SetGold(int gold) => _goldText.text = $"보유 골드 {gold:N0} G";
    public void ResetQuantity() => _quantityInput.SetTextWithoutNotify("1");

    /// <summary>
    /// 페이지 내 상품 행과 선택 강조 표시
    /// </summary>
    public void SetRow(int index, Sprite icon, string name, string price, bool selected)
    {
        Row row = _rows[index];
        if (_pulsedIcon == row.Icon.rectTransform) { StopTradePulse(); }
        row.Background.gameObject.SetActive(true);
        row.Background.color = selected ? _selectedColor : _normalColor;
        row.Icon.sprite = icon;
        row.Icon.enabled = icon != null;
        row.Name.text = name;
        row.Price.text = price;
    }

    public void HideRow(int index)
    {
        Row row = _rows[index];
        if (_pulsedIcon == row.Icon.rectTransform) { StopTradePulse(); }
        row.Background.gameObject.SetActive(false);
    }

    /// <summary>거래 후 현재 페이지에 남은 상품 아이콘 강조</summary>
    public void PulseTradeRow(int index)
    {
        if (index < 0 || index >= _rows.Length) { return; }
        Image icon = _rows[index].Icon;
        if (!icon.isActiveAndEnabled || icon.sprite == null) { return; }

        StopTradePulse();
        _pulsedIcon = icon.rectTransform;
        _pulseBaseScale = _pulsedIcon.localScale;
        _tradePulse = _pulsedIcon.DOPunchScale(_pulseBaseScale * 0.06f, 0.18f, 1, 0f)
            .SetUpdate(true).OnComplete(StopTradePulse);
    }

    private void StopTradePulse()
    {
        Tween pulse = _tradePulse;
        _tradePulse = null;
        pulse?.Kill();
        if (_pulsedIcon == null) { return; }
        _pulsedIcon.localScale = _pulseBaseScale;
        _pulsedIcon = null;
    }

    private void OnDisable() => StopTradePulse();

    public void SetPage(int page, int pages, bool empty)
    {
        _pageText.text = $"{page + 1} / {pages}";
        _previousPage.interactable = page > 0;
        _nextPage.interactable = page + 1 < pages;
        _emptyText.gameObject.SetActive(empty);
    }

    /// <summary>
    /// 선택 상품의 설명과 실제 요청 가능한 수량 반영
    /// </summary>
    public void SetDetail(Sprite icon, string name, string description, int maximum, bool canTrade)
    {
        _detailIcon.sprite = icon;
        _detailIcon.enabled = icon != null;
        _detailName.text = name;
        _detailText.text = description;
        int.TryParse(_quantityInput.text, out int count);
        _quantityInput.SetTextWithoutNotify(Mathf.Clamp(count, 1, Math.Max(1, maximum)).ToString());
        _quantityInput.interactable = canTrade && maximum > 1;
        _one.Button.interactable = canTrade;
        _quantity.Button.interactable = canTrade;
        _all.Button.interactable = canTrade;
    }

    public void ClearDetail()
    {
        _detailIcon.enabled = false;
        _detailName.text = "아이템을 선택해 주세요";
        _detailText.text = string.Empty;
        _quantityInput.interactable = false;
        _one.Button.interactable = false;
        _quantity.Button.interactable = false;
        _all.Button.interactable = false;
    }

    // Inspector의 저장된 버튼 연결 진입점
    public void RequestMode(int mode) => ModeRequested?.Invoke((Mode)mode);
    public void RequestCategory(int category) => CategoryRequested?.Invoke((Category)category);
    public void SelectRow(int index) => RowSelected?.Invoke(index);
    public void RequestPage(int direction) => PageRequested?.Invoke(direction);
    public void RequestOne() => TradeRequested?.Invoke(1);
    public void RequestAll() => AllRequested?.Invoke();
    public void RequestMenu() => MenuRequested?.Invoke();

    public void RequestQuantity()
    {
        int.TryParse(_quantityInput.text, out int count);
        TradeRequested?.Invoke(count);
    }
}
