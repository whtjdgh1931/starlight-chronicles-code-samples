using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상인 메뉴와 퀘스트 화면 표시 및 버튼 요청 전달
/// </summary>
public sealed class MerchantView : MonoBehaviour
{
    [Serializable]
    private sealed class QuestRow
    {
        public Button Button;
        public TMP_Text Label;
    }

    [SerializeField] private GameObject _menuRoot;
    [SerializeField] private GameObject _questRoot;
    [SerializeField] private MerchantShopView _shopView;
    public MerchantShopView ShopView => _shopView;
    [SerializeField] private QuestRow[] _questRows;
    [SerializeField] private TMP_Text _detailText;
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Button _actionButton;
    [SerializeField] private TMP_Text _actionLabel;

    [SerializeField] private UnityEngine.UI.Button _unfinishedTab;
    [SerializeField] private UnityEngine.UI.Button _completedTab;
    private UIWindowMotion _shopMotion;

    public bool IsOpen => gameObject.activeSelf;
    public bool IsQuestPageOpen => IsOpen && _questRoot.activeSelf;
    public bool IsShopPageOpen => IsOpen && _shopView.gameObject.activeSelf;
    internal bool CanInteractWithShop => IsShopPageOpen && _shopMotion != null && _shopMotion.CanInteract;
    public event Action CloseRequested;
    public event Action MenuRequested;
    public event Action QuestsRequested;
    public event Action ShopRequested;
    public event Action<int> QuestSelected;
    public event Action ActionRequested;
    public event Action<bool> QuestTabRequested;

    /// <summary>
    /// 준비된 퀘스트 수에 맞는 목록 슬롯 검증
    /// </summary>
    public void Initialize(int questCount)
    {
        if (questCount == 0 || _questRows.Length < questCount)
        {
            throw new InvalidOperationException($"{name}: 퀘스트 목록과 충분한 화면 슬롯 필요");
        }
        if (_shopMotion == null) { _shopMotion = UIWindowMotion.Attach(_shopView.gameObject, 1f, 0.12f); }
        Close();
    }

    /// <summary>현재 목록에 해당하는 탭 강조</summary>
    public void SetQuestTab(bool completed)
    {
        _unfinishedTab.interactable = completed;
        _completedTab.interactable = !completed;
    }

    public void ShowMenu()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _menuRoot.SetActive(true);
        _questRoot.SetActive(false);
        HideShop();
        _messageText.text = string.Empty;
    }

    public void ShowQuests()
    {
        _menuRoot.SetActive(false);
        _questRoot.SetActive(true);
        HideShop();
        _messageText.text = string.Empty;
    }

    public void ShowShop()
    {
        _menuRoot.SetActive(false);
        _questRoot.SetActive(false);
        _shopMotion.SetVisible(true);
        _messageText.text = string.Empty;
    }

    public void SetQuestList(IReadOnlyList<string> labels)
    {
        for (int index = 0; index < _questRows.Length; index++)
        {
            bool visible = index < labels.Count;
            _questRows[index].Button.gameObject.SetActive(visible);
            if (visible)
            {
                _questRows[index].Label.text = labels[index];
            }
        }
    }

    public void SetDetail(string detail, string actionLabel, bool canRequest)
    {
        _detailText.text = detail;
        _actionLabel.text = actionLabel;
        _actionButton.interactable = canRequest;
    }

    public void ShowMessage(string message) => _messageText.text = message;
    public void Close()
    {
        HideShop();
        gameObject.SetActive(false);
    }

    private void HideShop()
    {
        if (_shopMotion != null) { _shopMotion.SetVisible(false, true); }
        else { _shopView.gameObject.SetActive(false); }
    }

    // Inspector의 버튼에 연결하는 화면 요청 진입점
    public void RequestClose() => CloseRequested?.Invoke();
    public void RequestMenu() => MenuRequested?.Invoke();
    public void RequestQuests() => QuestsRequested?.Invoke();
    public void RequestShop() => ShopRequested?.Invoke();
    public void SelectQuest(int index) => QuestSelected?.Invoke(index);
    public void SelectQuestTab(bool completed) => QuestTabRequested?.Invoke(completed);
    public void RequestAction() => ActionRequested?.Invoke();
}
