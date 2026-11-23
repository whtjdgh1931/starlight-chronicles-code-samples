using Assets.HeroEditor4D.Common.Scripts.Collections;
using DG.Tweening;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnhanceUI : MonoBehaviour
{
    private UIWindowMotion _windowMotion;
    private Sequence _resultMotion;
    private Vector3 _itemIconBaseScale;
    [Header("UI 연결")]
    /// <summary>
    /// 강화 창 패널
    /// </summary>
    [SerializeField] private GameObject _windowObject;

    /// <summary>
    /// 강화하는 아이템의 아이템이 표시될 이미지
    /// </summary>
    [SerializeField] private Image _itemIcon;

    /// <summary>
    /// 강화하는 아이템의 이름이 표시될 텍스트박스
    /// </summary>
    [SerializeField] private TextMeshProUGUI _itemNameText;

    /// <summary>
    /// 강화 수치 텍스트 박스
    /// </summary>
    [SerializeField] private TextMeshProUGUI _enhanceLevelText;

    [SerializeField] private TextMeshProUGUI _statPreviewText;

    [Header("장비 미선택 안내")]
    [SerializeField] private string _emptyTargetTitle = "강화할 장비를 선택해 주세요";
    [SerializeField, TextArea] private string _emptyTargetDescription = "인벤토리의 장비를 위쪽 슬롯으로 끌어 놓으세요.\n\n장비를 선택하면 능력치 변화와 강화 확률, 비용을 확인할 수 있어요.";
    [SerializeField] private string _emptyItemIconId = "FantasyHeroes.Basic.MeleeWeapon1H.BronzeShortSword";
    [SerializeField] private Color _emptyItemColor = new(0.25f, 0.25f, 0.25f, 0.35f);
    [SerializeField] private string _emptyLevelText = "강화 단계 —";
    [SerializeField, TextArea] private string _emptyProbabilityText = "성공 —\n하락 —\n파괴 —";
    [SerializeField] private string _emptyCostText = "필요 골드 —";
    [SerializeField] private string _emptyButtonLabel = "장비를 선택하세요";
    [SerializeField] private string _enhanceButtonLabel = "강화하기";

    /// <summary>
    /// 강화 확률 텍스트 박스
    /// </summary>
    [SerializeField] private TextMeshProUGUI _probText;

    /// <summary>
    /// 강화하는데 필요한 자원 요구량 텍스트 박스
    /// </summary>
    [SerializeField] private TextMeshProUGUI _costText;

    /// <summary>
    /// 강화 시도 버튼
    /// </summary>
    [SerializeField] private Button _enhanceBtn;

    /// <summary>
    /// 종료 버튼
    /// </summary>
    [SerializeField] private Button _closeBtn;

    [Header("결과 팝업창")]
    [SerializeField] private GameObject _resultPopupObject;
    [SerializeField] private TextMeshProUGUI _resultText;
    [SerializeField] private Button _resultBtn;

    /// <summary>
    /// 강화를 시도하는 아이템의 데이터
    /// </summary>
    private InventoryItemData _targetItem;

    [SerializeField] private IconCollection _iconCollection;

    private IEnhancementRequestHandler _enhancementRequestHandler;
    private StatType _mainStat;
    private Guid _pendingRequestId;
    private InventoryItemData _pendingItem;
    private bool _canPresentPendingResult;
    private int _currentGold;
    private bool _isInteractionLocked;
    private Sprite _emptyItemSprite;
    private TMP_Text _enhanceButtonText;

    /// <summary>
    /// 초기화
    /// </summary>
    public void Initialize()
    {
        _windowMotion = UIWindowMotion.Attach(_windowObject);
        if (!_resultPopupObject.TryGetComponent(out Canvas resultCanvas))
        {
            resultCanvas = _resultPopupObject.AddComponent<Canvas>();
        }
        resultCanvas.overrideSorting = true;
        resultCanvas.sortingOrder = Constants.EnhancementResultSortingOrder;
        if (!_resultPopupObject.TryGetComponent(out GraphicRaycaster _))
        {
            _resultPopupObject.AddComponent<GraphicRaycaster>();
        }

        _itemIconBaseScale = _itemIcon.rectTransform.localScale;
        _enhanceButtonText = _enhanceBtn.GetComponentInChildren<TMP_Text>(true);
        if (_enhanceButtonText == null)
        {
            throw new MissingReferenceException($"{name}: 강화 버튼의 TMP 텍스트 필요");
        }

        _emptyItemSprite = _iconCollection != null && !string.IsNullOrEmpty(_emptyItemIconId)
            ? _iconCollection.GetIcon(_emptyItemIconId)
            : null;

        _enhanceBtn.onClick.RemoveAllListeners();
        _enhanceBtn.onClick.AddListener(TryEnhance);

        _closeBtn.onClick.RemoveAllListeners();
        _closeBtn.onClick.AddListener(CloseWindow);

        _resultBtn.onClick.RemoveAllListeners();
        _resultBtn.onClick.AddListener(ClosePopup);


        EventBus.OnGoldChanged -= HandleGoldChanged;
        EventBus.OnGoldChanged += HandleGoldChanged;

        ClearTarget();
        _resultPopupObject.SetActive(false);
    }

    /// <summary>
    /// 강화 요청 담당자와 창 입력 연결
    /// </summary>
    public void BindEnhancementRequestHandler(IEnhancementRequestHandler requestHandler, StatType mainStat)
    {
        if (requestHandler == null)
        {
            throw new ArgumentNullException(nameof(requestHandler));
        }

        UnbindEnhancementRequestHandler();

        _mainStat = mainStat;
        _enhancementRequestHandler = requestHandler;
        _enhancementRequestHandler.Completed += HandleEnhancementCompleted;
        EventBus.OnEnhanceWindowInputEvent += ToggleWindow;
    }

    /// <summary>
    /// 강화 대상과 창을 유지한 실행 버튼 잠금
    /// </summary>
    public void SetInteractionLocked(bool locked)
    {
        _isInteractionLocked = locked;
        RefreshUI();
    }

    /// <summary>
    /// 강화 요청 담당자와 창 입력 해제 및 대기 정보 정리
    /// 이미 전달된 강화 요청의 실행은 취소하지 않음
    /// </summary>
    public void UnbindEnhancementRequestHandler()
    {
        EventBus.OnEnhanceWindowInputEvent -= ToggleWindow;

        if (_enhancementRequestHandler != null)
        {
            _enhancementRequestHandler.Completed -= HandleEnhancementCompleted;
            _enhancementRequestHandler = null;
        }

        ResetPendingRequest();
        CloseWindowImmediately();
    }

    /// <summary>
    /// 연결된 강화 창의 표시 전환
    /// </summary>
    public void ToggleWindow()
    {
        if (_windowMotion.IsVisible)
        {
            CloseWindow();
        }
        else
        {
            ClearTarget();
            _windowMotion.SetVisible(true);
        }
    }

    /// <summary>
    /// 창 닫기
    /// </summary>
    public void CloseWindow()
    {
        _canPresentPendingResult = false;
        StopResultMotion();
        _resultPopupObject.SetActive(false);
        _windowMotion.SetVisible(false);
    }

    /// <summary>씬 전환·사망·소유권 해제 시 강화창과 결과 즉시 정리</summary>
    public void CloseWindowImmediately()
    {
        StopResultMotion();
        ClearTarget();
        _resultPopupObject.SetActive(false);
        _windowMotion.SetVisible(false, true);
    }

    /// <summary>
    /// 강화 아이템 등록
    /// </summary>
    /// <param name="item"></param>
    public void SetTarget(InventoryItemData item)
    {
        if (!_windowMotion.CanInteract) { return; }
        StopResultMotion();
        bool isEnhanceable = item.ItemData.GetItemType() == GameItemType.Equipment && !item.IsDestroyed;

        if (!isEnhanceable)
        {
            ShowPopup(GameManager.Instance.DataManager.GetString(Constants.STR_Enhance_InvalidItem));
            return;
        }

        // 대상 교체 이전 요청의 결과 팝업 표시 차단
        _canPresentPendingResult = false;
        _targetItem = item;
        _resultPopupObject.SetActive(false);
        RefreshUI();
    }

    private void ShowPopup(string message)
    {
        _resultText.text = message;
        _windowObject.transform.SetAsLastSibling();
        _resultPopupObject.transform.SetAsLastSibling();
        _resultPopupObject.SetActive(true);
    }

    private void ClosePopup()
    {
        StopResultMotion();
        _resultPopupObject.SetActive(false);
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (_targetItem == null)
        {
            RefreshEmptyTarget();
            return;
        }

        var item = _targetItem.ItemData;
        int level = _targetItem.EnhanceLevel;

        DataManager dataManager = GameManager.Instance.DataManager;

        _itemNameText.text = item.Name;
        _enhanceLevelText.text = $"+{level}강";

        _enhanceButtonText.text = _enhanceButtonLabel;
        Sprite icon = _iconCollection != null && !string.IsNullOrEmpty(item.IconId)
            ? _iconCollection.GetIcon(item.IconId)
            : null;
        _itemIcon.sprite = icon;
        _itemIcon.color = icon != null ? Color.white : Color.clear;

        var probData = dataManager.GetEnhanceProb(level);
        RefreshStatPreview(dataManager, probData != null);

        if (probData == null)
        {
            _probText.text = dataManager.GetString(Constants.STR_Enhance_MaxLevel);
            _costText.text = dataManager.GetString(Constants.STR_Enhance_MaxCost);
            _enhanceBtn.interactable = false;
            return;
        }

        _probText.text = dataManager.GetString(Constants.STR_Enhance_ProbFormat,
                probData.SuccessRate * 100,
                probData.DropRate * 100,
                probData.DestroyRate * 100);

        _costText.text = dataManager.GetString(Constants.STR_Enhance_CostFormat, probData.Cost);

        bool hasEnoughGold = _currentGold >= probData.Cost;
        bool isPopupClosed = !_resultPopupObject.activeSelf;

        _enhanceBtn.interactable = !_isInteractionLocked && hasEnoughGold && isPopupClosed &&
            _pendingRequestId == Guid.Empty && _enhancementRequestHandler != null;
    }

    /// <summary>
    /// 실제 강화 계산을 재사용한 현재·성공 후 보너스 표시
    /// 최대 단계에서는 현재 보너스만 표시
    /// </summary>
    private void RefreshStatPreview(DataManager dataManager, bool hasNextLevel)
    {
        ItemData item = _targetItem.ItemData;
        EnhanceStatData data = dataManager.GetEnhanceStat(item.ReqLevel);
        EquipSlot slot = item.GetEquipSlot();
        int level = _targetItem.EnhanceLevel;
        Dictionary<StatType, float> current = data.GetEnhanceBonusModifies(slot, level, _mainStat);
        Dictionary<StatType, float> next = data.GetEnhanceBonusModifies(slot, level + 1, _mainStat);
        List<string> lines = new();

        foreach (KeyValuePair<StatType, float> stat in current)
        {
            string label = dataManager.GetString(Constants.STR_Stat_NamePrefix + stat.Key);
            string line = hasNextLevel
                ? dataManager.GetString(Constants.STR_Enhance_StatPreview, label, stat.Value, next[stat.Key])
                : dataManager.GetString(Constants.STR_Enhance_StatCurrent, label, stat.Value);
            lines.Add(line);
        }

        _statPreviewText.text = string.Join("\n", lines);
    }

    /// <summary>
    /// 선택한 아이템의 강화 요청과 중복 요청 차단
    /// </summary>
    private void TryEnhance()
    {
        if (!_windowMotion.CanInteract) { return; }
        if (_isInteractionLocked || _targetItem == null || _pendingRequestId != Guid.Empty || !_enhanceBtn.interactable)
        {
            return;
        }

        // 즉시 완료되는 로컬 응답보다 먼저 대기 상태 설정
        _pendingRequestId = Guid.NewGuid();
        _pendingItem = _targetItem;
        _canPresentPendingResult = true;
        _enhanceBtn.interactable = false;

        try
        {
            _enhancementRequestHandler.RequestEnhancement(_pendingRequestId, _pendingItem);
        }
        catch
        {
            ResetPendingRequest();
            RefreshUI();
            throw;
        }
    }

    /// <summary>
    /// 확정된 강화 결과와 강화 단계 표시
    /// </summary>
    private void ShowEnhancementResult(EnhancementResult result, int enhanceLevel)
    {
        DataManager dataManager = GameManager.Instance.DataManager;

        switch (result)
        {
            case EnhancementResult.Success:
                ShowPopup(dataManager.GetString(Constants.STR_Enhance_Success, enhanceLevel));
                break;

            case EnhancementResult.LevelDown:
                ShowPopup(dataManager.GetString(Constants.STR_Enhance_Drop, enhanceLevel));
                break;

            case EnhancementResult.Destroyed:
                ShowPopup(dataManager.GetString(Constants.STR_Enhance_Destroy, enhanceLevel));
                ClearTarget();
                return;

            case EnhancementResult.Failed:
                ShowPopup(dataManager.GetString(Constants.STR_Enhance_Fail));
                break;

            case EnhancementResult.NotEnoughGold:
                ShowPopup(dataManager.GetString(Constants.STR_Enhance_NotEnoughGold));
                break;

            case EnhancementResult.MaxLevel:
                Debug.LogWarning("최대 강화 단계이거나 강화 데이터가 없습니다.");
                return;

            case EnhancementResult.InvalidItem:
                ShowPopup(dataManager.GetString(Constants.STR_Enhance_InvalidItem));
                return;
        }

        RefreshUI();
    }

    private void HandleGoldChanged(int newGold)
    {
        _currentGold = newGold;

        if (_windowMotion.IsVisible && _targetItem != null)
        {
            RefreshUI();
        }
    }

    /// <summary>
    /// 현재 대기 요청의 결과 수신과 대상 변경 이후의 표시 차단
    /// </summary>
    private void HandleEnhancementCompleted(Guid requestId, EnhancementResult result, int enhanceLevel)
    {
        if (_pendingRequestId == Guid.Empty || requestId != _pendingRequestId)
        {
            return;
        }

        bool canPresent = _canPresentPendingResult &&
            ReferenceEquals(_pendingItem, _targetItem) && _windowMotion.IsVisible && _windowObject.activeInHierarchy;

        ResetPendingRequest();

        if (!canPresent)
        {
            RefreshUI();
            return;
        }

        ShowEnhancementResult(result, enhanceLevel);
        RefreshUI();
        if (result is EnhancementResult.Success or EnhancementResult.Failed or EnhancementResult.LevelDown)
        {
            PlayResultMotion(result == EnhancementResult.Success);
        }
    }

    private void PlayResultMotion(bool success)
    {
        StopResultMotion();
        if (_itemIcon.sprite == null) { return; }

        _itemIcon.color = success ? new Color(1f, 0.83f, 0.42f) : new Color(0.66f, 0.68f, 0.73f);
        _resultMotion = DOTween.Sequence().SetUpdate(true);
        _resultMotion.Append(_itemIcon.DOColor(Color.white, 0.22f).SetEase(Ease.OutQuad));
        if (success)
        {
            _resultMotion.Insert(0f, _itemIcon.rectTransform.DOScale(_itemIconBaseScale * 1.06f, 0.08f).SetEase(Ease.OutQuad));
            _resultMotion.Insert(0.08f, _itemIcon.rectTransform.DOScale(_itemIconBaseScale, 0.14f).SetEase(Ease.OutQuad));
        }
        _resultMotion.OnComplete(() => _resultMotion = null);
    }

    private void StopResultMotion()
    {
        if (_resultMotion == null) { return; }
        _resultMotion.Kill();
        _resultMotion = null;
        _itemIcon.rectTransform.localScale = _itemIconBaseScale;
        _itemIcon.color = _itemIcon.sprite != null ? Color.white : Color.clear;
    }

    /// <summary>
    /// 완료되거나 연결이 교체된 요청의 대기 정보 초기화
    /// </summary>
    private void ResetPendingRequest()
    {
        _pendingRequestId = Guid.Empty;
        _pendingItem = null;
        _canPresentPendingResult = false;
    }

    /// <summary>
    /// 강화 대상과 결과 표시 권한 초기화
    /// </summary>
    private void ClearTarget()
    {
        _canPresentPendingResult = false;
        _targetItem = null;
        RefreshUI();
    }

    /// <summary>장비 미선택 상태의 안내와 정보 항목 표시</summary>
    private void RefreshEmptyTarget()
    {
        _itemIcon.sprite = _emptyItemSprite;
        _itemIcon.color = _emptyItemSprite != null ? _emptyItemColor : Color.clear;
        _itemNameText.text = _emptyTargetTitle;
        _enhanceLevelText.text = _emptyLevelText;
        _statPreviewText.text = _emptyTargetDescription;
        _probText.text = _emptyProbabilityText;
        _costText.text = _emptyCostText;
        _enhanceButtonText.text = _emptyButtonLabel;
        _enhanceBtn.interactable = false;
    }

    private void OnDisable()
    {
        StopResultMotion();
        _canPresentPendingResult = false;
    }

    private void OnDestroy()
    {
        StopResultMotion();
        if (_enhancementRequestHandler != null)
        {
            _enhancementRequestHandler.Completed -= HandleEnhancementCompleted;
        }

        ResetPendingRequest();
        EventBus.OnEnhanceWindowInputEvent -= ToggleWindow;
        EventBus.OnGoldChanged -= HandleGoldChanged;
    }
}
